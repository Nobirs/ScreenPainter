using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestLab1
{
    public class FreehandTool : DrawingTool
    {
        private FreehandPath _currentPath;

        public override void OnMouseDown(Point p, Graphics g)
        {
            _currentPath = new FreehandPath(color, thickness);
            _currentPath.AddPoint(p);
            _currentPath.DrawOnDown(g);
        }

        public override IDrawable OnMouseMove(Point p, Graphics g)
        {
            Point lastPoint = _currentPath.GetLastPoint();
            if (_currentPath != null && !lastPoint.IsEmpty)
            {
                int dx = lastPoint.X - p.X;
                int dy = lastPoint.Y - p.Y;
                if (dx*dx + dy*dy >= MinPointDistanceSq)
                {
                    _currentPath?.AddPoint(p);
                    _currentPath?.DrawOnMove(g);
                }
            }
            return _currentPath;
        }

        public override bool CheckPointsDistance(Point newPoint)
        {
            if (newPoint.IsEmpty || _currentPath.GetLastPoint().IsEmpty)
            {
                return false;
            }
            int dx = newPoint.X - _currentPath.GetLastPoint().X;
            int dy = newPoint.Y - _currentPath.GetLastPoint().Y;
            return dx * dx + dy * dy > MinPointDistanceSq;
        }

        public override IDrawable OnMouseUp(Point p, Graphics g)
        {
            if (_currentPath == null) return null;
            _currentPath.AddPoint(p);
            _currentPath.DrawOnUp(g);
            var finished = _currentPath;
            _currentPath = null;
            return finished;
        }

        public override void DrawPreview(Graphics g)
        {
            _currentPath?.Draw(g);
        }

        public FreehandPath GetCurrentPath()
        {
            return _currentPath;
        }
    }
}
