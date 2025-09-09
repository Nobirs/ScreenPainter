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

        public override void OnMouseDown(Point p)
        {
            _currentPath = new FreehandPath(color, thickness);
            _currentPath.OnDown(p);
        }

        public override IDrawable OnMouseMove(Point p)
        {
            _currentPath?.OnMove(p);
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

        public override IDrawable OnMouseUp(Point p)
        {
            if (_currentPath == null) return null;
            _currentPath.OnUp(p);
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
