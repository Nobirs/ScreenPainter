using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timer = System.Windows.Forms.Timer;

namespace TestLab1
{
    public class ImageButton : Button
    {

        private float gradientScale = 1.0f;      // текущий масштаб
        private float targetScale = 1.0f;        // целевой масштаб
        private int gradientAlpha = 180;         // текущая яркость (0-255)
        private int targetAlpha = 180;           // целевая яркость
        private readonly Timer animTimer;        // таймер анимации
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

            this.MouseEnter += (s, e) =>
            {
                targetScale = 1.2f;
                targetAlpha = 240; // ярче
                animTimer.Start();
            };

            this.MouseLeave += (s, e) =>
            {
                targetScale = 1.0f;
                targetAlpha = 180; // нормальное состояние
                animTimer.Start();
            };

            animTimer = new Timer { Interval = 15 }; // ~60 FPS
            animTimer.Tick += (s, e) => Animate();
        }

        private void Animate()
        {
            float speed = 0.1f;

            // масштаб
            if (Math.Abs(gradientScale - targetScale) < 0.01f)
                gradientScale = targetScale;
            else
                gradientScale += (targetScale - gradientScale) * speed;

            // яркость
            if (Math.Abs(gradientAlpha - targetAlpha) < 2)
                gradientAlpha = targetAlpha;
            else
                gradientAlpha += (int)((targetAlpha - gradientAlpha) * speed);

            // стоп, если оба параметра достигли цели
            if (Math.Abs(gradientScale - targetScale) < 0.01f &&
                Math.Abs(gradientAlpha - targetAlpha) < 2)
                animTimer.Stop();

            Invalidate();
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


            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            int w = Width, h = Height;

            // градиент с анимацией масштаба и яркости
            int gw = (int)(w * gradientScale);
            int gh = (int)(h * gradientScale);

            // круглый градиент
            using (var gp = new GraphicsPath())
            {
                gp.AddEllipse((w - gw) / 2, (h - gh) / 2, gw - 1, gh - 1);
                using (var pgb = new PathGradientBrush(gp))
                {
                    pgb.CenterColor = Color.FromArgb(gradientAlpha, 255, 255, 255);
                    pgb.SurroundColors = new[] { Color.FromArgb(20, 255, 255, 255) };
                    g.FillEllipse(pgb, (w - gw) / 2, (h - gh) / 2, gw, gh);
                }
            }

            // иконка
            if (this.Image != null)
            {
                int target = (int)(w * 0.86);
                var rect = new Rectangle((w - target) / 2, (h - target) / 2, target, target);
                g.DrawImage(this.Image, rect);
            }
            else
            {
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var f = new Font("Segoe UI Emoji", 28, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var br = new SolidBrush(Color.FromArgb(220, 40, 40, 40)))
                {
                    g.DrawString("⚙", f, br, new RectangleF(0, 0, w, h), sf);
                }
            }
        }
    }
}
