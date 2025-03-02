using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using Point = System.Windows.Point;

namespace ScreenshotTool
{
    public partial class ScreenshotWindow : Window
    {
        private Point startPoint;
        private bool isDrawing;

        public ScreenshotWindow()
        {
            InitializeComponent();
            
            this.MouseLeftButtonDown += OnMouseLeftButtonDown;
            this.MouseMove += OnMouseMove;
            this.MouseLeftButtonUp += OnMouseLeftButtonUp;
            this.KeyDown += OnKeyDown;
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            startPoint = e.GetPosition(canvas);
            isDrawing = true;
            Canvas.SetLeft(selectionRect, startPoint.X);
            Canvas.SetTop(selectionRect, startPoint.Y);
            selectionRect.Width = 0;
            selectionRect.Height = 0;
            selectionRect.Visibility = Visibility.Visible;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!isDrawing) return;

            var currentPoint = e.GetPosition(canvas);
            var x = Math.Min(startPoint.X, currentPoint.X);
            var y = Math.Min(startPoint.Y, currentPoint.Y);
            var width = Math.Abs(currentPoint.X - startPoint.X);
            var height = Math.Abs(currentPoint.Y - startPoint.Y);

            Canvas.SetLeft(selectionRect, x);
            Canvas.SetTop(selectionRect, y);
            selectionRect.Width = width;
            selectionRect.Height = height;
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isDrawing) return;
            isDrawing = false;

            if (selectionRect.Width > 5 && selectionRect.Height > 5)
            {
                CaptureScreen();
            }

            this.Close();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }

        private void CaptureScreen()
        {
            var x = (int)Canvas.GetLeft(selectionRect);
            var y = (int)Canvas.GetTop(selectionRect);
            var width = (int)selectionRect.Width;
            var height = (int)selectionRect.Height;

            using (var bitmap = new Bitmap(width, height))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height));
                }

                try
                {
                    var bitmapSource = ConvertBitmapToBitmapSource(bitmap);
                    var editWindow = new EditWindow(bitmapSource);
                    
                    if (editWindow.ShowDialog() == true)
                    {
                        Clipboard.SetImage(editWindow.Screenshot);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("截图失败: {0}", ex.Message));
                }
            }
        }

        private System.Windows.Media.Imaging.BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly, bitmap.PixelFormat);

            var bitmapSource = System.Windows.Media.Imaging.BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                PixelFormats.Bgr24, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);
            return bitmapSource;
        }
    }
} 