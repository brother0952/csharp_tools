using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace CameraTool
{
    public class CompressionProfile
    {
        public bool UseGpu { get; set; }
        public int Quality { get; set; }
        public string Encoder { get; set; }
        public int QualityValue { get; set; }
        public string Preset => UseGpu ? 
            (Quality <= 1 ? "slow" : Quality <= 2 ? "medium" : "fast") :
            (Quality <= 1 ? "veryslow" : Quality <= 2 ? "slow" : Quality <= 3 ? "medium" : "veryfast");
    }

    public class VideoCompressor
    {
        public string FFmpegPath { get; set; }

        public bool CheckNvidiaSupport()
        {
            try
            {
                if (string.IsNullOrEmpty(FFmpegPath) || !File.Exists(FFmpegPath))
                {
                    return false;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = FFmpegPath,
                    Arguments = "-hide_banner -encoders",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return false;
                }

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output.Contains("h264_nvenc");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检测NVIDIA支持时出错: {ex.Message}");
                return false;
            }
        }

        public void ProcessVideos(string inputFolder, CompressionProfile profile, Action<string> log, CancellationToken cancellationToken = default)
        {
            var videoExtensions = new[] { ".mp4", ".avi", ".mov" };
            var files = Directory.GetFiles(inputFolder)
                .Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLower()));

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    ProcessVideo(file, profile, log);
                }
                catch (Exception ex)
                {
                    log($"处理文件 {Path.GetFileName(file)} 时出错: {ex.Message}");
                }
            }
        }

        private void ProcessVideo(string inputPath, CompressionProfile profile, Action<string> log)
        {
            // 跳过已经压缩过的文件
            if (Path.GetFileName(inputPath).Contains("_cpu_q") || Path.GetFileName(inputPath).Contains("_gpu_q"))
            {
                log($"跳过已压缩文件: {Path.GetFileName(inputPath)}");
                return;
            }

            var outputPath = GetOutputPath(inputPath, profile);
            var originalSize = new FileInfo(inputPath).Length;

            log($"开始处理: {Path.GetFileName(inputPath)}");
            log($"输出文件: {Path.GetFileName(outputPath)}");
            log($"使用编码器: {profile.Encoder}, 预设: {profile.Preset}, 质量值: {profile.QualityValue}");

            var arguments = BuildFFmpegArguments(inputPath, outputPath, profile);
            log($"命令行: {FFmpegPath} {arguments}");

            var startInfo = new ProcessStartInfo
            {
                FileName = FFmpegPath,
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    log($"无法启动FFmpeg进程");
                    return;
                }

                // 读取错误输出
                string errorOutput = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0 && File.Exists(outputPath))
                {
                    var newSize = new FileInfo(outputPath).Length;
                    var ratio = (double)originalSize / newSize;
                    log($"压缩完成: {Path.GetFileName(outputPath)}");
                    log($"原始大小: {FormatFileSize(originalSize)}, 压缩后: {FormatFileSize(newSize)}");
                    log($"压缩比: {ratio:F2}x");
                }
                else
                {
                    log($"压缩失败: {Path.GetFileName(inputPath)}");
                    log($"FFmpeg退出代码: {process.ExitCode}");
                    
                    // 输出完整错误信息
                    if (!string.IsNullOrEmpty(errorOutput))
                    {
                        log($"错误信息: {errorOutput}");
                    }
                    
                    if (File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                        log($"已删除不完整的输出文件");
                    }
                }
            }
            catch (Exception ex)
            {
                log($"处理异常: {ex.Message}");
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }
            return $"{number:n2} {suffixes[counter]}";
        }

        private string GetOutputPath(string inputPath, CompressionProfile profile)
        {
            var dir = Path.GetDirectoryName(inputPath);
            var filename = Path.GetFileNameWithoutExtension(inputPath);
            var ext = Path.GetExtension(inputPath);
            var suffix = profile.UseGpu ? "_gpu" : "_cpu";
            suffix += $"_q{profile.Quality}";
            return Path.Combine(dir, $"{filename}{suffix}{ext}");
        }

        private string BuildFFmpegArguments(string inputPath, string outputPath, CompressionProfile profile)
        {
            // hwaccel 是输入选项，必须放在输入文件前面
            var args = "-hwaccel cuda ";
            args += $"-i \"{inputPath}\" ";

            if (profile.UseGpu)
            {
                // 参考Python版本的NVIDIA GPU参数
                args += $"-c:v {profile.Encoder} ";
                args += $"-preset {profile.Preset} ";
                args += "-tune hq ";
                args += "-rc:v vbr ";
                args += $"-b:v {GetBitrateForQuality(profile.Quality)} ";
                args += $"-maxrate:v {GetMaxBitrateForQuality(profile.Quality)} ";
                args += "-profile:v high ";
            }
            else
            {
                // 参考Python版本的CPU参数
                args += $"-c:v {profile.Encoder} ";
                args += $"-preset {profile.Preset} ";
                args += $"-crf {profile.QualityValue} ";
                args += "-profile:v high ";
            }

            args += "-c:a copy ";  // 复制音频流
            args += $"\"{outputPath}\" -y";

            return args;
        }

        private string GetBitrateForQuality(int quality)
        {
            // 根据质量级别返回合适的比特率
            return quality switch
            {
                0 => "8000k", // 超高质量
                1 => "5000k", // 高质量
                2 => "3000k", // 中等质量
                3 => "1500k", // 低质量
                4 => "800k",  // 最小体积
                _ => "3000k"  // 默认
            };
        }

        private string GetMaxBitrateForQuality(int quality)
        {
            // 最大比特率通常设置为目标比特率的1.5倍
            return quality switch
            {
                0 => "12000k", // 超高质量
                1 => "7500k",  // 高质量
                2 => "4500k",  // 中等质量
                3 => "2250k",  // 低质量
                4 => "1200k",  // 最小体积
                _ => "4500k"   // 默认
            };
        }
    }
} 