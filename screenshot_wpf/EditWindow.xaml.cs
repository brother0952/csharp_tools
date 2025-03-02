using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ScreenshotTool
{
    public partial class EditWindow : Window
    {
        private Point startPoint;
        private UIElement currentElement;
        private DrawingMode currentMode = DrawingMode.Arrow;
        private Color currentColor = Colors.Red;
        private TextBox currentTextBox;

        public BitmapSource Screenshot { get; private set; }

        public EditWindow(BitmapSource screenshot)
        {
            InitializeComponent();
            Screenshot = screenshot;
            backgroundImage.Source = screenshot;

            // 初始化颜色选择器
            colorPicker.ItemsSource = new SolidColorBrush[]
            {
                new SolidColorBrush(Colors.Red),
                new SolidColorBrush(Colors.Blue),
                new SolidColorBrush(Colors.Green),
                new SolidColorBrush(Colors.Yellow),
                new SolidColorBrush(Colors.White),
                new SolidColorBrush(Colors.Black)
            };
        }

        private void ToolButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as RadioButton;
            if (button == null) return;

            switch (button.Name)
            {
                case "btnArrow": currentMode = DrawingMode.Arrow; break;
                case "btnRectangle": currentMode = DrawingMode.Rectangle; break;
                case "btnText": currentMode = DrawingMode.Text; break;
                case "btnPen": currentMode = DrawingMode.Pen; break;
            }
        }

        private void ColorPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var brush = colorPicker.SelectedItem as SolidColorBrush;
            if (brush != null)
            {
                currentColor = brush.Color;
            }
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            startPoint = e.GetPosition(canvas);

            switch (currentMode)
            {
                case DrawingMode.Arrow:
                    var arrow = new ArrowLine
                    {
                        StartPoint = startPoint,
                        EndPoint = startPoint,
                        Stroke = new SolidColorBrush(currentColor),
                        StrokeThickness = 2
                    };
                    currentElement = arrow;
                    break;

                case DrawingMode.Rectangle:
                    var rect = new Rectangle
                    {
                        Stroke = new SolidColorBrush(currentColor),
                        StrokeThickness = 2
                    };
                    Canvas.SetLeft(rect, startPoint.X);
                    Canvas.SetTop(rect, startPoint.Y);
                    currentElement = rect;
                    break;

                case DrawingMode.Text:
                    var textBox = new TextBox
                    {
                        Background = Brushes.Transparent,
                        Foreground = new SolidColorBrush(currentColor),
                        BorderThickness = new Thickness(0),
                        FontSize = 16
                    };
                    Canvas.SetLeft(textBox, startPoint.X);
                    Canvas.SetTop(textBox, startPoint.Y);
                    currentElement = textBox;
                    currentTextBox = textBox;
                    break;

                case DrawingMode.Pen:
                    var path = new Path
                    {
                        Stroke = new SolidColorBrush(currentColor),
                        StrokeThickness = 2
                    };
                    var geometry = new PathGeometry();
                    var figure = new PathFigure { StartPoint = startPoint };
                    geometry.Figures.Add(figure);
                    path.Data = geometry;
                    currentElement = path;
                    break;
            }

            if (currentElement != null)
            {
                canvas.Children.Add(currentElement);
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || currentElement == null)
                return;

            var currentPoint = e.GetPosition(canvas);

            switch (currentMode)
            {
                case DrawingMode.Arrow:
                    var arrow = currentElement as ArrowLine;
                    if (arrow != null)
                    {
                        arrow.EndPoint = currentPoint;
                    }
                    break;

                case DrawingMode.Rectangle:
                    var rect = currentElement as Rectangle;
                    if (rect != null)
                    {
                        var x = Math.Min(startPoint.X, currentPoint.X);
                        var y = Math.Min(startPoint.Y, currentPoint.Y);
                        var width = Math.Abs(currentPoint.X - startPoint.X);
                        var height = Math.Abs(currentPoint.Y - startPoint.Y);

                        Canvas.SetLeft(rect, x);
                        Canvas.SetTop(rect, y);
                        rect.Width = width;
                        rect.Height = height;
                    }
                    break;

                case DrawingMode.Pen:
                    var path = currentElement as Path;
                    if (path != null)
                    {
                        var geometry = path.Data as PathGeometry;
                        var figure = geometry.Figures[0];
                        var segment = new LineSegment(currentPoint, true);
                        figure.Segments.Add(segment);
                    }
                    break;
            }
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (currentMode == DrawingMode.Text && currentTextBox != null)
            {
                currentTextBox.Focus();
            }
            currentElement = null;
        }

        private void BtnDone_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    DialogResult = false;
                    Close();
                    break;

                case Key.Enter:
                    var textBox = Keyboard.FocusedElement as TextBox;
                    if (textBox == null)
                    {
                        DialogResult = true;
                        Close();
                    }
                    break;

                case Key.A: btnArrow.IsChecked = true; break;
                case Key.R: btnRectangle.IsChecked = true; break;
                case Key.T: btnText.IsChecked = true; break;
                case Key.P: btnPen.IsChecked = true; break;
            }
        }
    }

    public enum DrawingMode
    {
        Arrow,
        Rectangle,
        Text,
        Pen
    }
} 