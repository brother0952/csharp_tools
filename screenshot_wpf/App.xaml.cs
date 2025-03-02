using System.Windows;

namespace ScreenshotTool
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // 创建并显示主窗口
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
} 