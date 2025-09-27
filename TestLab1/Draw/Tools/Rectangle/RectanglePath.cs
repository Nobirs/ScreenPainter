using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestLab1.Draw.Tools.Ellipse;

namespace TestLab1.Draw.Tools.Rectangle
{
    public class RectanglePath : IDrawable
    {
        public Point StartPoint { get; private set; } = Point.Empty;
        public Point EndPoint { get; private set; } = Point.Empty;
        public Color Color { get; }
        public int Thickness { get; }

        public RectanglePath(Color color, int thickness)
        {
            Color = color;
            Thickness = thickness;
        }

        public Point GetLastPoint()
        {
            return EndPoint;
        }


        public void Draw(Graphics g)
        {
            if (StartPoint != Point.Empty && EndPoint != Point.Empty && StartPoint != EndPoint)
            {
                using var pen = new Pen(Color, Thickness);
                var rect = GetDrawingRectangle();
                g.DrawRectangle(pen, rect);
            }
        }

        public void OnDown(Point p)
        {
            StartPoint = p;
        }

        public void OnMove(Point p)
        {
            EndPoint = p;
        }

        public void OnUp(Point p)
        {
            EndPoint = p;
        }

        protected System.Drawing.Rectangle GetDrawingRectangle()
        {
            int x = Math.Min(StartPoint.X, EndPoint.X);
            int y = Math.Min(StartPoint.Y, EndPoint.Y);
            int width = Math.Abs(EndPoint.X - StartPoint.X);
            int height = Math.Abs(EndPoint.Y - StartPoint.Y);

            return new System.Drawing.Rectangle(x, y, width, height);
        }

        public System.Drawing.Rectangle GetBounds()
        {
            if (StartPoint == Point.Empty || EndPoint == Point.Empty)
                return System.Drawing.Rectangle.Empty;

            var rect = GetDrawingRectangle();
            return System.Drawing.Rectangle.Inflate(rect, Thickness, Thickness);
        }
    }
}
