
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
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
            this.Size = new Size(80, 80); // Увеличиваем размер формы
            this.BackColor = Color.Magenta;
            this.TransparencyKey = Color.Magenta;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(20, 20); // Позиция в левом верхнем углу
            this.ShowInTaskbar = false;
        }

        private void SetupSettingsButton()
        {
            settingsButton = new Button();
            settingsButton.Size = new Size(80, 80); // Увеличиваем размер кнопки
            settingsButton.BackColor = Color.FromArgb(255, 40, 40, 40); // Полностью непрозрачный
            settingsButton.ForeColor = Color.White;
            settingsButton.Text = "⚙️";
            settingsButton.Font = new Font("Arial", 24); // Увеличиваем размер шрифта
            settingsButton.FlatStyle = FlatStyle.Flat;
            settingsButton.FlatAppearance.BorderSize = 0;
            settingsButton.Cursor = Cursors.Hand;

            settingsButton.Click += SettingsButton_Click;
            this.Controls.Add(settingsButton);
        }

        private void SettingsButton_Click(object sender, EventArgs e)
        {
            ShowSettingsMenu();
        }

        private void ShowSettingsMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Font = new Font("Arial", 10);
            menu.ShowImageMargin = false;

            // Цвета
            var colors = new[] {
                new { Name = "Красный", Color = Color.Red },
                new { Name = "Синий", Color = Color.Blue },
                new { Name = "Зеленый", Color = Color.Green },
                new { Name = "Желтый", Color = Color.Yellow },
                new { Name = "Белый", Color = Color.White },
                new { Name = "Черный", Color = Color.Black },
                new { Name = "Розовый", Color = Color.Pink },
                new { Name = "Фиолетовый", Color = Color.Purple }
            };

            foreach (var color in colors)
            {
                var item = menu.Items.Add(color.Name);
                item.Click += (s, e) => {
                    OnColorChanged?.Invoke(color.Color);
                    menu.Close();
                };
            }

            menu.Items.Add(new ToolStripSeparator());

            // Размеры кисти
            var sizes = new[] { 2, 5, 8, 12, 16, 20 };
            foreach (var size in sizes)
            {
                var item = menu.Items.Add($"Кисть {size}px");
                item.Click += (s, e) => {
                    OnThicknessChanged?.Invoke(size);
                    menu.Close();
                };
            }

            menu.Items.Add(new ToolStripSeparator());

            // Действия
            menu.Items.Add("Очистить экран", null, (s, e) => {
                OnClearScreen?.Invoke();
            });

            menu.Items.Add("Выйти", null, (s, e) => {
                OnExit?.Invoke();
            });

            // Показываем меню рядом с кнопкой
            menu.Show(settingsButton, new Point(0, settingsButton.Height));
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Не рисуем фон - он прозрачный
        }
    }
}