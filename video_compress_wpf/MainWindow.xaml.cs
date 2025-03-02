using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Threading;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using System.Threading;
using System.Windows.Media;

namespace CameraTool
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private string _ffmpegPath;
        private string _inputFolder;
        private string _logText = "";
        private bool _isProcessing = false;
        private VideoCompressor _compressor;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _initialized = false;
        private bool _deleteOriginalFiles;

        public event PropertyChangedEventHandler PropertyChanged;

        public string FFmpegPath
        {
            get => _ffmpegPath;
            set
            {
                if (_ffmpegPath != value)
                {
                    _ffmpegPath = value;
                    OnPropertyChanged(nameof(FFmpegPath));
                    // 只有在非初始化阶段才保存设置
                    if (_initialized)
                    {
                        SaveSettings();
                    }
                }
            }
        }

        public string InputFolder
        {
            get => _inputFolder;
            set
            {
                if (_inputFolder != value)
                {
                    _inputFolder = value;
                    OnPropertyChanged(nameof(InputFolder));
                    // 只有在非初始化阶段才保存设置
                    if (_initialized)
                    {
                        SaveSettings();
                    }
                }
            }
        }

        public string LogText
        {
            get => _logText;
            set
            {
                _logText = value;
                OnPropertyChanged(nameof(LogText));
            }
        }

        public bool DeleteOriginalFiles
        {
            get => _deleteOriginalFiles;
            set
            {
                if (_deleteOriginalFiles != value)
                {
                    _deleteOriginalFiles = value;
                    OnPropertyChanged(nameof(DeleteOriginalFiles));
                    // 只有在非初始化阶段才保存设置
                    if (_initialized)
                    {
                        SaveSettings();
                    }
                }
            }
        }

        public MainWindow()
        {
            // 标记为未初始化状态
            _initialized = false;
            
            InitializeComponent();
            DataContext = this;
            
            // 初始化组件
            _compressor = new VideoCompressor();
            
            // 禁用事件处理器，避免在加载设置时触发保存
            EncoderComboBox.SelectionChanged -= EncoderComboBox_SelectionChanged;
            QualityComboBox.SelectionChanged -= QualityComboBox_SelectionChanged;
            
            // 加载设置
            LoadSettings();
            
            // 使用Dispatcher延迟执行，确保UI已完全加载
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                try
                {
                    // 设置质量选择
                    int savedQuality = Properties.Settings.Default.QualityIndex;
                    if (savedQuality >= 0 && savedQuality < QualityComboBox.Items.Count)
                    {
                        QualityComboBox.SelectedIndex = savedQuality;
                        LogThreadSafe($"已设置质量选择: {savedQuality}");
                    }
                    
                    // 如果有保存的FFmpeg路径，尝试检测NVIDIA支持
                    if (!string.IsNullOrEmpty(FFmpegPath) && File.Exists(FFmpegPath))
                    {
                        _compressor.FFmpegPath = FFmpegPath;
                        UpdateNvidiaSupport();
                        
                        // 如果支持NVIDIA且有保存的编码器选择，则恢复选择
                        int savedEncoder = Properties.Settings.Default.EncoderIndex;
                        if (savedEncoder == 1 && 
                            EncoderComboBox.Items.Count > 1 && 
                            EncoderComboBox.Items[1] is ComboBoxItem nvItem && 
                            nvItem.IsEnabled)
                        {
                            EncoderComboBox.SelectedIndex = 1;
                            LogThreadSafe("已恢复NVIDIA GPU编码器设置");
                        }
                        else
                        {
                            EncoderComboBox.SelectedIndex = 0;
                            LogThreadSafe("已设置CPU编码器");
                        }
                    }
                    else
                    {
                        EncoderComboBox.SelectedIndex = 0;
                        LogThreadSafe("已设置CPU编码器（默认）");
                    }
                    
                    // 重新启用事件处理器
                    EncoderComboBox.SelectionChanged += EncoderComboBox_SelectionChanged;
                    QualityComboBox.SelectionChanged += QualityComboBox_SelectionChanged;
                    
                    // 标记初始化完成
                    _initialized = true;
                    
                    // 手动保存一次当前设置，确保正确保存
                    SaveSettingsSilently();
                    
                    LogThreadSafe("UI设置已完成");
                }
                catch (Exception ex)
                {
                    LogThreadSafe($"UI设置时出错: {ex.Message}");
                }
            }));
            
            // 记录初始化完成
            LogThreadSafe("应用程序已启动");
        }

        private void LoadSettings()
        {
            try
            {
                // 先获取设置值
                string savedPath = Properties.Settings.Default.FFmpegPath;
                string savedFolder = Properties.Settings.Default.LastFolder;
                int savedQuality = Properties.Settings.Default.QualityIndex;
                int savedEncoder = Properties.Settings.Default.EncoderIndex;
                bool deleteOriginal = Properties.Settings.Default.DeleteOriginalFiles;
                
                // 确保索引值有效
                if (savedQuality < 0) savedQuality = 1; // 默认高质量
                if (savedEncoder < 0) savedEncoder = 0; // 默认CPU
                
                // 记录日志
                LogThreadSafe($"读取设置: FFmpeg路径={savedPath}, 编码器={savedEncoder}, 质量={savedQuality}, 上次文件夹={savedFolder}, 删除原始文件={deleteOriginal}");
                
                // 设置属性值（不触发保存）
                _ffmpegPath = savedPath;
                _inputFolder = savedFolder;
                _deleteOriginalFiles = deleteOriginal;
                
                // 通知属性变更
                OnPropertyChanged(nameof(FFmpegPath));
                OnPropertyChanged(nameof(InputFolder));
                OnPropertyChanged(nameof(DeleteOriginalFiles));
            }
            catch (Exception ex)
            {
                LogThreadSafe($"加载设置时出错: {ex.Message}");
            }
        }

        private void SaveSettings()
        {
            try
            {
                // 获取当前选择的索引
                int encoderIndex = EncoderComboBox.SelectedIndex;
                int qualityIndex = QualityComboBox.SelectedIndex;
                
                // 只有在索引有效时才保存
                if (encoderIndex >= 0)
                {
                    Properties.Settings.Default.EncoderIndex = encoderIndex;
                }
                
                if (qualityIndex >= 0)
                {
                    Properties.Settings.Default.QualityIndex = qualityIndex;
                }
                
                // 其他设置正常保存
                Properties.Settings.Default.FFmpegPath = FFmpegPath;
                Properties.Settings.Default.LastFolder = InputFolder;
                Properties.Settings.Default.DeleteOriginalFiles = DeleteOriginalFiles;
                Properties.Settings.Default.Save();
                
                LogThreadSafe($"设置已保存: FFmpeg路径={FFmpegPath}, 编码器={Properties.Settings.Default.EncoderIndex}, 质量={Properties.Settings.Default.QualityIndex}, 文件夹={InputFolder}, 删除原始文件={DeleteOriginalFiles}");
            }
            catch (Exception ex)
            {
                LogThreadSafe($"保存设置时出错: {ex.Message}");
            }
        }

        private void SaveSettingsSilently()
        {
            try
            {
                // 获取当前选择的索引
                int encoderIndex = EncoderComboBox.SelectedIndex;
                int qualityIndex = QualityComboBox.SelectedIndex;
                
                // 只有在索引有效时才保存
                if (encoderIndex >= 0)
                {
                    Properties.Settings.Default.EncoderIndex = encoderIndex;
                }
                
                if (qualityIndex >= 0)
                {
                    Properties.Settings.Default.QualityIndex = qualityIndex;
                }
                
                // 其他设置正常保存
                Properties.Settings.Default.FFmpegPath = _ffmpegPath;
                Properties.Settings.Default.LastFolder = _inputFolder;
                Properties.Settings.Default.DeleteOriginalFiles = _deleteOriginalFiles;
                Properties.Settings.Default.Save();
                
                // 可选：记录实际保存的值
                // Console.WriteLine($"静默保存: 编码器={Properties.Settings.Default.EncoderIndex}, 质量={Properties.Settings.Default.QualityIndex}");
            }
            catch (Exception ex)
            {
                // 静默处理异常
                Console.WriteLine($"静默保存设置时出错: {ex.Message}");
            }
        }

        private void BrowseFFmpeg_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "FFmpeg|ffmpeg.exe|所有文件|*.*",
                Title = "选择FFmpeg可执行文件"
            };

            if (dialog.ShowDialog() == true)
            {
                FFmpegPath = dialog.FileName;
                UpdateNvidiaSupport(); // 更新NVIDIA支持状态
            }
        }

        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                InputFolder = dialog.SelectedPath;
                SaveSettings(); // 保存最后使用的文件夹
            }
        }

        private void UpdateButtonState(bool isProcessing)
        {
            _isProcessing = isProcessing;
            StartButton.IsEnabled = !isProcessing;
            StopButton.IsEnabled = isProcessing;
        }

        private async void StartCompression_Click(object sender, RoutedEventArgs e)
        {
            // 重置自动滚动
            ScrollViewerExtensions.ResetAutoScroll(LogScrollViewer);
            
            if (_isProcessing)
            {
                LogThreadSafe("正在处理中，请等待...");
                return;
            }

            if (string.IsNullOrEmpty(FFmpegPath) || !File.Exists(FFmpegPath))
            {
                LogThreadSafe("请先设置正确的FFmpeg路径！");
                return;
            }

            if (string.IsNullOrEmpty(InputFolder) || !Directory.Exists(InputFolder))
            {
                LogThreadSafe("请选择有效的输入文件夹！");
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            UpdateButtonState(true);
            
            try
            {
                var profile = GetCurrentProfile();
                _compressor.FFmpegPath = FFmpegPath;
                
                // 如果选择了删除原始文件，添加确认对话框
                if (DeleteOriginalFiles)
                {
                    var result = System.Windows.MessageBox.Show(
                        "您选择了压缩完成后删除原始文件，此操作不可恢复！\n\n确定要继续吗？",
                        "危险操作确认",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning);
                        
                    if (result != System.Windows.MessageBoxResult.Yes)
                    {
                        LogThreadSafe("操作已取消");
                        UpdateButtonState(false);
                        return;
                    }
                }
                
                await Task.Run(() => _compressor.ProcessVideos(InputFolder, profile, LogThreadSafe, DeleteOriginalFiles, _cancellationTokenSource.Token));
            }
            catch (OperationCanceledException)
            {
                LogThreadSafe("压缩已停止");
            }
            catch (Exception ex)
            {
                LogThreadSafe($"处理出错: {ex.Message}");
            }
            finally
            {
                UpdateButtonState(false);
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void StopCompression_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            LogThreadSafe("正在停止压缩...");
        }

        private void EncoderComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // 只在初始化完成后才保存设置
            if (_initialized)
            {
                SaveSettings();
            }
        }

        private void QualityComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // 只在初始化完成后才保存设置
            if (_initialized)
            {
                SaveSettings();
            }
        }

        private CompressionProfile GetCurrentProfile()
        {
            bool useGpu = EncoderComboBox.SelectedIndex == 1;
            int quality = QualityComboBox.SelectedIndex;
            
            return new CompressionProfile
            {
                UseGpu = useGpu,
                Quality = quality,
                Encoder = useGpu ? "h264_nvenc" : "libx264",
                QualityValue = quality switch
                {
                    0 => 18, // 超高质量
                    1 => 23, // 高质量
                    2 => 28, // 中等质量
                    3 => 33, // 低质量
                    4 => 40, // 最小体积
                    _ => 23  // 默认高质量
                }
            };
        }

        private void LogThreadSafe(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => LogThreadSafe(message));
                return;
            }

            // 使用数据绑定更新日志
            LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            
            // 尝试滚动到底部
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => {
                try
                {
                    // 如果用户没有手动滚动，则自动滚动到底部
                    if (LogScrollViewer != null && 
                        Math.Abs(LogScrollViewer.VerticalOffset - LogScrollViewer.ScrollableHeight) < 1.0)
                    {
                        LogScrollViewer.ScrollToEnd();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"滚动日志时出错: {ex.Message}");
                }
            }));
        }

        // 辅助方法：查找视觉树中的子元素
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T result)
                {
                    return result;
                }
                
                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            
            return null;
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                SaveSettings();
                LogThreadSafe("应用程序关闭，设置已保存");
            }
            catch (Exception ex)
            {
                LogThreadSafe($"关闭时保存设置出错: {ex.Message}");
            }
            base.OnClosing(e);
        }

        // 添加一个方法来更新NVIDIA支持状态
        private void UpdateNvidiaSupport()
        {
            if (string.IsNullOrEmpty(FFmpegPath) || !File.Exists(FFmpegPath))
            {
                LogThreadSafe("FFmpeg路径无效，无法检测NVIDIA支持");
                if (EncoderComboBox.Items.Count > 1 && EncoderComboBox.Items[1] is ComboBoxItem gpuItem)
                {
                    gpuItem.IsEnabled = false;
                }
                EncoderComboBox.SelectedIndex = 0;
                return;
            }

            LogThreadSafe("正在检测NVIDIA支持...");
            _compressor.FFmpegPath = FFmpegPath;
            bool hasNvidia = _compressor.CheckNvidiaSupport();
            
            if (hasNvidia)
            {
                LogThreadSafe("检测到NVIDIA GPU支持");
            }
            else
            {
                LogThreadSafe("未检测到NVIDIA GPU支持");
            }
            
            if (EncoderComboBox.Items.Count > 1 && EncoderComboBox.Items[1] is ComboBoxItem nvItem)
            {
                nvItem.IsEnabled = hasNvidia;
            }
            
            if (!hasNvidia && EncoderComboBox.SelectedIndex == 1)
            {
                EncoderComboBox.SelectedIndex = 0;
            }
        }

        private void ResetSettings()
        {
            try
            {
                Properties.Settings.Default.Reset();
                LogThreadSafe("设置已重置为默认值");
            }
            catch (Exception ex)
            {
                LogThreadSafe($"重置设置时出错: {ex.Message}");
            }
        }
    }
} 