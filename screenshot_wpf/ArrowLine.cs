using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ScreenshotTool
{
    public class ArrowLine : Shape
    {
        public static readonly DependencyProperty StartPointProperty =
            DependencyProperty.Register("StartPoint", typeof(Point), typeof(ArrowLine),
                new FrameworkPropertyMetadata(new Point(), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty EndPointProperty =
            DependencyProperty.Register("EndPoint", typeof(Point), typeof(ArrowLine),
                new FrameworkPropertyMetadata(new Point(), FrameworkPropertyMetadataOptions.AffectsRender));

        public Point StartPoint
        {
            get { return (Point)GetValue(StartPointProperty); }
            set { SetValue(StartPointProperty, value); }
        }

        public Point EndPoint
        {
            get { return (Point)GetValue(EndPointProperty); }
            set { SetValue(EndPointProperty, value); }
        }

        protected override Geometry DefiningGeometry
        {
            get
            {
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    DrawArrow(context);
                }
                geometry.Freeze();
                return geometry;
            }
        }

        private void DrawArrow(StreamGeometryContext context)
        {
            var vector = EndPoint - StartPoint;
            var length = vector.Length;
            if (length < 0.001) return;

            var direction = vector / length;
            var arrowSize = Math.Min(10.0, length / 3);
            var arrowAngle = Math.PI / 6;

            var arrowPoint1 = EndPoint - direction * arrowSize;
            var perpendicular = new Vector(-direction.Y, direction.X);
            var arrowCorner1 = arrowPoint1 + perpendicular * arrowSize * Math.Tan(arrowAngle);
            var arrowCorner2 = arrowPoint1 - perpendicular * arrowSize * Math.Tan(arrowAngle);

            context.BeginFigure(StartPoint, true, false);
            context.LineTo(EndPoint, true, true);

            context.BeginFigure(EndPoint, true, false);
            context.LineTo(arrowCorner1, true, true);

            context.BeginFigure(EndPoint, true, false);
            context.LineTo(arrowCorner2, true, true);
        }
    }
} 