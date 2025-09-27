using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Timers;

namespace TestLab1
{
    public partial class LoadingForm : Form
    {
        private System.Windows.Forms.Timer animationTimer;
        private System.Timers.Timer closeTimer;
        private float angle = 0;
        private const float RotationSpeed = 5f;
        private const int DisplayTime = 30000;

        // Система частиц для комет
        private List<Particle> particles = new List<Particle>();
        private Random random = new Random();
        private const int MaxParticles = 50;
        private const int ParticleSpawnRate = 2; // Частиц за кадр

        public LoadingForm()
        {
            InitializeComponent();
            SetupForm();
            SetupTimers();
        }

        private void SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(800, 800); // Увеличим размер для эффекта
            this.BackColor = Color.Black;
            this.TransparencyKey = Color.Black;
            this.DoubleBuffered = true;
        }

        private void SetupTimers()
        {
            animationTimer = new System.Windows.Forms.Timer();
            animationTimer.Interval = 16;
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();

            closeTimer = new System.Timers.Timer(DisplayTime);
            closeTimer.Elapsed += CloseTimer_Elapsed;
            closeTimer.AutoReset = false;
            closeTimer.Start();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            // Вращение серпа
            angle += RotationSpeed;
            if (angle >= 360) angle = 0;

            // Обновление частиц
            UpdateParticles();

            // Создание новых частиц
            SpawnParticles();

            this.Invalidate();
        }

        private void SpawnParticles()
        {
            if (particles.Count < MaxParticles)
            {
                var center = new PointF(this.Width / 2, this.Height / 2);

                for (int i = 0; i < ParticleSpawnRate; i++)
                {
                    // Случайный угол вылета от серпа
                    float spawnAngle = angle + random.Next(0, 360);
                    float distance = 50 + random.Next(20); // Расстояние от центра

                    var particle = new Particle
                    {
                        Position = new PointF(
                            center.X + (float)Math.Cos(spawnAngle * Math.PI / 180) * distance,
                            center.Y + (float)Math.Sin(spawnAngle * Math.PI / 180) * distance
                        ),
                        Velocity = new PointF(
                            (float)(Math.Cos(spawnAngle * Math.PI / 180) * (3 + random.NextDouble() * 3)),
                            (float)(Math.Sin(spawnAngle * Math.PI / 180) * (3 + random.NextDouble() * 3))
                        ),
                        Life = 1.0f,
                        Size = 2 + random.Next(4),
                        Color = ColorFromHue((spawnAngle + 180) % 360) // Красивые цвета
                    };

                    particles.Add(particle);
                }
            }
        }

        private void UpdateParticles()
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var particle = particles[i];

                // Обновление позиции
                particle.Position = new PointF(
                    particle.Position.X + particle.Velocity.X,
                    particle.Position.Y + particle.Velocity.Y
                );

                // Уменьшение жизни
                particle.Life -= 0.02f;

                // Уменьшение размера
                particle.Size *= 1.08f;

                // Удаление мертвых частиц
                if (particle.Life <= 0 || particle.Size < 0.5f ||
                    particle.Position.X < -100 || particle.Position.X > this.Width + 100 ||
                    particle.Position.Y < -100 || particle.Position.Y > this.Height + 100)
                {
                    particles.RemoveAt(i);
                }
                else
                {
                    particles[i] = particle;
                }
            }
        }

        private Color ColorFromHue(float hue)
        {
            // Создание красивых цветов на основе hue
            return ColorFromHsla(hue / 360f, 0.8f, 0.7f, 1.0f);
        }

        private Color ColorFromHsla(float h, float s, float l, float a)
        {
            float r, g, b;

            if (s == 0)
            {
                r = g = b = l;
            }
            else
            {
                float q = l < 0.5f ? l * (1 + s) : l + s - l * s;
                float p = 2 * l - q;
                r = HueToRgb(p, q, h + 1f / 3f);
                g = HueToRgb(p, q, h);
                b = HueToRgb(p, q, h - 1f / 3f);
            }

            return Color.FromArgb((int)(a * 255), (int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        private float HueToRgb(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }

        private void CloseTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.Invoke(new Action(() =>
            {
                animationTimer.Stop();
                closeTimer.Stop();
                closeTimer.Dispose();
                this.DialogResult = DialogResult.OK;
                this.Close();
            }));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Рисуем частицы (кометы)
            DrawParticles(g);

            // Рисуем серп
            var center = new PointF(this.Width / 2, this.Height / 2);
            float radius = Math.Min(this.Width, this.Height) / 3 - 10;

            using (var matrix = new Matrix())
            {
                matrix.RotateAt(angle, center);
                g.Transform = matrix;
                DrawCrescent(g, center, radius);
            }
        }

        private void DrawParticles(Graphics g)
        {
            foreach (var particle in particles)
            {
                using (var brush = new SolidBrush(Color.FromArgb(
                    (int)(particle.Life * 255), particle.Color)))
                {
                    float size = particle.Size;
                    g.FillEllipse(brush,
                        particle.Position.X - size / 2,
                        particle.Position.Y - size / 2,
                        size, size);

                    // Хвост кометы
                    if (size > 2)
                    {
                        using (var pen = new Pen(Color.FromArgb(
                            (int)(particle.Life * 128), particle.Color), size / 3))
                        {
                            PointF tailStart = new PointF(
                                particle.Position.X - particle.Velocity.X * 0.5f,
                                particle.Position.Y - particle.Velocity.Y * 0.5f
                            );
                            g.DrawLine(pen, tailStart, particle.Position);
                        }
                    }
                }
            }
        }

        private void DrawCrescent(Graphics g, PointF center, float radius)
        {
            using (var path = new GraphicsPath())
            {
                // Внешний круг
                path.AddEllipse(center.X - radius, center.Y - radius,
                               radius * 2, radius * 2);

                // Внутренний круг для выреза
                var smallRadius = radius * 0.7f;
                var offset = radius * 0.6f;
                path.AddEllipse(center.X - radius + offset, center.Y - smallRadius,
                               smallRadius * 2, smallRadius * 2);

                // Градиентная заливка для красоты
                using (var brush = new LinearGradientBrush(
                    new PointF(center.X - radius, center.Y),
                    new PointF(center.X + radius, center.Y),
                    ColorFromHue(angle % 360),
                    ColorFromHue((angle + 60) % 360)))
                {
                    g.FillPath(brush, path);
                }

                // Обводка
                using (var pen = new Pen(Color.White, 2))
                {
                    g.DrawPath(pen, path);
                }
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            animationTimer.Stop();
            closeTimer.Stop();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            animationTimer?.Dispose();
            closeTimer?.Dispose();
        }

        // Структура для частиц
        private struct Particle
        {
            public PointF Position;
            public PointF Velocity;
            public float Life;
            public float Size;
            public Color Color;
        }
    }
}