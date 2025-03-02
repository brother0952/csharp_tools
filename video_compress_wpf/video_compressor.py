import os
import subprocess
import shutil
from typing import Dict, Optional
import configparser
import sys
import glob


# TODO 中文路径不行

class VideoCompressor:
    def __init__(self):
        self.config = self._load_config()
        self.ffmpeg_path = self.config.get('FFmpeg', 'ffmpeg_path')
        self.input_dir = self._get_input_dir()
        self.control_fps = False
        self.has_nvidia = self._check_nvidia_support()
        self.profiles = self._init_profiles()
        
    def _load_config(self) -> configparser.ConfigParser:
        """加载配置文件"""
        config = configparser.ConfigParser()
        config_path = os.path.join(os.path.dirname(__file__), 'config.ini')
        
        if not os.path.exists(config_path):
            print(f"Error: Configuration file 'config.ini' not found in {os.path.dirname(__file__)}")
            sys.exit(1)
            
        config.read(config_path, encoding='utf-8')  # 指定UTF-8编码
        return config
        
    def _get_input_dir(self) -> str:
        """获取用户输入的目录路径"""
        while True:
            dir_path = input("Please enter the directory path containing videos to compress: ").strip()
            if not dir_path:
                print("Error: Directory path cannot be empty")
                continue
                
            # 移除引号（如果有）
            dir_path = dir_path.strip('"\'')
            
            if not os.path.exists(dir_path):
                print(f"Error: Directory '{dir_path}' does not exist")
                continue
                
            if not os.path.isdir(dir_path):
                print(f"Error: '{dir_path}' is not a directory")
                continue
                
            return dir_path

    def _check_nvidia_support(self) -> bool:
        """检查是否支持NVIDIA编码器"""
        try:
            result = subprocess.run(
                [self.ffmpeg_path, "-hide_banner", "-encoders"],
                capture_output=True,
                text=True
            )
            return "h264_nvenc" in result.stdout
        except Exception:
            return False

    def _init_profiles(self) -> Dict:
        """初始化压缩配置文件"""
        profiles = {
            # CPU配置
            1: {
                "name": "Ultra High Quality",
                "quality": 18,
                "preset": "veryslow",
                "suffix": "_uhq",
                "encoder": "libx264"
            },
            2: {
                "name": "High Quality",
                "quality": 23,
                "preset": "slow",
                "suffix": "_hq",
                "encoder": "libx264"
            },
            3: {
                "name": "Medium Quality",
                "quality": 28,
                "preset": "medium",
                "suffix": "_mq",
                "encoder": "libx264"
            },
            4: {
                "name": "Low Quality High Compression",
                "quality": 33,
                "preset": "veryfast",
                "suffix": "_lq",
                "encoder": "libx264"
            },
            5: {
                "name": "Maximum Compression",
                "quality": 40,
                "preset": "ultrafast",
                "suffix": "_min",
                "encoder": "libx264"
            }
        }

        if self.has_nvidia:
            # GPU配置
            gpu_profiles = {
                1: {
                    "name": "Ultra High Quality (GPU)",
                    "quality": 18,
                    "preset": "slow",
                    "suffix": "_uhq_gpu",
                    "encoder": "h264_nvenc"
                },
                2: {
                    "name": "High Quality (GPU)",
                    "quality": 23,
                    "preset": "slow",
                    "suffix": "_hq_gpu",
                    "encoder": "h264_nvenc"
                },
                3: {
                    "name": "Medium Quality (GPU)",
                    "quality": 28,
                    "preset": "medium",
                    "suffix": "_mq_gpu",
                    "encoder": "h264_nvenc"
                },
                4: {
                    "name": "Low Quality High Compression (GPU)",
                    "quality": 33,
                    "preset": "fast",
                    "suffix": "_lq_gpu",
                    "encoder": "h264_nvenc"
                },
                5: {
                    "name": "Maximum Compression (GPU)",
                    "quality": 40,
                    "preset": "fast",
                    "suffix": "_min_gpu",
                    "encoder": "h264_nvenc"
                }
            }
            # 添加CPU配置作为额外选项
            gpu_profiles[6] = profiles[1].copy()
            gpu_profiles[6]["name"] = "Ultra High Quality (CPU)"
            gpu_profiles[6]["suffix"] = "_uhq_cpu"
            
            gpu_profiles[7] = profiles[2].copy()
            gpu_profiles[7]["name"] = "High Quality (CPU)"
            gpu_profiles[7]["suffix"] = "_hq_cpu"
            
            return gpu_profiles
        return profiles

    def _get_video_info(self, video_path: str) -> Optional[Dict]:
        """获取视频信息"""
        try:
            # 获取FPS和比特率
            result = subprocess.run(
                [self.ffmpeg_path, "-i", video_path],
                capture_output=True,
                text=True
            )
            
            info = {}
            output = result.stderr
            
            # 解析FPS和比特率
            for line in output.split('\n'):
                # 查找包含 fps 的行
                if "fps" in line:
                    # 更健壮的FPS提取
                    try:
                        parts = line.split(',')
                        for part in parts:
                            if 'fps' in part:
                                fps_str = ''.join(filter(lambda x: x.isdigit() or x == '.', part))
                                info['fps'] = float(fps_str)
                                break
                    except (ValueError, IndexError):
                        continue

                # 查找包含 bitrate 的行
                if "bitrate" in line:
                    try:
                        parts = line.split()
                        for i, part in enumerate(parts):
                            if part == "kb/s":
                                info['bitrate'] = int(parts[i-1])
                                break
                    except (ValueError, IndexError):
                        continue
            
            # 获取文件大小
            info['size'] = os.path.getsize(video_path)
            
            # 确保所有必要的信息都存在
            if 'fps' not in info or 'bitrate' not in info:
                print(f"Warning: Could not extract complete video info from {video_path}")
                info['fps'] = info.get('fps', 30)  # 默认30fps
                info['bitrate'] = info.get('bitrate', 5000)  # 默认5Mbps
            
            print(f"Debug - Extracted info: FPS={info.get('fps')}, Bitrate={info.get('bitrate')}kb/s")
            return info
        except Exception as e:
            print(f"Error getting video info: {str(e)}")
            return None

    def _compress_video(self, input_path: str, output_path: str, profile: Dict, video_info: Dict):
        """压缩视频"""
        # 从配置文件读取质量因子
        quality_factor = {
            18: float(self.config.get('Quality', 'uhq_factor')),
            23: float(self.config.get('Quality', 'hq_factor')),
            28: float(self.config.get('Quality', 'mq_factor')),
            33: float(self.config.get('Quality', 'lq_factor')),
            40: float(self.config.get('Quality', 'min_factor')),
        }
        
        # 从配置文件读取码率限制
        min_bitrate = int(self.config.get('Bitrate', 'min_bitrate'))
        max_bitrate = int(self.config.get('Bitrate', 'max_bitrate'))
        
        # 计算目标码率，但设置上下限
        target_bitrate = int(video_info['bitrate'] * quality_factor.get(profile["quality"], 0.5) / 1000)
        target_bitrate = max(min(target_bitrate, max_bitrate), min_bitrate)
        
        command = []

        if profile["encoder"] == "h264_nvenc":
            # GPU encoding with quality-focused parameters
            command.extend([
                self.ffmpeg_path,
                "-hwaccel", "cuda",
                "-i", input_path,
                "-c:v", "h264_nvenc",
                "-preset", profile["preset"],
                "-rc:v", "vbr_hq",
                "-cq:v", str(profile["quality"]),  # 使用固定质量参数
                "-b:v", f"{target_bitrate}M",
                "-maxrate:v", f"{int(target_bitrate * 1.2)}M",  # 稍微提高峰值码率
                "-bufsize", f"{target_bitrate}M",
                "-profile:v", "high",
                "-rc-lookahead", "32",
                "-spatial-aq", "1",
                "-temporal-aq", "1",
                "-aq-strength", "8",
                "-refs", "4"  # 减少参考帧数以降低文件大小
            ])
        else:
            # CPU encoding with quality-focused parameters
            command.extend([
                self.ffmpeg_path,
                "-i", input_path,
                "-c:v", "libx264",
                "-preset", profile["preset"],
                "-crf", str(profile["quality"]),
                "-maxrate:v", f"{target_bitrate}M",
                "-bufsize", f"{target_bitrate}M",
                "-profile:v", "high",
                "-level", "4.1",
                "-movflags", "+faststart",
                "-x264-params", "ref=4:me=hex:subme=6:rc-lookahead=30:me-range=16:chroma-qp-offset=0:bframes=3:b-adapt=1"
            ])

        # 不再强制更改帧率，保持原始帧率
        if self.control_fps and video_info['fps'] >= 50:
            print(f"High FPS detected ({video_info['fps']}), but keeping original frame rate as requested")

        command.extend([
            "-c:a", "copy",  # 复制音频流，不重新编码
            "-y",
            output_path
        ])

        try:
            print("\nDebug - Compression settings:")
            print(f"Original bitrate: {video_info['bitrate']} kb/s ({video_info['bitrate']/1000:.1f} Mbps)")
            print(f"Target bitrate: {target_bitrate} Mbps")
            print(f"Quality factor: {quality_factor.get(profile['quality'], 0.5)}")
            print(f"Original FPS: {video_info['fps']}")
            print("Debug - Running command:", " ".join(command))
            
            result = subprocess.run(command, capture_output=True, text=True)
            if result.returncode != 0:
                print(f"FFmpeg error output: {result.stderr}")
                return False
            return True
        except subprocess.CalledProcessError as e:
            print(f"Error compressing video: {e}")
            if e.stderr:
                print(f"FFmpeg error output: {e.stderr}")
            return False


    def process_videos(self):
        """处理视频"""
        if not os.path.exists(self.input_dir):
            print(f"Error: Input directory '{self.input_dir}' does not exist.")
            return

        # 获取所有可能的压缩后缀
        all_suffixes = set()
        for profile in self.profiles.values():
            all_suffixes.add(profile['suffix'])

        video_files = []
        for ext in ['.mp4', '.avi', '.mov']:
            files = glob.glob(os.path.join(self.input_dir, f"*{ext}"))
            # 过滤掉已经压缩过的文件（文件名中包含任何压缩后缀的文件）
            for file in files:
                filename = os.path.basename(file)
                if not any(suffix in filename for suffix in all_suffixes):
                    video_files.append(file)

        if not video_files:
            print("No uncompressed video files found in input directory.")
            return

        # 使用配置文件中的默认配置
        default_profile_num = int(self.config.get('Compression', 'default_profile'))
        if default_profile_num not in self.profiles:
            print(f"Error: Invalid default profile number {default_profile_num}")
            return
            
        profile = self.profiles[default_profile_num]
        print(f"\nUsing default profile: {profile['name']}")
        print("Starting video processing...\n")

        for video_path in video_files:
            filename = os.path.basename(video_path)
            dirname = os.path.dirname(video_path)
            base_name, ext = os.path.splitext(filename)
            
            # 检查是否已经存在压缩过的文件
            compressed_filename = f"{base_name}{profile['suffix']}{ext}"
            output_path = os.path.join(dirname, compressed_filename)
            
            if os.path.exists(output_path):
                print(f"Skipping {filename} - compressed version already exists")
                continue

            print(f"Processing: {filename}")

            video_info = self._get_video_info(video_path)
            if not video_info:
                continue

            if video_info['bitrate'] <= 1000:  # 2 Mbps
                print("Original bitrate too low, skipping compression")
                continue

            print(f"Original FPS: {video_info['fps']}")
            print(f"Original bitrate: {video_info['bitrate'] // 1000} Mbps")
            print(f"Original size: {video_info['size']} bytes")

            if self._compress_video(video_path, output_path, profile, video_info):
                if os.path.exists(output_path):
                    new_size = os.path.getsize(output_path)
                    if new_size >= video_info['size']:
                        print("Warning: Compressed file is larger, keeping original")
                        os.remove(output_path)
                    else:
                        saved_space = video_info['size'] - new_size
                        saved_ratio = saved_space * 100 / video_info['size']
                        print(f"Space saved ratio: {saved_ratio/100:.2f}x")
                        print(f"Compressed file saved as: {compressed_filename}")

            print(f"Completed: {filename}\n")

        print("All videos processed!")

if __name__ == "__main__":
    try:
        compressor = VideoCompressor()
        compressor.process_videos()
    except KeyboardInterrupt:
        print("\nOperation cancelled by user")
    except Exception as e:
        print(f"\nError: {str(e)}")
    finally:
        input("\nPress Enter to exit...") 