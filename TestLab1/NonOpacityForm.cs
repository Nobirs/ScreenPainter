using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Drawing.Imaging;

namespace TestLab1
{
    public class DrawingPath
    {
        public List<Point> Points { get; set; } = new List<Point>();
        public Color Color { get; set; }
        public int Thickness { get; set; }
    }

    public partial class NonOpacityForm : Form
    {
        private List<DrawingPath> allPaths = new List<DrawingPath>();
        private DrawingPath currentPath = null;
        private bool isDrawing = false;
        private Color currentColor = Color.Red;
        private int currentBrushSize = 3;
        private bool ctrlPressed = false;

        private Bitmap layerBitmap;
        private Graphics layerGraphics;
        private System.Windows.Forms.Timer renderTimer;
        private volatile bool needsUpdate = false;
        private Point? lastDrawPoint = null;
        private const int RenderIntervalMs = 24; //  FPS
        private const int MinPointDistanceSq = 4; // порог 2px (2*2)

        private SettingsForm settingsForm;

        // ---------- WinAPI / GDI ----------
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd,
            IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pprSrc, int crKey,
            ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr ho);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int ULW_ALPHA = 0x00000002;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x, y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx, cy; }

        [StructLayout(LayoutKind.Sequential)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        // Low-level mouse hook bits (как у вас раньше)
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc callback, IntPtr hInstance, uint threadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hInstance);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr idHook, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT point);

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int VK_CONTROL = 0x11;

        private LowLevelMouseProc _mouseProc;
        private IntPtr _mouseHook = IntPtr.Zero;

        public NonOpacityForm()
        {
            InitializeComponent();
            SetupForm();
            SetupSettingsForm();
            SetupKeyboardEvents();
            SetupMouseHook();

            // Первичная отрисовка слоя (пустой)
            RedrawLayer();
        }

        private void SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;

            this.SetStyle(ControlStyles.UserPaint |
                          ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();

            // Сделаем layered (для UpdateLayeredWindow) и click-through по умолчанию
            int ex = GetWindowLong(this.Handle, GWL_EXSTYLE);
            ex |= WS_EX_LAYERED;
            ex |= WS_EX_TRANSPARENT;
            SetWindowLong(this.Handle, GWL_EXSTYLE, ex);

            // Создаём offscreen bitmap + graphics
            CreateLayerResources(this.ClientSize);

            // Таймер для батчевого обновления окна
            renderTimer = new System.Windows.Forms.Timer();
            renderTimer.Interval = RenderIntervalMs;
            renderTimer.Tick += (s, e) =>
            {
                if (needsUpdate)
                {
                    // UpdateLayeredWindow heavy call only when needed and on timer
                    RedrawLayerImmediate();
                    needsUpdate = false;
                }
            };
            renderTimer.Start();
        }

        private void CreateLayerResources(Size size)
        {
            // освободить старые если есть
            layerGraphics?.Dispose();
            layerBitmap?.Dispose();

            if (size.Width <= 0 || size.Height <= 0) return;

            layerBitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
            layerGraphics = Graphics.FromImage(layerBitmap);
            layerGraphics.SmoothingMode = SmoothingMode.AntiAlias;
            layerGraphics.Clear(Color.Transparent);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (this.ClientSize.Width > 0 && this.ClientSize.Height > 0)
            {
                CreateLayerResources(this.ClientSize);
                // пометим, что нужно перерисовать всё (перенести все saved paths)
                // нарисуем заново все сохранённые пути в layerGraphics:
                if (layerGraphics != null)
                {
                    layerGraphics.Clear(Color.Transparent);
                    RenderDrawing(layerGraphics); // отрисует все allPaths + currentPath если нужно
                    needsUpdate = true;
                }
            }
        }

        private void SetupSettingsForm()
        {
            settingsForm = new SettingsForm();
            settingsForm.OnColorChanged += (color) => currentColor = color;
            settingsForm.OnThicknessChanged += (thickness) => currentBrushSize = thickness;
            settingsForm.OnClearScreen += () =>
            {
                allPaths.Clear();
                RedrawLayer();
            };
            settingsForm.OnExit += () => this.Close();

            settingsForm.TopMost = true;
            settingsForm.Show();
        }

        private void SetupKeyboardEvents()
        {
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;
            this.KeyUp += MainForm_KeyUp;
        }

        private void SetupMouseHook()
        {
            _mouseProc = MouseHookProc;
            var module = Process.GetCurrentProcess().MainModule;
            _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, LoadLibrary(module.ModuleName), 0);
        }

        private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && this.Visible)
            {
                int msg = wParam.ToInt32();
                MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

                POINT cursorPos = new POINT { x = hookStruct.pt.x, y = hookStruct.pt.y };
                Point screenPoint = new Point(cursorPos.x, cursorPos.y);

                bool isOverSettingsForm = settingsForm != null && settingsForm.Bounds.Contains(screenPoint);
                if (isOverSettingsForm)
                {
                    return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
                }

                POINT clientPoint = cursorPos;
                ScreenToClient(this.Handle, ref clientPoint);
                Point formPoint = new Point(clientPoint.x, clientPoint.y);

                ctrlPressed = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;

                switch (msg)
                {
                    case WM_LBUTTONDOWN:
                        if (ctrlPressed)
                        {
                            this.BeginInvoke((Action)(() =>
                            {
                                isDrawing = true;
                                currentPath = new DrawingPath { Color = currentColor, Thickness = currentBrushSize };
                                currentPath.Points.Add(formPoint);

                                // стартовая точка для инкрементального рисования
                                lastDrawPoint = formPoint;
                                // рисуем маленькую точку сразу в layerGraphics
                                if (layerGraphics != null)
                                {
                                    using (var b = new SolidBrush(currentPath.Color))
                                    {
                                        layerGraphics.FillEllipse(b, formPoint.X - currentPath.Thickness / 2, formPoint.Y - currentPath.Thickness / 2, currentPath.Thickness, currentPath.Thickness);
                                    }
                                    needsUpdate = true;
                                }
                            }));
                        }
                        break;

                    case WM_MOUSEMOVE:
                        if (isDrawing && ctrlPressed)
                        {
                            this.BeginInvoke((Action)(() =>
                            {
                                if (currentPath != null)
                                {
                                    // декимация — добавим точку только если достаточно далеко от предыдущей
                                    Point prev = currentPath.Points.Count > 0 ? currentPath.Points[currentPath.Points.Count - 1] : formPoint;
                                    int dx = formPoint.X - prev.X;
                                    int dy = formPoint.Y - prev.Y;
                                    if (dx * dx + dy * dy >= MinPointDistanceSq)
                                    {
                                        currentPath.Points.Add(formPoint);

                                        // Рисуем только последний сегмент на layerGraphics — это быстро.
                                        if (layerGraphics != null && lastDrawPoint != null)
                                        {
                                            using (var pen = new Pen(currentPath.Color, currentPath.Thickness)
                                            {
                                                StartCap = LineCap.Round,
                                                EndCap = LineCap.Round,
                                                LineJoin = LineJoin.Round
                                            })
                                            {
                                                layerGraphics.DrawLine(pen, lastDrawPoint.Value, formPoint);
                                            }
                                        }
                                        else if (layerGraphics != null && lastDrawPoint == null)
                                        {
                                            // начальный точечный штрих
                                            using (var b = new SolidBrush(currentPath.Color))
                                            {
                                                layerGraphics.FillEllipse(b, formPoint.X - currentPath.Thickness / 2, formPoint.Y - currentPath.Thickness / 2, currentPath.Thickness, currentPath.Thickness);
                                            }
                                        }

                                        lastDrawPoint = formPoint;
                                        needsUpdate = true; // пометить, что окно нужно обновить на следующем тике
                                    }
                                }
                            }));
                        }
                        break;

                    case WM_LBUTTONUP:
                        if (isDrawing)
                        {
                            this.BeginInvoke((Action)(() =>
                            {
                                isDrawing = false;
                                if (currentPath != null && currentPath.Points.Count > 1)
                                {
                                    allPaths.Add(currentPath);
                                }
                                currentPath = null;
                                lastDrawPoint = null;
                                needsUpdate = true;
                            }));
                        }
                        break;
                }
            }

            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ControlKey)
            {
                ctrlPressed = true;
                this.Cursor = Cursors.Cross;

                int exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
                exStyle &= ~WS_EX_TRANSPARENT; // снимаем click-through, чтобы получать мышь
                SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle);
            }
            else if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }

        private void MainForm_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ControlKey)
            {
                ctrlPressed = false;
                this.Cursor = Cursors.Default;

                int exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
                exStyle |= WS_EX_TRANSPARENT; // вновь пропускаем клики
                SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);

            if (_mouseHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }

            settingsForm?.Close();
        }

        // ---------- Rendering via UpdateLayeredWindow ----------
        // Этот метод создаёт ARGB-битмап, рисует на нём все пути, и обновляет окно.
        private void RedrawLayer()
        {
            // Защита: окно может быть не инициализировано
            if (this.Width <= 0 || this.Height <= 0 || this.IsDisposed) return;

            // Создаём 32bpp ARGB bitmap
            using (var bmp = new Bitmap(this.Width, this.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent); // важно: прозрачный фон

                    // рисуем все пути на bmp
                    RenderDrawing(g);
                }

                IntPtr screenDc = GetDC(IntPtr.Zero);
                IntPtr memDc = CreateCompatibleDC(screenDc);
                IntPtr hBitmap = bmp.GetHbitmap(Color.FromArgb(0)); // попытка сохранить прозрачность
                IntPtr oldBitmap = SelectObject(memDc, hBitmap);

                POINT topPos = new POINT { x = this.Left, y = this.Top };
                SIZE size = new SIZE { cx = this.Width, cy = this.Height };
                POINT src = new POINT { x = 0, y = 0 };

                BLENDFUNCTION blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = AC_SRC_ALPHA
                };

                // UpdateLayeredWindow = обновляем per-pixel альфа содержимое окна
                UpdateLayeredWindow(this.Handle, screenDc, ref topPos, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA);

                // очистка
                SelectObject(memDc, oldBitmap);
                DeleteObject(hBitmap);
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }


        private void RedrawLayerImmediate()
        {
            if (layerBitmap == null) return;

            // Если ты хранишь все линии в allPaths, то layerBitmap уже содержит инкрементальные рисунки.
            // Здесь конвертируем layerBitmap -> HBITMAP и вызываем UpdateLayeredWindow (как раньше).
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = layerBitmap.GetHbitmap(Color.FromArgb(0)); // heavy, но делаем это только ~60fps
            IntPtr oldBitmap = SelectObject(memDc, hBitmap);

            POINT topPos = new POINT { x = this.Left, y = this.Top };
            SIZE size = new SIZE { cx = this.Width, cy = this.Height };
            POINT src = new POINT { x = 0, y = 0 };
            BLENDFUNCTION blend = new BLENDFUNCTION { BlendOp = AC_SRC_OVER, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA };

            UpdateLayeredWindow(this.Handle, screenDc, ref topPos, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA);

            SelectObject(memDc, oldBitmap);
            DeleteObject(hBitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
        private void RenderDrawing(Graphics g)
        {
            // рисуем все сохраненные пути (полностью непрозрачные)
            foreach (var path in allPaths)
            {
                if (path.Points.Count > 1)
                {
                    using (var pen = new Pen(path.Color, path.Thickness)
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round,
                        LineJoin = LineJoin.Round
                    })
                    {
                        g.DrawLines(pen, path.Points.ToArray());
                    }
                }
            }

            // рисуем текущий путь
            if (currentPath != null && currentPath.Points.Count > 1)
            {
                using (var pen = new Pen(currentPath.Color, currentPath.Thickness)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                })
                {
                    g.DrawLines(pen, currentPath.Points.ToArray());
                }
            }
        }

        // Мелкие вспомогательные структуры для хука
        [StructLayout(LayoutKind.Sequential)]
        private struct POINTAPI { public int x, y; } // не используется напрямую

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
    }
}