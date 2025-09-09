using System;
using System.Drawing;

namespace TestLab1
{
    public class EllipseTool : DrawingTool
    {
        EllipsePath _currentEllipse;
        public override void OnMouseDown(Point p)
        {
            _currentEllipse = new EllipsePath(color, thickness);
            _currentEllipse.OnDown(p);

        }

        public override IDrawable OnMouseMove(Point p)
        {
            if (_currentEllipse == null) return null;
            _currentEllipse.OnMove(p);
            return _currentEllipse;
        }

        public override IDrawable OnMouseUp(Point p)
        {
            if (_currentEllipse == null) return null;

            _currentEllipse.OnUp(p);
            var finished = _currentEllipse;
            _currentEllipse = null;
            return finished;
        }

        public override bool CheckPointsDistance(Point newPoint)
        {
            if (newPoint.IsEmpty || _currentEllipse == null) return false;
            Point last = _currentEllipse.GetLastPoint();
            if (last.IsEmpty) return true; 
            int dx = newPoint.X - last.X;
            int dy = newPoint.Y - last.Y;
            return dx * dx + dy * dy > MinPointDistanceSq;
        }
        public override void DrawPreview(Graphics g)
        {
            _currentEllipse?.Draw(g);
        }

        public EllipsePath GetCurrentEllipse() => _currentEllipse;
    }
}
