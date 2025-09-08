using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TestLab1
{
    public abstract class DrawingTool
    {
        public Color color { get; set; } = Color.Red;
        public int thickness { get; set; } = 3;
        public int MinPointDistanceSq { get; } = 36;

        public virtual void SetCanvasSnapshot(Bitmap bmp) { }
        public abstract void OnMouseDown(Point p, Graphics g);
        public abstract IDrawable OnMouseMove(Point p, Graphics g);
        public abstract IDrawable OnMouseUp(Point p, Graphics g);
        public abstract void DrawPreview(System.Drawing.Graphics g);
        public abstract bool CheckPointsDistance(Point newPoint);
    }
}
