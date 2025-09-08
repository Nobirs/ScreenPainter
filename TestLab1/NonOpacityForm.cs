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
    public partial class NonOpacityForm : Form
    {
        private DrawingContext _context = new DrawingContext();

        private Bitmap layerBitmap;
        private Graphics layerGraphics;
        private System.Windows.Forms.Timer renderTimer;
        private volatile bool needsUpdate = false;
        private const int RenderIntervalMs = 16;
        private const int MinPointDistanceSq = 4;


        private SettingsForm settingsForm;
        private bool _isConsumingMouseEvents = false;
        private Point _drawStartPoint;
        private bool ctrlPressed = false;
        private bool isTempCommited = false;

        #region WinAPI Imports
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

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern bool ClipCursor(ref Rectangle lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClipCursor(IntPtr lpRect);

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int ULW_ALPHA = 0x00000002;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int VK_CONTROL = 0x11;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx; public int cy; }

        [StructLayout(LayoutKind.Sequential)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        #endregion

        private LowLevelMouseProc _mouseProc;
        private IntPtr _mouseHook = IntPtr.Zero;

        public NonOpacityForm()
        {
            InitializeComponent();
            SetupForm();
            SetupSettingsForm();
            SetupKeyboardEvents();
            SetupMouseHook();

            // default tool
            var freehand = new FreehandTool() { color = Color.Red, thickness = 3 };
            var ellipsehand = new EllipseTool() { color = Color.Red, thickness = 3 };

            _context.CurrentTool = freehand;
            _context.CurrentTool = ellipsehand;

            CreateLayerResources(this.ClientSize);
            RedrawLayer();
        }

        private void SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.DoubleBuffered = true;

            int exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
            exStyle |= WS_EX_LAYERED;
            SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle);

            CreateLayerResources(this.ClientSize);

            renderTimer = new System.Windows.Forms.Timer();
            renderTimer.Interval = RenderIntervalMs;
            renderTimer.Tick += (s, e) =>
            {
                if (needsUpdate)
                {
                    RedrawLayerImmediate();
                    needsUpdate = false;
                    if (isTempCommited)
                    {
                        _context.Undo();
                        isTempCommited = false;
                    }
                }
            };
            renderTimer.Start();
        }

        private void CreateLayerResources(Size size)
        {
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
                if (layerGraphics != null)
                {
                    layerGraphics.Clear(Color.Transparent);
                    RenderDrawing(layerGraphics);
                    needsUpdate = true;
                }
            }
        }

        private void SetupSettingsForm()
        {
            settingsForm = new SettingsForm();
            settingsForm.OnColorChanged += (color) => { if (_context.CurrentTool != null) _context.CurrentTool.color = color; };
            settingsForm.OnThicknessChanged += (th) => { if (_context.CurrentTool != null) _context.CurrentTool.thickness = th; };
            settingsForm.OnClearScreen += () => { _context.Shapes.Clear(); _context.ClearHistory(); RenderFullAndRequestUpdate(); };
            settingsForm.OnExit += () => this.Close();
            settingsForm.TopMost = true;
            settingsForm.Show();
        }

        private void RenderFullAndRequestUpdate()
        {
            if (layerGraphics == null) return;
            layerGraphics.Clear(Color.Transparent);
            RenderDrawing(layerGraphics);
            needsUpdate = true;
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
            if (nCode >= 0 && this.Visible && !this.IsDisposed)
            {
                int msg = wParam.ToInt32();
                MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

                POINT cursorPos = new POINT { x = hookStruct.pt.x, y = hookStruct.pt.y };
                Point screenPoint = new Point(cursorPos.x, cursorPos.y);

                // Не перехватываем события, когда курсор над settingsForm
                bool isOverSettingsForm = settingsForm != null && settingsForm.Bounds.Contains(screenPoint);
                if (isOverSettingsForm)
                    return CallNextHookEx(_mouseHook, nCode, wParam, lParam);

                // Конвертируем в координаты формы
                POINT clientPoint = cursorPos;
                ScreenToClient(this.Handle, ref clientPoint);
                Point formPoint = new Point(clientPoint.x, clientPoint.y);

                // Обновляем состояние Ctrl
                ctrlPressed = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;

                switch (msg)
                {
                    case WM_LBUTTONDOWN:
                        if (ctrlPressed && !_isConsumingMouseEvents)
                        {
                            _isConsumingMouseEvents = true;
                            _drawStartPoint = screenPoint;
                            CaptureMouse();

                            // Все взаимодействия с GDI+/UI делаем в UI-потоке
                            this.BeginInvoke((Action)(() =>
                            {
                                var tool = _context.CurrentTool;
                                tool.SetCanvasSnapshot(layerBitmap);
                                tool?.OnMouseDown(formPoint, layerGraphics);
                                needsUpdate = true;
                            }));

                            return (IntPtr)1;
                        }
                        break;

                    case WM_MOUSEMOVE:
                        if (_isConsumingMouseEvents && ctrlPressed)
                        {
                            if (_context.CurrentTool.CheckPointsDistance(formPoint))
                            {
                                this.BeginInvoke((Action)(() =>
                                {
                                    var tool = _context.CurrentTool;
                                    if (tool == null) return;

                                    var tempshape = tool.OnMouseMove(formPoint, layerGraphics);
                                    if(tempshape != null)
                                    {
                                        _context.CommitShape(tempshape);
                                        isTempCommited = true;
                                    }
                                    needsUpdate = true;
                                }));
                            }
                            SetCursorPos(formPoint.X, formPoint.Y);
                            return (IntPtr)1;
                        }
                        break;

                    case WM_LBUTTONUP:
                        if (_isConsumingMouseEvents)
                        {
                            this.BeginInvoke((Action)(() =>
                            {
                                var finished = _context.CurrentTool?.OnMouseUp(formPoint, layerGraphics);
                                if (finished != null)
                                {
                                    _context.CommitShape(finished);
                                } 
                                needsUpdate = true;
                            }));

                            _isConsumingMouseEvents = false;
                            ReleaseMouse();
                            return (IntPtr)1;
                        }
                        break;
                }
            }

            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }




        private void CaptureMouse()
        {
            Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
            ClipCursor(ref screenBounds);
            Cursor.Hide();
        }

        private void ReleaseMouse()
        {
            ClipCursor(IntPtr.Zero);
            Cursor.Show();
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_LBUTTONDOWN = 0x0201;
            const int WM_LBUTTONUP = 0x0202;
            const int WM_MOUSEMOVE = 0x0200;
            const int WM_LBUTTONDBLCLK = 0x0203;
            const int WM_RBUTTONDOWN = 0x0204;

            if (_isConsumingMouseEvents)
            {
                switch (m.Msg)
                {
                    case WM_LBUTTONDOWN:
                    case WM_LBUTTONUP:
                    case WM_MOUSEMOVE:
                    case WM_LBUTTONDBLCLK:
                    case WM_RBUTTONDOWN:
                        m.Result = (IntPtr)1;
                        return;
                }
            }

            base.WndProc(ref m);
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ControlKey)
            {
                ctrlPressed = true;
                this.Cursor = Cursors.Cross;

                int exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
                exStyle &= ~WS_EX_TRANSPARENT;
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
                exStyle |= WS_EX_TRANSPARENT;
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
            layerGraphics?.Dispose();
            layerBitmap?.Dispose();
        }

        private void RedrawLayer()
        {
            if (this.Width <= 0 || this.Height <= 0 || this.IsDisposed) return;

            using (var bmp = new Bitmap(this.Width, this.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    RenderDrawing(g);
                }

                UpdateLayeredWindowFromBitmap(bmp);
            }
        }

        private void RedrawLayerImmediate()
        {
            if (layerBitmap == null) return;
            UpdateLayeredWindowFromBitmap(layerBitmap);
        }

        private void UpdateLayeredWindowFromBitmap(Bitmap bmp)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
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

            UpdateLayeredWindow(this.Handle, screenDc, ref topPos, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA);

            SelectObject(memDc, oldBitmap);
            DeleteObject(hBitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }

        private void RenderDrawing(Graphics g)
        {
            foreach (var shape in _context.Shapes)
                shape.Draw(g);


            // preview
            _context.CurrentTool?.DrawPreview(g);
        }
    }
}