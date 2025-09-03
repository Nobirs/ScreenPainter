
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TestLab1
{
    // Отдельная форма для кнопки настроек
    public partial class SettingsForm : Form
    {
        public event Action<Color> OnColorChanged;
        public event Action<int> OnThicknessChanged;
        public event Action OnClearScreen;
        public event Action OnExit;

        private Button settingsButton;

        public SettingsForm()
        {
            InitializeComponent();
            SetupForm();
            SetupSettingsButton();
        }

        private void SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ClientSize = new Size(128, 128);

            // Включаем поддержку layered window
            if (Environment.OSVersion.Version.Major >= 6) // Vista и выше
            {
                // Используем DWM для настоящей прозрачности
                this.AllowTransparency = true;
                this.TransparencyKey = Color.Magenta;
                this.BackColor = Color.Magenta;
            }
            else
            {
                // Для старых Windows - fallback
                this.BackColor = Color.Magenta;
                this.TransparencyKey = Color.Magenta;
            }

            // Делаем форму круглой
            SetFormRound();

            this.ShowInTaskbar = false;
        }

        private void SetFormRound()
        {
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(new Rectangle(0, 0, this.Width, this.Height));
                this.Region = new Region(path);
            }
        }

        private void SetupSettingsButton()
        {
            settingsButton = new ImageButton();
            settingsButton.Size = new Size(128, 128);
            settingsButton.Location = new Point(0, 0);
            settingsButton.Cursor = Cursors.Hand;
            settingsButton.Text = "";

            var bmp = new Bitmap(128, 128, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);

                using (var gp = new GraphicsPath())
                {
                    gp.AddEllipse(0, 0, bmp.Width - 1, bmp.Height - 1);
                    using (var pgb = new PathGradientBrush(gp))
                    {
                        pgb.CenterColor = Color.FromArgb(180, 255, 255, 255);
                        pgb.SurroundColors = new Color[] { Color.FromArgb(12, 255, 255, 255) };
                        //pgb.FocusScales = new PointF(0.6f, 0.6f);
                        g.FillEllipse(pgb, 0, 0, bmp.Width, bmp.Height);
                    }
                }

                // небольшой шум 
                var rnd = new Random(1234);
                int noise = (bmp.Width * bmp.Height) / 300;
                for (int i = 0; i < noise; i++)
                {
                    int x = rnd.Next(bmp.Width);
                    int y = rnd.Next(bmp.Height);
                    float dx = x - bmp.Width / 2f;
                    float dy = y - bmp.Height / 2f;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d > bmp.Width * 0.28f && d < bmp.Width * 0.48f)
                    {
                        int a = rnd.Next(10, 45);
                        using (var br = new SolidBrush(Color.FromArgb(a, 255, 255, 255)))
                        {
                            g.FillEllipse(br, x, y, 1, 1);
                        }
                    }
                }

                // подгружаем иконку или fallback
                try
                {
                    using (var ms = new System.IO.MemoryStream(Properties.Resources.settings128))
                    using (var icon = Image.FromStream(ms))
                    {
                        int target = (int)(bmp.Width * 0.86);
                        var rect = new Rectangle(
                            (bmp.Width - target) / 2,
                            (bmp.Height - target) / 2,
                            target,
                            target
                        );
                        g.DrawImage(icon, rect);
                    }
                }
                catch
                {
                    DrawGearFallback(g, bmp.Width, bmp.Height);
                }

            }

            settingsButton.Image = bmp;
            settingsButton.ImageAlign = ContentAlignment.MiddleCenter;
            settingsButton.Click += SettingsButton_Click;

            this.Controls.Add(settingsButton);
        }


        private void DrawGearFallback(Graphics g, int w, int h)
        {
            using (var sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var f = new Font("Segoe UI Emoji", 28, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var br = new SolidBrush(Color.FromArgb(220, 40, 40, 40)))
            {
                g.DrawString("⚙", f, br, new RectangleF(0, 0, w, h), sf);
            }
        }

        private void SettingsButton_Click(object sender, EventArgs e)
        {
            ShowSettingsMenu();
        }

        private void ShowSettingsMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Font = new Font("Segoe UI", 10);
            menu.ShowImageMargin = false;
            menu.Renderer = new ModernMenuRenderer();
            menu.BackColor = Color.Transparent;

            // Режимы (если нужно — можно добавить Checked-индикацию)
            var mFree = new ToolStripMenuItem("Свободно (кисть)") { ForeColor = Color.White };
            var mClear = new ToolStripMenuItem("Очистить экран") { ForeColor = Color.White };
            var mExit = new ToolStripMenuItem("Выйти") { ForeColor = Color.White };

            // Цвета
            var colors = new (string name, Color col)[]
            {
                ("Красный", Color.Red),
                ("Синий", Color.Blue),
                ("Зеленый", Color.Green),
                ("Жёлтый", Color.Yellow),
                ("Белый", Color.White),
                ("Чёрный", Color.Black),
            };

            // Добавляем режимы/функции
            menu.Items.Add(mFree);
            menu.Items.Add(new ToolStripSeparator());

            var colorsHeader = new ToolStripLabel("Цвета") { ForeColor = Color.White, Enabled = false };
            menu.Items.Add(colorsHeader);
            foreach (var c in colors)
            {
                var it = new ToolStripMenuItem(c.name) { ForeColor = Color.White };
                it.Click += (s, e) =>
                {
                    OnColorChanged?.Invoke(c.col);
                };
                menu.Items.Add(it);
            }

            menu.Items.Add(new ToolStripSeparator());

            // Размеры кисти
            int[] sizes = { 2, 5, 8, 12, 16, 20 };
            foreach (var s in sizes)
            {
                var it = new ToolStripMenuItem($"Кисть {s}px") { ForeColor = Color.White };
                it.Click += (ss, ee) => OnThicknessChanged?.Invoke(s);
                menu.Items.Add(it);
            }

            menu.Items.Add(new ToolStripSeparator());

            mClear.Click += (s, e) => OnClearScreen?.Invoke();
            mExit.Click += (s, e) => OnExit?.Invoke();

            menu.Items.Add(mClear);
            menu.Items.Add(mExit);

            // Показываем меню под кнопкой
            menu.Show(settingsButton, new Point(0, settingsButton.Height));
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Не рисуем фон - прозрачный
        }

        // ---------- Modern menu renderer ----------
        private class ModernMenuRenderer : ToolStripProfessionalRenderer
        {
            private readonly Color bg = Color.FromArgb(230, 40, 40, 40);     // основной фон
            private readonly Color hover = Color.FromArgb(255, 60, 60, 60);  // hover
            private readonly Color selected = Color.FromArgb(60, 255, 255, 255); // checked bg
            private readonly int radius = 8;
            private readonly Padding itemPadding = new Padding(12, 6, 12, 6);

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                var r = e.AffectedBounds;
                using (var gp = RoundedRectangle(r, radius))
                using (var br = new SolidBrush(bg))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.FillPath(br, gp);
                }
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                var r = e.AffectedBounds;
                using (var gp = RoundedRectangle(r, radius))
                using (var pen = new Pen(Color.FromArgb(40, 255, 255, 255)))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.DrawPath(pen, gp);
                }
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                var rc = new Rectangle(Point.Empty, e.Item.Size);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                if (e.Item.Selected)
                {
                    using (var br = new SolidBrush(hover))
                        e.Graphics.FillRectangle(br, rc);
                }

                var mi = e.Item as ToolStripMenuItem;
                if (mi != null && mi.Checked)
                {
                    using (var br = new SolidBrush(selected))
                        e.Graphics.FillRectangle(br, rc);
                }
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = Color.White;
                e.TextFont = new Font("Segoe UI", 10);
                base.OnRenderItemText(e);
            }

            protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
            {
                // мы не используем изображения в меню
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                var rc = new Rectangle(8, e.Item.Bounds.Top + 2, e.Item.Owner.Width - 16, 1);
                using (var pen = new Pen(Color.FromArgb(50, 255, 255, 255)))
                {
                    e.Graphics.DrawLine(pen, rc.Left, rc.Top, rc.Right, rc.Top);
                }
            }

            private GraphicsPath RoundedRectangle(Rectangle r, int d)
            {
                var gp = new GraphicsPath();
                int r2 = d * 2;
                gp.AddArc(r.Left, r.Top, r2, r2, 180, 90);
                gp.AddArc(r.Right - r2, r.Top, r2, r2, 270, 90);
                gp.AddArc(r.Right - r2, r.Bottom - r2, r2, r2, 0, 90);
                gp.AddArc(r.Left, r.Bottom - r2, r2, r2, 90, 90);
                gp.CloseFigure();
                return gp;
            }
        }
    }

}