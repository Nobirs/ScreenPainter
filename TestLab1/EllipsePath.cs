using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestLab1
{
    public class EllipsePath : IDrawable
    {
        public Point StartPoint { get; private set; } = Point.Empty;
        public Point EndPoint { get; private set; } = Point.Empty;
        public Color Color { get; }
        public int Thickness { get; }
        public bool IsDrawing { get; private set; } = false;

        public EllipsePath(Color color, int thickness)
        {
            Color = color;
            Thickness = thickness;
        }

        public void SetStartPoint(Point p)
        {
            StartPoint = p;
            EndPoint = p;
            IsDrawing = true;
        }

        public void SetEndPoint(Point p)
        {
            if (IsDrawing)
            {
                EndPoint = p;
            }
        }

        public void CompleteDrawing()
        {
            IsDrawing = false;
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
                g.DrawEllipse(pen, rect);
            }
        }

        public void DrawOnDown(Graphics g)
        {
            // DO NOTHING
        }

        public void DrawOnMove(Graphics g)
        {
            if (IsDrawing && StartPoint != Point.Empty && EndPoint != Point.Empty && StartPoint != EndPoint)
            {
                using var pen = new Pen(Color, Thickness)
                {
                    DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
                };
                var rect = GetDrawingRectangle();
                g.DrawEllipse(pen, rect);
            }
        }

        public void DrawOnUp(Graphics g)
        {
            if (EndPoint != Point.Empty)
            {
                using var pen = new Pen(Color, Thickness);
                var rect = GetDrawingRectangle();
                g.DrawEllipse(pen, rect);

                CompleteDrawing();
            }
        }

        private Rectangle GetDrawingRectangle()
        {
            int x = Math.Min(StartPoint.X, EndPoint.X);
            int y = Math.Min(StartPoint.Y, EndPoint.Y);
            int width = Math.Abs(EndPoint.X - StartPoint.X);
            int height = Math.Abs(EndPoint.Y - StartPoint.Y);

            return new Rectangle(x, y, width, height);
        }

        public Rectangle GetBounds()
        {
            if (StartPoint == Point.Empty || EndPoint == Point.Empty)
                return Rectangle.Empty;

            var rect = GetDrawingRectangle();
            return Rectangle.Inflate(rect, Thickness, Thickness);
        }
    }
}