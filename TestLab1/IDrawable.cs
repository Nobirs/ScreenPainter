using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestLab1
{
    public interface IDrawable
    {
        void Draw(System.Drawing.Graphics g);
        Point GetLastPoint();
        void OnMove(Point p);
        void OnDown(Point p);
        void OnUp(Point p);
        Rectangle GetBounds();
    }
}
