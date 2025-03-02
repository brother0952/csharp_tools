using System;
using System.Windows;
using System.Windows.Input;

namespace ScreenshotTool
{
    public partial class MainWindow : Window
    {
        private ScreenshotWindow screenshotWindow;

        public MainWindow()
        {
            InitializeComponent();
            
            // 注册全局热键
            HotKeyManager.RegisterHotKey(Key.F1, KeyModifier.None, StartCapture);
        }

        private void BtnCapture_Click(object sender, RoutedEventArgs e)
        {
            StartCapture();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();  // 允许拖动窗口
        }

        private void StartCapture()
        {
            this.Hide();  // 隐藏主窗口
            screenshotWindow = new ScreenshotWindow();
            screenshotWindow.ShowDialog();
            this.Show();  // 截图完成后显示主窗口
        }

        protected override void OnClosed(EventArgs e)
        {
            HotKeyManager.UnregisterHotKey();
            base.OnClosed(e);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HotKeyManager.Initialize(this);
        }
    }
} 