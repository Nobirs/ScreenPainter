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
        void DrawOnMove(System.Drawing.Graphics g);
        void DrawOnDown(System.Drawing.Graphics g);
        void DrawOnUp(System.Drawing.Graphics g);
        Rectangle GetBounds();
    }
}
