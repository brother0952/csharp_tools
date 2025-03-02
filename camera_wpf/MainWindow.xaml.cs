using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace CameraTool
{
    public partial class MainWindow : Window
    {
        private CameraManager cameraManager;
        private bool isRunning;

        public MainWindow()
        {
            InitializeComponent();
            InitializeCamera();
        }

        private void InitializeCamera()
        {
            cameraManager = new CameraManager();
            cameraManager.NewFrame += CameraManager_NewFrame;
            cameraManager.FPSUpdated += CameraManager_FPSUpdated;

            var devices = cameraManager.GetDeviceNames();
            foreach (var device in devices)
            {
                cmbCameras.Items.Add(device);
            }

            if (cmbCameras.Items.Count > 0)
            {
                cmbCameras.SelectedIndex = 0;
            }
        }

        private void CameraManager_NewFrame(BitmapSource frame)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                imgPreview.Source = frame;
            }));
        }

        private void CameraManager_FPSUpdated(float fps)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                txtFPS.Text = $"FPS: {fps:F1}";
            }));
        }

        private void BtnStartStop_Click(object sender, RoutedEventArgs e)
        {
            if (!isRunning)
            {
                cameraManager.SelectDevice(cmbCameras.SelectedIndex);
                cameraManager.StartCamera();
                btnStartStop.Content = "停止摄像头";
                isRunning = true;
            }
            else
            {
                cameraManager.StopCamera();
                btnStartStop.Content = "开启摄像头";
                isRunning = false;
            }
        }

        private void ChkShowFPS_CheckedChanged(object sender, RoutedEventArgs e)
        {
            txtFPS.Visibility = chkShowFPS.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (cameraManager == null || !isRunning) return;

            var slider = sender as Slider;
            if (slider == null) return;

            CameraParameter parameter;
            switch (slider.Name)
            {
                case "sldBrightness":
                    parameter = CameraParameter.Brightness;
                    break;
                case "sldContrast":
                    parameter = CameraParameter.Contrast;
                    break;
                case "sldSaturation":
                    parameter = CameraParameter.Saturation;
                    break;
                default:
                    return;
            }

            cameraManager.SetParameter(parameter, (int)slider.Value);
        }

        private void CmbResolutions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cameraManager == null || !isRunning) return;

            // 重新启动摄像头以应用新的分辨率
            cameraManager.StopCamera();
            cameraManager.SelectDevice(cmbCameras.SelectedIndex);
            cameraManager.StartCamera();
        }

        protected override void OnClosed(EventArgs e)
        {
            cameraManager?.Dispose();
            base.OnClosed(e);
        }
    }
} 