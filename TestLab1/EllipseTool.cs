using System;
using System.Drawing;

namespace TestLab1
{
    public class EllipseTool : DrawingTool
    {
        private EllipsePath _currentEllipse;
        private Bitmap _canvasCopy;
        private Bitmap _canvasOrigin;
        private Rectangle _previousBounds;

        public override void SetCanvasSnapshot(Bitmap bmp)
        {
            _canvasOrigin = bmp;
            _canvasCopy?.Dispose();
            _canvasCopy = (Bitmap)_canvasOrigin.Clone();
        }

        public override void OnMouseDown(Point p, Graphics g)
        {
            _currentEllipse = new EllipsePath(color, thickness);
            _currentEllipse.SetStartPoint(p);

            _currentEllipse.DrawOnDown(g);
            _previousBounds = Rectangle.Empty;
        }

        public override IDrawable OnMouseMove(Point p, Graphics g)
        {
            if (_currentEllipse == null) return null;

            if (!_previousBounds.IsEmpty)
            {
                g.DrawImage(_canvasCopy, _previousBounds, _previousBounds, GraphicsUnit.Pixel);
            }
            _currentEllipse.SetEndPoint(p);

            // Вычисляем новые границы с учетом толщины пера
            var bounds = CalculateBoundsWithThickness();
            _previousBounds = bounds;

            _currentEllipse.DrawOnMove(g);

            return _currentEllipse;
        }

        public override IDrawable OnMouseUp(Point p, Graphics g)
        {
            if (_currentEllipse == null) return null;


            // Восстанавливаем холст и рисуем финальную версию
            if (!_previousBounds.IsEmpty)
            {
                g.DrawImage(_canvasCopy, _previousBounds, _previousBounds, GraphicsUnit.Pixel);
            }

            _currentEllipse.SetEndPoint(p);
            _currentEllipse.DrawOnUp(g);

            var finished = _currentEllipse;
            _currentEllipse = null;
            _previousBounds = Rectangle.Empty;

            _canvasCopy?.Dispose();
            _canvasCopy = null;

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

        private Rectangle CalculateBoundsWithThickness()
        {
            var start = _currentEllipse.StartPoint;
            var end = _currentEllipse.EndPoint;

            int x = Math.Min(start.X, end.X);
            int y = Math.Min(start.Y, end.Y);
            int width = Math.Abs(start.X - end.X);
            int height = Math.Abs(start.Y - end.Y);

            // Учитываем толщину пера
            int inflate = (int)Math.Ceiling(thickness / 2f);
            return new Rectangle(x - inflate, y - inflate,
                               width + 2 * inflate, height + 2 * inflate);
        }

        public override void DrawPreview(Graphics g)
        {
            _currentEllipse?.Draw(g);
        }

        public EllipsePath GetCurrentEllipse() => _currentEllipse;
    }
}
