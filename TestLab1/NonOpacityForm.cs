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
        private const int RenderIntervalMs = 32;
        private const int MinPointDistanceSq = 4;

        private SettingsForm settingsForm;
        private bool _isConsumingMouseEvents = false;
        private bool ctrlPressed = false;

        private Dictionary<Tool, DrawingTool> tools = new();

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

            tools.Add(Tool.Freehand, new FreehandTool() { color = Color.Red, thickness = 3 });
            tools.Add(Tool.Ellipsehand, new EllipseTool() { color = Color.Red, thickness = 3 });

            _context.CurrentTool = tools[Tool.Freehand];
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            CreateLayerResources(this.ClientSize);
            needsUpdate = true;
            RedrawLayer();
        }

        private void SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.DoubleBuffered = true;

            int exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
            exStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT;
            SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle);

            renderTimer = new System.Windows.Forms.Timer();
            renderTimer.Interval = RenderIntervalMs;
            renderTimer.Tick += (s, e) =>
            {
                if (needsUpdate)
                {
                    RenderDrawing();
                    needsUpdate = false;
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
                needsUpdate = true;
            }
        }

        private void SetupSettingsForm()
        {
            settingsForm = new SettingsForm();
            settingsForm.OnToolChanged += (tool) => { _context.CurrentTool = tools.GetValueOrDefault(tool, tools[Tool.Freehand]); };
            settingsForm.OnColorChanged += (color) => { if (_context.CurrentTool != null) _context.CurrentTool.color = color; };
            settingsForm.OnThicknessChanged += (th) => { if (_context.CurrentTool != null) _context.CurrentTool.thickness = th; };
            settingsForm.OnClearScreen += () => { _context.Shapes.Clear(); _context.ClearHistory(); needsUpdate = true; };
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
            if (nCode >= 0 && this.Visible && !this.IsDisposed)
            {
                int msg = wParam.ToInt32();
                MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

                POINT cursorPos = new POINT { x = hookStruct.pt.x, y = hookStruct.pt.y };
                Point screenPoint = new Point(cursorPos.x, cursorPos.y);

                bool isOverSettingsForm = settingsForm != null && settingsForm.Bounds.Contains(screenPoint);
                if (isOverSettingsForm)
                    return CallNextHookEx(_mouseHook, nCode, wParam, lParam);

                POINT clientPoint = cursorPos;
                ScreenToClient(this.Handle, ref clientPoint);
                Point formPoint = new Point(clientPoint.x, clientPoint.y);

                ctrlPressed = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;

                switch (msg)
                {
                    case WM_LBUTTONDOWN:
                        if (ctrlPressed && !_isConsumingMouseEvents)
                        {
                            _isConsumingMouseEvents = true;
                            CaptureMouse();

                            var tool = _context.CurrentTool;
                            tool?.OnMouseDown(formPoint);
                            needsUpdate = true;

                            return (IntPtr)1;
                        }
                        break;

                    case WM_MOUSEMOVE:
                        if (_isConsumingMouseEvents && ctrlPressed && _context.CurrentTool.CheckPointsDistance(formPoint))
                        {
                            var tool = _context.CurrentTool;
                            if (tool != null)
                            {
                                tool.OnMouseMove(formPoint);
                                needsUpdate = true;
                            }

                        }
                        break;

                    case WM_LBUTTONUP:
                        if (_isConsumingMouseEvents)
                        {
                            var finished = _context.CurrentTool?.OnMouseUp(formPoint);
                            if (finished != null)
                            {
                                _context.CommitShape(finished);
                            }
                            needsUpdate = true;

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
            var screenRect = new Rectangle(this.Bounds.X, this.Bounds.Y, this.Bounds.Width, this.Bounds.Height);
            ClipCursor(ref screenRect);
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
            RenderDrawing();
        }

        private void UpdateLayeredWindowFromBitmap(Bitmap bmp)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
            IntPtr oldBitmap = SelectObject(memDc, hBitmap);

            var screenPos = this.PointToScreen(Point.Empty);
            POINT topPos = new POINT { x = screenPos.X, y = screenPos.Y };

            SIZE size = new SIZE { cx = bmp.Width, cy = bmp.Height };
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

        private void RenderDrawing()
        {
            if (layerGraphics == null || layerBitmap == null) return;

            layerGraphics.Clear(Color.Transparent);

            foreach (var shape in _context.Shapes)
                shape.Draw(layerGraphics);

            _context.CurrentTool?.DrawPreview(layerGraphics);
            UpdateLayeredWindowFromBitmap(layerBitmap);
        }
    }
}
