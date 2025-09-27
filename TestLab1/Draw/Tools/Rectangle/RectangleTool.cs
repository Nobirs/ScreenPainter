using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestLab1.Draw.Tools.Rectangle
{
    public class RectangleTool : DrawingTool
    {
        RectanglePath _currentRectangle;
        public override void OnMouseDown(Point p)
        {
            _currentRectangle = new RectanglePath(color, thickness);
            _currentRectangle.OnDown(p);
        }
        public override IDrawable OnMouseMove(Point p)
        {
            if (_currentRectangle == null) return null;
            _currentRectangle.OnMove(p);
            return _currentRectangle;
        }
        public override IDrawable OnMouseUp(Point p)
        {
            if (_currentRectangle == null) return null;
            _currentRectangle.OnUp(p);
            var finished = _currentRectangle;
            _currentRectangle = null;
            return finished;
        }
        public override bool CheckPointsDistance(Point newPoint)
        {
            if (newPoint.IsEmpty || _currentRectangle == null) return false;
            Point last = _currentRectangle.GetLastPoint();
            if (last.IsEmpty) return true; 
            int dx = newPoint.X - last.X;
            int dy = newPoint.Y - last.Y;
            return dx * dx + dy * dy > MinPointDistanceSq;
        }
        public override void DrawPreview(Graphics g)
        {
            _currentRectangle?.Draw(g);
        }
        public RectanglePath GetCurrentRectangle() => _currentRectangle;
    }
}
