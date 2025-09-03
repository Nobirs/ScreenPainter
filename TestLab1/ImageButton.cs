using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestLab1
{
    public class ImageButton : Button
    {
        public ImageButton()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer, true);

            BackColor = Color.Transparent;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            TabStop = false;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // отключаем заливку прямоугольника
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var path = new GraphicsPath();
            path.AddEllipse(ClientRectangle);
            this.Region = new Region(path);

            base.OnPaint(pevent);
        }
    }
}
