
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

        private Point mouseDownLocation;
        private bool isDragging = false;
        private const int MAX_NODRAG_DISTANCE = 3;

        private ContextMenuStrip menu;
        private bool isMenuOpened = false;

        public SettingsForm()
        {
            InitializeComponent();
            SetupForm();
            SetupSettingsButton();

        }

        private void SettingsButton_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = false;
                mouseDownLocation = e.Location;
            }
        }

        private void SettingsButton_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int dx = e.X - mouseDownLocation.X;
                int dy = e.Y - mouseDownLocation.Y;

                if (!isDragging && (Math.Abs(dx) > MAX_NODRAG_DISTANCE || Math.Abs(dy) > MAX_NODRAG_DISTANCE))
                    isDragging = true;

                if (isDragging)
                {
                    Point screenPos = settingsButton.PointToScreen(e.Location);
                    this.Location = new Point(screenPos.X - mouseDownLocation.X, 
                        screenPos.Y - mouseDownLocation.Y);
                }
            }
            
        }

        private void SettingsButton_MouseUp(object sender, MouseEventArgs e)
        {
            if (!isDragging && e.Button == MouseButtons.Left)
            {
                if (!isMenuOpened)
                {
                    ShowSettingsMenu();
                    isMenuOpened = true;
                } else
                {
                    closeSettingsMenu();
                    isMenuOpened = false;
                }
            }
        }

        private void SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ClientSize = new Size(128, 128);

            this.AllowTransparency = true;
            this.TransparencyKey = Color.Magenta;
            this.BackColor = Color.Magenta;

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

            // Load Icon
            try
            {
                using (var ms = new System.IO.MemoryStream(Properties.Resources.settings128))
                {
                    settingsButton.Image = Image.FromStream(ms);
                }
            }
            catch
            {
                settingsButton.Image = null;
            }

            this.Controls.Add(settingsButton);

            settingsButton.MouseDown += SettingsButton_MouseDown;
            settingsButton.MouseMove += SettingsButton_MouseMove;
            settingsButton.MouseUp += SettingsButton_MouseUp;
        }

        private void createSettingsMenu()
        {
            this.menu = new ContextMenuStrip();
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

            var colorsMenu = new ToolStripMenuItem("Цвета") { ForeColor = Color.White };
            menu.Items.Add(colorsMenu);
            foreach (var c in colors)
            {
                var it = new ToolStripMenuItem(c.name) { ForeColor = Color.White };
                if (it.Text == "Красный") it.Checked = true;
                it.Click += (s, e) =>
                {
                    OnColorChanged?.Invoke(c.col);
                    it.Checked = true;
                    foreach (ToolStripMenuItem colorItem in colorsMenu.DropDownItems)
                    {
                        if (colorItem.Checked && colorItem.Text != it.Text) colorItem.Checked = false;
                    }

                };
                colorsMenu.DropDownItems.Add(it);
            }

            menu.Items.Add(new ToolStripSeparator());


            var brushMenu = new ToolStripMenuItem("Кисти") { ForeColor = Color.White };
            menu.Items.Add(brushMenu);
            int[] sizes = { 2, 5, 8, 12, 16, 20 };
            foreach (var s in sizes)
            {
                var it = new ToolStripMenuItem($"{s}px") { ForeColor = Color.White };
                if (it.Text == "2px") it.Checked = true;
                it.Click += (ss, ee) =>
                {
                    OnThicknessChanged?.Invoke(s);
                    it.Checked = true;
                    foreach (ToolStripMenuItem brushItem in brushMenu.DropDownItems)
                    {
                        if (brushItem.Checked && brushItem.Text != it.Text) brushItem.Checked = false;
                    }
                };
                brushMenu.DropDownItems.Add(it);
            }

            menu.Items.Add(new ToolStripSeparator());

            mClear.Click += (s, e) => OnClearScreen?.Invoke();
            mExit.Click += (s, e) => OnExit?.Invoke();

            menu.Items.Add(mClear);
            menu.Items.Add(mExit);
        }

        private void ShowSettingsMenu()
        {
            if(this.menu == null)
            {
                createSettingsMenu();
            }

            this.menu.Show(settingsButton, new Point(settingsButton.Width, settingsButton.Height / 2));
        }

        private void closeSettingsMenu()
        {
            this.menu.Close();
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