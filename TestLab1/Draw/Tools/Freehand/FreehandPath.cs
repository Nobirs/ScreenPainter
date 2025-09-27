using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestLab1.Draw;

namespace TestLab1.Draw.Tools.Freehand
{
    public class FreehandPath : IDrawable
    {
        public List<Point> Points { get; } = new List<Point>();
        private int lastPointedIndex = 0;
        public Color Color { get; }
        public int Thickness { get; }

        public FreehandPath(Color color, int thickness)
        {
            Color = color;
            Thickness = thickness;
        }

        public void AddPoint(Point p) => Points.Add(p);

        public Point GetLastPoint()
        {
            if (Points.Count > 0)
            {
                return Points[Points.Count - 1];
            }
            return Point.Empty;
        }

        public void Draw(Graphics g)
        {
            if (Points.Count > 1)
            {
                using var pen = new Pen(Color, Thickness)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round,
                    LineJoin = System.Drawing.Drawing2D.LineJoin.Round
                };
                g.DrawLines(pen, Points.ToArray());
            } else if (Points.Count == 1)
            {
                using var b = new SolidBrush(Color);
                g.FillEllipse(b, Points[0].X - Thickness / 2, Points[0].Y - Thickness / 2, Thickness, Thickness);
            }
        }

        public void OnDown(Point p)
        {
            Points.Add(p);
        }

        public void OnMove(Point p)
        {
            Points.Add(p);
        }

        public void OnUp(Point p)
        {
            Points.Add(p);
        }

        public System.Drawing.Rectangle GetBounds()
        {
            if (Points.Count == 0) return System.Drawing.Rectangle.Empty;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var p in Points)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            return System.Drawing.Rectangle.FromLTRB(minX - Thickness, minY - Thickness, maxX + Thickness, maxY + Thickness);
        }
    }
}
