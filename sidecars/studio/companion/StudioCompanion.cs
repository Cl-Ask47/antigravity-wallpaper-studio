using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Automation;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;

namespace WallpaperStudioCompanion
{
    public class CompanionState
    {
        public bool shutdown { get; set; }
        public bool backdropEnabled { get; set; }
        public bool hudEnabled { get; set; }
        public bool docMaskEnabled { get; set; }
        public bool lowPowerMode { get; set; }
        public bool pauseWhenUnfocused { get; set; }
        public string docMaskColor { get; set; }
        public string stageUrl { get; set; }
        public string previewPath { get; set; }
        public string mediaPath { get; set; }
        public string mediaKind { get; set; }
        public string wallpaperTitle { get; set; }
        public int windowTranslucency { get; set; }
        public int backdropBlur { get; set; }
        public int backdropDim { get; set; }
        public bool videoMuted { get; set; }
        public int videoVolume { get; set; }
        public int hudSize { get; set; }
        public string hudAccent { get; set; }
        public string hudWarnColor { get; set; }
        public string hudDangerColor { get; set; }
        public bool hudShowPercent { get; set; }
        public string hudFont { get; set; }
        public double contextPercent { get; set; }
        public long contextTokens { get; set; }
        public long contextLimit { get; set; }
        public double cacheHitRate { get; set; }
        public string sessionTitle { get; set; }
        public string model { get; set; }
        public int totalSteps { get; set; }
    }

    public class HudOffset
    {
        public int offsetX { get; set; }
        public int offsetY { get; set; }
    }

    internal static class Native
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public int Width { get { return Right - Left; } }
            public int Height { get { return Bottom - Top; } }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        public const uint LWA_ALPHA = 0x00000002;

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint SWP_HIDEWINDOW = 0x0080;
        public const uint SWP_NOCOPYBITS = 0x0100;

        public const uint GW_HWNDNEXT = 2;
        public const uint GW_HWNDPREV = 3;
        public static readonly IntPtr HWND_TOP = IntPtr.Zero;

        public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        public const uint EVENT_SYSTEM_MOVESIZESTART = 0x000A;
        public const uint EVENT_SYSTEM_MOVESIZEEND = 0x000B;
        public const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
        public const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
        public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

        public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        public const int DWMWA_CLOAKED = 14;
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public const int DWMWCP_ROUND = 2;

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        public static extern IntPtr BeginDeferWindowPos(int nNumWindows);

        [DllImport("user32.dll")]
        public static extern IntPtr DeferWindowPos(IntPtr hWinPosInfo, IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern bool EndDeferWindowPos(IntPtr hWinPosInfo);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        public static extern IntPtr SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("winmm.dll")]
        public static extern uint timeBeginPeriod(uint uPeriod);

        [DllImport("winmm.dll")]
        public static extern uint timeEndPeriod(uint uPeriod);

        public static void EnableDpiAwareness()
        {
            try
            {
                SetProcessDpiAwarenessContext(new IntPtr(-4));
                return;
            }
            catch { }
            try { SetProcessDPIAware(); } catch { }
        }

        public static bool GetVisibleBounds(IntPtr hwnd, out RECT rect)
        {
            try
            {
                if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf(typeof(RECT))) == 0)
                {
                    if (rect.Width > 0 && rect.Height > 0) return true;
                }
            }
            catch { }
            return GetWindowRect(hwnd, out rect);
        }

        public static double GetDpiScale(IntPtr hwnd)
        {
            try
            {
                uint dpi = GetDpiForWindow(hwnd);
                if (dpi >= 72 && dpi <= 480) return dpi / 96.0;
            }
            catch { }
            return 1.0;
        }

        public static List<IntPtr> FindAntigravityWindows()
        {
            HashSet<uint> pids = new HashSet<uint>();
            try
            {
                Process[] procs = Process.GetProcessesByName("Antigravity");
                for (int i = 0; i < procs.Length; i++)
                {
                    try { pids.Add((uint)procs[i].Id); } catch { }
                    try { procs[i].Dispose(); } catch { }
                }
            }
            catch { }

            List<IntPtr> result = new List<IntPtr>();
            if (pids.Count == 0) return result;

            EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                if (!pids.Contains(pid)) return true;
                if (!IsWindowVisible(hWnd)) return true;
                RECT r;
                if (GetWindowRect(hWnd, out r) && r.Width >= 260 && r.Height >= 180)
                {
                    result.Add(hWnd);
                }
                return true;
            }, IntPtr.Zero);

            return result;
        }

        public static IntPtr PickPrimaryAntigravityWindow()
        {
            List<IntPtr> wins = FindAntigravityWindows();
            if (wins.Count == 0) return IntPtr.Zero;
            IntPtr fg = GetForegroundWindow();
            for (int i = 0; i < wins.Count; i++)
            {
                if (wins[i] == fg) return wins[i];
            }
            IntPtr best = wins[0];
            long bestArea = 0;
            for (int i = 0; i < wins.Count; i++)
            {
                RECT r;
                if (GetWindowRect(wins[i], out r))
                {
                    long area = (long)r.Width * r.Height;
                    if (area > bestArea)
                    {
                        bestArea = area;
                        best = wins[i];
                    }
                }
            }
            return best;
        }

        public static void SetWindowAlpha(IntPtr hwnd, byte alpha)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return;
            try
            {
                int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
                if (alpha >= 255)
                {
                    if ((ex & WS_EX_LAYERED) != 0)
                    {
                        SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
                        SetWindowLong(hwnd, GWL_EXSTYLE, ex & ~WS_EX_LAYERED);
                    }
                }
                else
                {
                    if ((ex & WS_EX_LAYERED) == 0)
                    {
                        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_LAYERED);
                    }
                    SetLayeredWindowAttributes(hwnd, 0, alpha, LWA_ALPHA);
                }
            }
            catch { }
        }

        public static void TrimWorkingSet()
        {
            try
            {
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, new IntPtr(-1), new IntPtr(-1));
            }
            catch { }
        }

        public static bool IsWindowCloaked(IntPtr hwnd)
        {
            try
            {
                int cloaked = 0;
                if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out cloaked, sizeof(int)) == 0)
                {
                    return cloaked != 0;
                }
            }
            catch { }
            return false;
        }

        public static HashSet<uint> GetAntigravityPids()
        {
            HashSet<uint> pids = new HashSet<uint>();
            try
            {
                Process[] procs = Process.GetProcessesByName("Antigravity");
                for (int i = 0; i < procs.Length; i++)
                {
                    try { pids.Add((uint)procs[i].Id); } catch { }
                    try { procs[i].Dispose(); } catch { }
                }
            }
            catch { }
            return pids;
        }

        public static bool IsWindowFullyOccluded(IntPtr agyHwnd, IntPtr backdropHwnd, IntPtr hudHwnd, RECT agyRect)
        {
            long agyArea = (long)agyRect.Width * agyRect.Height;
            if (agyArea <= 0 || agyHwnd == IntPtr.Zero) return false;
            try
            {
                IntPtr cur = GetWindow(agyHwnd, GW_HWNDPREV);
                int steps = 0;
                while (cur != IntPtr.Zero && steps++ < 64)
                {
                    if (cur != backdropHwnd && cur != hudHwnd && IsWindowVisible(cur) && !IsIconic(cur) && !IsWindowCloaked(cur))
                    {
                        int ex = GetWindowLong(cur, GWL_EXSTYLE);
                        if ((ex & WS_EX_TOOLWINDOW) == 0 && (ex & WS_EX_TRANSPARENT) == 0)
                        {
                            RECT wr;
                            if (GetWindowRect(cur, out wr) && wr.Width > 80 && wr.Height > 80)
                            {
                                int il = Math.Max(agyRect.Left, wr.Left);
                                int it = Math.Max(agyRect.Top, wr.Top);
                                int ir = Math.Min(agyRect.Right, wr.Right);
                                int ib = Math.Min(agyRect.Bottom, wr.Bottom);
                                if (ir > il && ib > it)
                                {
                                    long interArea = (long)(ir - il) * (ib - it);
                                    if (interArea >= (long)(agyArea * 0.90))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                    cur = GetWindow(cur, GW_HWNDPREV);
                }
            }
            catch { }
            return false;
        }

        public static void RestoreAllAntigravityWindows()
        {
            List<IntPtr> wins = FindAntigravityWindows();
            for (int i = 0; i < wins.Count; i++)
            {
                SetWindowAlpha(wins[i], 255);
            }
        }
    }

    public class BackdropWindow : Window
    {
        private readonly Grid _fallbackHost;
        private readonly Image _fallbackImage;
        private readonly BlurEffect _fallbackBlur;
        private readonly Rectangle _fallbackScrim;
        private readonly WebView2 _webView;
        private bool _wvReady = false;
        private bool _wvInitStarted = false;
        private string _wvError = "";
        private string _desiredStageUrl = "";
        private string _activeUrl = "";
        private bool _playbackActive = false;
        private string _fallbackPath = "";
        private string _lastDocMaskJson = "";
        private IntPtr _hwnd = IntPtr.Zero;
        private DispatcherTimer _resyncTimer1 = null;
        private DispatcherTimer _resyncTimer2 = null;
        private bool _lowPowerDesired = false;
        private bool _lowPowerApplied = false;
        private bool _hasLowPowerApplied = false;
        private string _lastPowerMsgJson = "";

        public event Action WebViewInitialized;
        public IntPtr Handle { get { return _hwnd; } }
        public bool WebViewReady { get { return _wvReady; } }
        public string WebViewError { get { return _wvError; } }
        public string ActiveUrl { get { return _activeUrl; } }
        public string WebViewDipSize { get { return string.Format("{0:0}x{1:0} (bg {2:0}x{3:0})", _webView.ActualWidth, _webView.ActualHeight, ActualWidth, ActualHeight); } }

        public BackdropWindow()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Background = Brushes.Black;
            AllowsTransparency = false;
            Left = 0;
            Top = 0;
            Width = 1280;
            Height = 720;

            Grid root = new Grid();
            root.Background = Brushes.Black;

            _fallbackBlur = new BlurEffect();
            _fallbackBlur.Radius = 0;
            _fallbackBlur.RenderingBias = RenderingBias.Performance;

            _fallbackHost = new Grid();
            _fallbackHost.Effect = _fallbackBlur;
            _fallbackHost.Visibility = Visibility.Collapsed;

            _fallbackImage = new Image();
            _fallbackImage.Stretch = Stretch.UniformToFill;
            RenderOptions.SetBitmapScalingMode(_fallbackImage, BitmapScalingMode.HighQuality);
            _fallbackHost.Children.Add(_fallbackImage);

            _fallbackScrim = new Rectangle();
            _fallbackScrim.Fill = Brushes.Black;
            _fallbackScrim.Opacity = 0.22;
            _fallbackScrim.Visibility = Visibility.Collapsed;

            _webView = new WebView2();
            _webView.DefaultBackgroundColor = System.Drawing.Color.Black;
            _webView.HorizontalAlignment = HorizontalAlignment.Stretch;
            _webView.VerticalAlignment = VerticalAlignment.Stretch;
            _webView.Focusable = false;
            // Must remain Visible so WPF creates its HwndHost and EnsureCoreWebView2Async completes!
            _webView.Visibility = Visibility.Visible;

            root.Children.Add(_fallbackHost);
            root.Children.Add(_fallbackScrim);
            root.Children.Add(_webView);
            Content = root;

            SourceInitialized += delegate
            {
                _hwnd = new WindowInteropHelper(this).Handle;
                ClearValue(WidthProperty);
                ClearValue(HeightProperty);
                int ex = Native.GetWindowLong(_hwnd, Native.GWL_EXSTYLE);
                Native.SetWindowLong(
                    _hwnd,
                    Native.GWL_EXSTYLE,
                    ex | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW);
                try
                {
                    int pref = Native.DWMWCP_ROUND;
                    Native.DwmSetWindowAttribute(_hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
                }
                catch { }
            };
        }

        public void StartWebViewInit()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate
            {
                InitWebViewAsync();
            }));
        }

        private async void InitWebViewAsync()
        {
            if (_wvInitStarted) return;
            _wvInitStarted = true;
            try
            {
                string udf = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "agy_wallpaper_studio", "wv2_profile");
                Directory.CreateDirectory(udf);
                CoreWebView2EnvironmentOptions opts = new CoreWebView2EnvironmentOptions(
                    "--autoplay-policy=no-user-gesture-required --disable-background-timer-throttling");
                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, udf, opts);
                await _webView.EnsureCoreWebView2Async(env);
                if (_webView.CoreWebView2 != null)
                {
                    _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                    _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                    _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                    _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                    _webView.ZoomFactor = 1.0;
                    _webView.CoreWebView2.NavigationCompleted += delegate
                    {
                        try { _webView.ZoomFactor = 1.0; } catch { }
                        _lastPowerMsgJson = "";
                        ScheduleViewportResync();
                    };
                    _webView.CoreWebView2.ProcessFailed += delegate(object s, CoreWebView2ProcessFailedEventArgs ev)
                    {
                        _wvError = "WV2 ProcessFailed: " + ev.ProcessFailedKind;
                        try
                        {
                            Dispatcher.BeginInvoke(new Action(delegate
                            {
                                _fallbackHost.Visibility = Visibility.Visible;
                                _fallbackScrim.Visibility = Visibility.Visible;
                                _webView.Visibility = Visibility.Collapsed;
                            }));
                        }
                        catch { }
                    };
                    _wvReady = true;
                    _wvError = "";
                    _lastDipScale = 0;
                    ApplyMemoryTarget(_lowPowerDesired);
                    SyncWebViewNavigation();
                    ScheduleViewportResync();
                    Action cb = WebViewInitialized;
                    if (cb != null)
                    {
                        try { cb(); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                _wvReady = false;
                _wvError = ex.GetType().Name + ": " + ex.Message;
                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        _fallbackHost.Visibility = Visibility.Visible;
                        _fallbackScrim.Visibility = Visibility.Visible;
                        _webView.Visibility = Visibility.Collapsed;
                    }));
                }
                catch { }
            }
        }

        private void SyncWebViewNavigation()
        {
            if (!_wvReady || _webView.CoreWebView2 == null) return;
            string target = (_playbackActive && !string.IsNullOrEmpty(_desiredStageUrl)) ? _desiredStageUrl : "about:blank";
            if (_activeUrl != target)
            {
                _activeUrl = target;
                try
                {
                    _webView.CoreWebView2.Navigate(target);
                }
                catch (Exception ex)
                {
                    _wvError = "Nav: " + ex.Message;
                }
            }
        }

        public void ApplyState(CompanionState s)
        {
            _desiredStageUrl = s.stageUrl ?? "";
            _lowPowerDesired = s.lowPowerMode;
            ApplyMemoryTarget(s.lowPowerMode);
            _fallbackBlur.Radius = Math.Max(0, Math.Min(24, s.backdropBlur));
            _fallbackScrim.Opacity = Math.Max(0.0, Math.Min(0.8, s.backdropDim / 100.0));

            string p = !string.IsNullOrEmpty(s.previewPath) ? s.previewPath : (s.mediaKind == "image" ? (s.mediaPath ?? "") : "");
            if (!string.Equals(_fallbackPath, p, StringComparison.OrdinalIgnoreCase))
            {
                _fallbackPath = p;
                if (!string.IsNullOrEmpty(p) && File.Exists(p))
                {
                    try
                    {
                        BitmapImage bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.DecodePixelWidth = 1920;
                        bmp.UriSource = new Uri(p, UriKind.Absolute);
                        bmp.EndInit();
                        bmp.Freeze();
                        _fallbackImage.Source = bmp;
                    }
                    catch { }
                }
            }
            SyncWebViewNavigation();
        }

        private double _lastDipScale = 0;

        public void SyncWpfSize(int physW, int physH, double dpiScale)
        {
            if (!_wvReady) return;
            if (dpiScale <= 0) dpiScale = 1.0;
            if (Math.Abs(dpiScale - _lastDipScale) < 0.01) return;
            ForceSyncWebView(dpiScale);
        }

        public void ForceSyncWebView(double dpiScale)
        {
            if (dpiScale <= 0) dpiScale = 1.0;
            _lastDipScale = dpiScale;
            if (!_wvReady) return;
            try
            {
                _webView.ZoomFactor = 1.0;
                _webView.InvalidateMeasure();
                InvalidateMeasure();
                UpdateLayout();
                SendViewportResync();
                ScheduleViewportResync();
            }
            catch { }
        }

        public void SendViewportResync()
        {
            if (!_wvReady || _webView.CoreWebView2 == null) return;
            try
            {
                _webView.CoreWebView2.PostWebMessageAsJson("{\"type\":\"resyncViewport\"}");
            }
            catch { }
        }

        public void ScheduleViewportResync()
        {
            try
            {
                if (_resyncTimer1 == null)
                {
                    _resyncTimer1 = new DispatcherTimer(DispatcherPriority.Background);
                    _resyncTimer1.Interval = TimeSpan.FromMilliseconds(250);
                    _resyncTimer1.Tick += delegate
                    {
                        _resyncTimer1.Stop();
                        try
                        {
                            if (_wvReady)
                            {
                                _webView.ZoomFactor = 1.0;
                                SendViewportResync();
                            }
                        }
                        catch { }
                    };
                }
                _resyncTimer1.Stop();
                _resyncTimer1.Start();

                if (_resyncTimer2 == null)
                {
                    _resyncTimer2 = new DispatcherTimer(DispatcherPriority.Background);
                    _resyncTimer2.Interval = TimeSpan.FromMilliseconds(850);
                    _resyncTimer2.Tick += delegate
                    {
                        _resyncTimer2.Stop();
                        try
                        {
                            if (_wvReady)
                            {
                                _webView.ZoomFactor = 1.0;
                                SendViewportResync();
                            }
                        }
                        catch { }
                    };
                }
                _resyncTimer2.Stop();
                _resyncTimer2.Start();
            }
            catch { }
        }

        public void ApplyMemoryTarget(bool lowPower)
        {
            if (!_wvReady || _webView.CoreWebView2 == null) return;
            if (_hasLowPowerApplied && _lowPowerApplied == lowPower) return;
            _hasLowPowerApplied = true;
            _lowPowerApplied = lowPower;
            try
            {
                _webView.CoreWebView2.MemoryUsageTargetLevel = lowPower
                    ? CoreWebView2MemoryUsageTargetLevel.Low
                    : CoreWebView2MemoryUsageTargetLevel.Normal;
            }
            catch { }
        }

        public void SetPowerPaused(bool paused, bool lowPower)
        {
            if (!_wvReady || _webView.CoreWebView2 == null) return;
            string json = string.Format("{{\"type\":\"powerState\",\"paused\":{0},\"lowPower\":{1}}}",
                paused ? "true" : "false",
                lowPower ? "true" : "false");
            if (json == _lastPowerMsgJson) return;
            _lastPowerMsgJson = json;
            try
            {
                _webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch { }
        }

        public void TrySuspendWhenHidden(bool lowPower)
        {
            if (!lowPower || !_wvReady || _webView.CoreWebView2 == null) return;
            try
            {
                _webView.CoreWebView2.TrySuspendAsync();
            }
            catch { }
        }

        public void EnsureResumed()
        {
            if (!_wvReady || _webView.CoreWebView2 == null) return;
            try
            {
                if (_webView.CoreWebView2.IsSuspended)
                {
                    _webView.CoreWebView2.Resume();
                }
            }
            catch { }
        }

        public void SetPlaybackActive(bool active)
        {
            if (_playbackActive == active) return;
            _playbackActive = active;
            if (active) EnsureResumed();
            SyncWebViewNavigation();
        }

        public void PushDocMask(bool show, double x, double y, double w, double h, string bgColor)
        {
            if (!_wvReady || _webView.CoreWebView2 == null) return;
            string col = string.IsNullOrEmpty(bgColor) ? "#0f172a" : bgColor;
            string json = show
                ? string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"type\":\"docMask\",\"show\":true,\"x\":{0:0.0},\"y\":{1:0.0},\"w\":{2:0.0},\"h\":{3:0.0},\"bg\":\"{4}\"}}",
                    x, y, w, h, col.Replace("\"", ""))
                : "{\"type\":\"docMask\",\"show\":false}";
            if (json == _lastDocMaskJson) return;
            _lastDocMaskJson = json;
            try
            {
                _webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch { }
        }
    }

    public class RingHudWindow : Window
    {
        private readonly Canvas _canvas;
        private readonly Ellipse _plate;
        private readonly Ellipse _track;
        private readonly System.Windows.Shapes.Path _arcPath;
        private readonly TextBlock _pctText;
        private readonly ToolTip _tip;
        private IntPtr _hwnd = IntPtr.Zero;
        private int _sizeDip = 40;
        private bool _dragging = false;
        private Native.POINT _dragStartCursor;
        private int _dragStartOffsetX;
        private int _dragStartOffsetY;

        public IntPtr Handle { get { return _hwnd; } }
        public int OffsetX { get; set; }
        public int OffsetY { get; set; }
        public event Action<int, int> OffsetChanged;
        public event Action ResetRequested;

        public RingHudWindow()
        {
            OffsetX = -56;
            OffsetY = -56;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Cursor = Cursors.Hand;
            Left = -32000;
            Top = -32000;
            Width = 48;
            Height = 48;

            _canvas = new Canvas();
            _canvas.Background = Brushes.Transparent;

            _plate = new Ellipse();
            _plate.Fill = new SolidColorBrush(Color.FromArgb(215, 16, 20, 30));
            _plate.Stroke = new SolidColorBrush(Color.FromArgb(52, 255, 255, 255));
            _plate.StrokeThickness = 1.0;

            _track = new Ellipse();
            _track.Fill = Brushes.Transparent;
            _track.Stroke = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255));
            _track.StrokeThickness = 3.2;

            _arcPath = new System.Windows.Shapes.Path();
            _arcPath.StrokeThickness = 3.4;
            _arcPath.StrokeStartLineCap = PenLineCap.Round;
            _arcPath.StrokeEndLineCap = PenLineCap.Round;
            _arcPath.Stroke = new SolidColorBrush(Color.FromRgb(241, 113, 127));

            _pctText = new TextBlock();
            _pctText.Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249));
            _pctText.FontWeight = FontWeights.SemiBold;
            _pctText.TextAlignment = TextAlignment.Center;
            _pctText.Text = "0";

            _canvas.Children.Add(_plate);
            _canvas.Children.Add(_track);
            _canvas.Children.Add(_arcPath);
            _canvas.Children.Add(_pctText);

            _tip = new ToolTip();
            _tip.Background = new SolidColorBrush(Color.FromRgb(18, 22, 33));
            _tip.Foreground = new SolidColorBrush(Color.FromRgb(236, 240, 248));
            _tip.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
            _tip.Padding = new Thickness(10, 7, 10, 7);
            _tip.FontSize = 12;
            ToolTipService.SetInitialShowDelay(_canvas, 120);
            ToolTipService.SetShowDuration(_canvas, 12000);
            _canvas.ToolTip = _tip;

            Content = _canvas;

            MouseLeftButtonDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseUp;

            SourceInitialized += delegate
            {
                _hwnd = new WindowInteropHelper(this).Handle;
                int ex = Native.GetWindowLong(_hwnd, Native.GWL_EXSTYLE);
                Native.SetWindowLong(_hwnd, Native.GWL_EXSTYLE, ex | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW);
            };
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                OffsetX = -56;
                OffsetY = -56;
                if (ResetRequested != null) ResetRequested();
                return;
            }
            _dragging = true;
            Native.GetCursorPos(out _dragStartCursor);
            _dragStartOffsetX = OffsetX;
            _dragStartOffsetY = OffsetY;
            CaptureMouse();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            Native.POINT cur;
            Native.GetCursorPos(out cur);
            OffsetX = _dragStartOffsetX + (cur.X - _dragStartCursor.X);
            OffsetY = _dragStartOffsetY + (cur.Y - _dragStartCursor.Y);
            if (OffsetChanged != null) OffsetChanged(OffsetX, OffsetY);
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            ReleaseMouseCapture();
            if (OffsetChanged != null) OffsetChanged(OffsetX, OffsetY);
        }

        private static Color ParseHexColor(string hex, Color fallback)
        {
            try
            {
                if (!string.IsNullOrEmpty(hex))
                {
                    object obj = ColorConverter.ConvertFromString(hex);
                    if (obj is Color) return (Color)obj;
                }
            }
            catch { }
            return fallback;
        }

        private static string FormatTokens(long n)
        {
            if (n >= 1000000) return string.Format("{0:0.00}M", n / 1000000.0);
            if (n >= 1000) return string.Format("{0:0.0}K", n / 1000.0);
            return n.ToString();
        }

        public void RenderState(CompanionState s)
        {
            int size = Math.Max(32, Math.Min(56, s.hudSize > 0 ? s.hudSize : 40));
            _sizeDip = size;
            Width = size;
            Height = size;
            _canvas.Width = size;
            _canvas.Height = size;

            double pad = 2.0;
            double plateSize = size - pad * 2;
            Canvas.SetLeft(_plate, pad);
            Canvas.SetTop(_plate, pad);
            _plate.Width = plateSize;
            _plate.Height = plateSize;

            double stroke = Math.Max(2.8, size * 0.082);
            double ringMargin = pad + 4.0;
            double ringDia = size - ringMargin * 2;
            Canvas.SetLeft(_track, ringMargin);
            Canvas.SetTop(_track, ringMargin);
            _track.Width = ringDia;
            _track.Height = ringDia;
            _track.StrokeThickness = stroke;

            double pct = Math.Max(0.0, Math.Min(100.0, s.contextPercent));
            Color normalCol = ParseHexColor(s.hudAccent, Color.FromRgb(241, 113, 127));
            Color warnCol = ParseHexColor(s.hudWarnColor, Color.FromRgb(245, 158, 11));
            Color dangerCol = ParseHexColor(s.hudDangerColor, Color.FromRgb(239, 68, 68));
            Color activeCol = pct >= 85.0 ? dangerCol : (pct >= 65.0 ? warnCol : normalCol);

            _arcPath.Stroke = new SolidColorBrush(activeCol);
            _arcPath.StrokeThickness = stroke;

            double cx = size / 2.0;
            double cy = size / 2.0;
            double r = ringDia / 2.0;

            if (pct <= 0.1)
            {
                _arcPath.Data = null;
            }
            else
            {
                double clampedPct = pct >= 99.9 ? 99.9 : pct;
                double angleDeg = clampedPct * 3.6;
                double angleRad = (angleDeg - 90.0) * Math.PI / 180.0;
                Point start = new Point(cx, cy - r);
                Point end = new Point(cx + r * Math.Cos(angleRad), cy + r * Math.Sin(angleRad));
                bool isLarge = angleDeg > 180.0;

                PathFigure fig = new PathFigure();
                fig.StartPoint = start;
                fig.Segments.Add(new ArcSegment(end, new Size(r, r), 0, isLarge, SweepDirection.Clockwise, true));
                PathGeometry geo = new PathGeometry();
                geo.Figures.Add(fig);
                _arcPath.Data = geo;
            }

            if (s.hudShowPercent)
            {
                _pctText.Visibility = Visibility.Visible;
                _pctText.Text = string.Format("{0:0}", Math.Round(pct));
                _pctText.FontSize = Math.Max(9.5, size * 0.25);
                if (!string.IsNullOrEmpty(s.hudFont))
                {
                    try { _pctText.FontFamily = new FontFamily(s.hudFont); } catch { }
                }
                _pctText.Width = size;
                Canvas.SetLeft(_pctText, 0);
                Canvas.SetTop(_pctText, (size - _pctText.FontSize * 1.32) / 2.0);
            }
            else
            {
                _pctText.Visibility = Visibility.Collapsed;
            }

            string tipText = string.Format(
                "上下文占用: {0} / {1} ({2:0.1}%)\n缓存命中: {3:0.1}% · 步数: {4}\n模型: {5}\n会话: {6}\n左键拖动位置 · 双击重置",
                FormatTokens(s.contextTokens),
                FormatTokens(s.contextLimit > 0 ? s.contextLimit : 512000),
                pct,
                s.cacheHitRate,
                s.totalSteps,
                string.IsNullOrEmpty(s.model) ? "Antigravity" : s.model,
                string.IsNullOrEmpty(s.sessionTitle) ? "当前会话" : s.sessionTitle);
            _tip.Content = tipText;
        }

        public int GetSizeDip() { return _sizeDip; }
    }

    public class Program
    {
        private static string _statePath = "";
        private static string _hudPosPath = "";
        private static string _activeWinPath = "";
        private static string _udpPortPath = "";
        private static int _parentPid = 0;
        private static UdpClient _udpServer = null;

        private static BackdropWindow _backdrop;
        private static RingHudWindow _hud;
        private static IntPtr _trackedAgy = IntPtr.Zero;
        private static DateTime _lastWindowDiscovery = DateTime.MinValue;
        private static DateTime _lastInsetCalib = DateTime.MinValue;
        private static DateTime _lastResizeApply = DateTime.MinValue;
        private static DateTime _bootUtc = DateTime.UtcNow;
        private static DispatcherTimer _geomTimer = null;
        private static HashSet<uint> _cachedAgyPids = new HashSet<uint>();
        private static DateTime _unfocusedSinceUtc = DateTime.MinValue;
        private static bool _pausedByPower = false;
        private static string _pauseReason = "";
        private static DateTime _lastTitlePoll = DateTime.MinValue;
        private static int _insetL = 0, _insetT = 0, _insetR = 0, _insetB = 0;
        private static double _cachedDpiScale = 1.0;
        private static int _lastRawW = 0, _lastRawH = 0;
        private static volatile bool _isMovingOrSizing = false;
        private static bool _timerPeriodActive = false;
        private static bool _renderingSubscribed = false;
        private static bool _alreadyHidden = false;
        private static Native.RECT _lastBackdropRect = new Native.RECT();
        private static bool _lastBackdropShown = false;
        private static Native.RECT _lastHudRect = new Native.RECT();
        private static bool _lastHudShown = false;
        private static string _lastReportedTitle = null;
        private static byte _lastAppliedAlpha = 255;
        private static CompanionState _state = new CompanionState();
        private static readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private static DateTime _lastStateWrite = DateTime.MinValue;
        private static DateTime _lastDiagWrite = DateTime.MinValue;
        private static bool _suspendedBySystem = false;
        private static System.Threading.Mutex _singleInstanceMutex = null;
        private static IntPtr _hookLoc = IntPtr.Zero;
        private static IntPtr _hookFg = IntPtr.Zero;
        private static IntPtr _hookMin = IntPtr.Zero;
        private static Native.WinEventDelegate _winEventProc = null;
        private static DispatcherTimer _dragTimer = null;

        // Doc-preview UIA tracking state (written by background UIA thread, read by UI thread)
        private static readonly object _docLock = new object();
        private static bool _docVisible = false;
        private static Native.RECT _docScreenRect = new Native.RECT();
        private static string _docMatchKind = "";
        private static volatile bool _uiaRunning = true;

        private static void WriteDiagSnapshot()
        {
            if ((DateTime.UtcNow - _lastDiagWrite).TotalMilliseconds < 1200) return;
            _lastDiagWrite = DateTime.UtcNow;
            try
            {
                string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "agy_wallpaper_studio");
                Directory.CreateDirectory(dir);
                Native.RECT ar = new Native.RECT();
                Native.RECT br = new Native.RECT();
                if (_trackedAgy != IntPtr.Zero) Native.GetVisibleBounds(_trackedAgy, out ar);
                if (_backdrop != null && _backdrop.Handle != IntPtr.Zero) Native.GetWindowRect(_backdrop.Handle, out br);
                var obj = new Dictionary<string, object>();
                obj["pid"] = Process.GetCurrentProcess().Id;
                obj["agyHwnd"] = _trackedAgy.ToInt64();
                obj["agyRect"] = string.Format("{0},{1} {2}x{3}", ar.Left, ar.Top, ar.Width, ar.Height);
                obj["agyAlpha"] = _lastAppliedAlpha;
                obj["backdropHwnd"] = _backdrop != null ? _backdrop.Handle.ToInt64() : 0;
                obj["backdropVis"] = _backdrop != null && _backdrop.Handle != IntPtr.Zero && Native.IsWindowVisible(_backdrop.Handle);
                obj["backdropRect"] = string.Format("{0},{1} {2}x{3}", br.Left, br.Top, br.Width, br.Height);
                obj["wvReady"] = _backdrop != null && _backdrop.WebViewReady;
                obj["wvDip"] = _backdrop != null ? _backdrop.WebViewDipSize : "";
                obj["wvError"] = _backdrop != null ? (_backdrop.WebViewError ?? "") : "";
                obj["activeUrl"] = _backdrop != null ? (_backdrop.ActiveUrl ?? "") : "";
                obj["backdropEnabled"] = _state.backdropEnabled;
                obj["translucency"] = _state.windowTranslucency;
                obj["lowPowerMode"] = _state.lowPowerMode;
                obj["pausedByPower"] = _pausedByPower;
                obj["pauseReason"] = _pauseReason;
                lock (_docLock)
                {
                    obj["docVisible"] = _docVisible;
                    obj["docKind"] = _docMatchKind;
                    obj["docRect"] = string.Format("{0},{1} {2}x{3}", _docScreenRect.Left, _docScreenRect.Top, _docScreenRect.Width, _docScreenRect.Height);
                }
                var agyAll = new List<string>();
                HashSet<uint> agyPids = new HashSet<uint>();
                try
                {
                    foreach (Process ap in Process.GetProcessesByName("Antigravity"))
                    {
                        try { agyPids.Add((uint)ap.Id); } catch { }
                        try { ap.Dispose(); } catch { }
                    }
                }
                catch { }
                IntPtr fgH = Native.GetForegroundWindow();
                uint fgPid = 0;
                Native.GetWindowThreadProcessId(fgH, out fgPid);
                StringBuilder fgTitle = new StringBuilder(96);
                StringBuilder fgCls = new StringBuilder(48);
                Native.GetWindowText(fgH, fgTitle, 96);
                Native.GetClassName(fgH, fgCls, 48);
                string fgProc = "?";
                try { using (Process fp = Process.GetProcessById((int)fgPid)) { fgProc = fp.ProcessName; } } catch { }
                Native.RECT fgRect;
                Native.GetWindowRect(fgH, out fgRect);
                obj["fg"] = string.Format("{0}[{1}] hwnd={2} cls={3} vis={4} {5},{6} {7}x{8} '{9}'",
                    fgProc, fgPid, fgH.ToInt64(), fgCls, Native.IsWindowVisible(fgH), fgRect.Left, fgRect.Top, fgRect.Width, fgRect.Height, fgTitle);

                Native.EnumWindows(delegate(IntPtr h, IntPtr l)
                {
                    uint wp;
                    Native.GetWindowThreadProcessId(h, out wp);
                    if (!agyPids.Contains(wp)) return true;
                    Native.RECT wr;
                    Native.GetWindowRect(h, out wr);
                    StringBuilder tb = new StringBuilder(96);
                    StringBuilder cb = new StringBuilder(48);
                    Native.GetWindowText(h, tb, 96);
                    Native.GetClassName(h, cb, 48);
                    agyAll.Add(string.Format("pid={0} hwnd={1} vis={2} iconic={3} cls={4} {5},{6} {7}x{8} '{9}'",
                        wp, h.ToInt64(), Native.IsWindowVisible(h), Native.IsIconic(h), cb, wr.Left, wr.Top, wr.Width, wr.Height, tb));
                    return agyAll.Count < 20;
                }, IntPtr.Zero);
                obj["agyPidCount"] = agyPids.Count;
                obj["agyAllWindows"] = agyAll;
                File.WriteAllText(System.IO.Path.Combine(dir, "companion_diag.json"), _json.Serialize(obj), Encoding.UTF8);
            }
            catch { }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            _bootUtc = DateTime.UtcNow;
            Native.EnableDpiAwareness();
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal; } catch { }

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--restore")
                {
                    Native.RestoreAllAntigravityWindows();
                    return;
                }
                if (args[i] == "--state" && i + 1 < args.Length) _statePath = args[++i];
                else if (args[i] == "--hud-pos" && i + 1 < args.Length) _hudPosPath = args[++i];
                else if (args[i] == "--active-win" && i + 1 < args.Length) _activeWinPath = args[++i];
                else if (args[i] == "--udp-port-file" && i + 1 < args.Length) _udpPortPath = args[++i];
                else if (args[i] == "--parent-pid" && i + 1 < args.Length) int.TryParse(args[++i], out _parentPid);
            }

            bool createdNew = false;
            try
            {
                _singleInstanceMutex = new System.Threading.Mutex(true, "Local\\AgyWallpaperStudioCompanionMutex", out createdNew);
                if (!createdNew) return;
            }
            catch { }

            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.DispatcherUnhandledException += delegate(object s, DispatcherUnhandledExceptionEventArgs e)
            {
                RestoreAndHide();
                e.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += delegate
            {
                RestoreAndHide();
            };

            SystemEvents.PowerModeChanged += delegate(object s, PowerModeChangedEventArgs e)
            {
                if (e.Mode == PowerModes.Suspend)
                {
                    _suspendedBySystem = true;
                    RestoreAndHide();
                }
                else if (e.Mode == PowerModes.Resume)
                {
                    _suspendedBySystem = false;
                }
            };
            SystemEvents.SessionSwitch += delegate(object s, SessionSwitchEventArgs e)
            {
                if (e.Reason == SessionSwitchReason.SessionLock)
                {
                    _suspendedBySystem = true;
                    RestoreAndHide();
                }
                else if (e.Reason == SessionSwitchReason.SessionUnlock)
                {
                    _suspendedBySystem = false;
                    _lastInsetCalib = DateTime.MinValue;
                    _lastWindowDiscovery = DateTime.MinValue;
                }
            };
            SystemEvents.DisplaySettingsChanged += delegate
            {
                _lastInsetCalib = DateTime.MinValue;
                _lastRawW = 0;
                _lastRawH = 0;
                SyncGeometry(true);
                if (_backdrop != null && _backdrop.WebViewReady)
                {
                    _backdrop.ForceSyncWebView(_cachedDpiScale);
                }
            };

            _backdrop = new BackdropWindow();
            _hud = new RingHudWindow();
            LoadHudOffset();

            _backdrop.WebViewInitialized += delegate
            {
                _lastInsetCalib = DateTime.MinValue;
                SyncGeometry(true);
            };

            _hud.OffsetChanged += delegate(int ox, int oy)
            {
                SaveHudOffset(ox, oy);
                SyncGeometry(true);
            };
            _hud.ResetRequested += delegate
            {
                SaveHudOffset(_hud.OffsetX, _hud.OffsetY);
                SyncGeometry(true);
            };

            _backdrop.Show();
            _hud.Show();
            int initX = 0, initY = 0, initW = 1280, initH = 720;
            IntPtr initAgy = Native.PickPrimaryAntigravityWindow();
            if (initAgy != IntPtr.Zero)
            {
                Native.RECT ir;
                if (Native.GetVisibleBounds(initAgy, out ir) && ir.Width >= 200 && ir.Height >= 140)
                {
                    initX = ir.Left;
                    initY = ir.Top;
                    initW = ir.Width;
                    initH = ir.Height;
                }
            }
            Native.SetWindowPos(_backdrop.Handle, IntPtr.Zero, initX, initY, initW, initH, Native.SWP_NOACTIVATE | Native.SWP_HIDEWINDOW);
            Native.SetWindowPos(_hud.Handle, IntPtr.Zero, -32000, -32000, 10, 10, Native.SWP_NOACTIVATE | Native.SWP_HIDEWINDOW);

            DispatcherTimer stateTimer = new DispatcherTimer();
            stateTimer.Interval = TimeSpan.FromMilliseconds(90);
            stateTimer.Tick += delegate
            {
                if (IsParentDead())
                {
                    RestoreAndHide();
                    app.Shutdown();
                    return;
                }
                PollStateFile();
                WriteDiagSnapshot();
                if (_state.shutdown)
                {
                    RestoreAndHide();
                    app.Shutdown();
                    return;
                }
            };
            stateTimer.Start();

            _geomTimer = new DispatcherTimer(DispatcherPriority.Background);
            _geomTimer.Interval = TimeSpan.FromMilliseconds(_state.lowPowerMode ? 480 : 240);
            _geomTimer.Tick += delegate
            {
                if (!_isMovingOrSizing) SyncGeometry(false);
            };
            _geomTimer.Start();

            _dragTimer = new DispatcherTimer(DispatcherPriority.Input);
            _dragTimer.Interval = TimeSpan.FromMilliseconds(8);
            _dragTimer.Tick += delegate
            {
                if (_isMovingOrSizing) SyncGeometry(false);
                else StopDragBoost();
            };

            InstallWinEventHooks();
            StartUiaDocWatcher();

            app.Exit += delegate
            {
                _uiaRunning = false;
                StopDragBoost();
                try { if (_hookLoc != IntPtr.Zero) Native.UnhookWinEvent(_hookLoc); } catch { }
                try { if (_hookFg != IntPtr.Zero) Native.UnhookWinEvent(_hookFg); } catch { }
                try { if (_hookMin != IntPtr.Zero) Native.UnhookWinEvent(_hookMin); } catch { }
                try { if (_udpServer != null) _udpServer.Close(); } catch { }
                RestoreAndHide();
            };

            app.Startup += delegate
            {
                _backdrop.StartWebViewInit();
                StartUdpLiveListener(app.Dispatcher);
            };

            PollStateFile();
            SyncGeometry(true);
            app.Run();
        }

        private static void InstallWinEventHooks()
        {
            try
            {
                _winEventProc = new Native.WinEventDelegate(OnWinEvent);
                uint flags = Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS;
                _hookFg = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_MOVESIZEEND, IntPtr.Zero, _winEventProc, 0, 0, flags);
                _hookMin = Native.SetWinEventHook(Native.EVENT_SYSTEM_MINIMIZESTART, Native.EVENT_SYSTEM_MINIMIZEEND, IntPtr.Zero, _winEventProc, 0, 0, flags);
                _hookLoc = Native.SetWinEventHook(Native.EVENT_OBJECT_LOCATIONCHANGE, Native.EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, _winEventProc, 0, 0, flags);
            }
            catch { }
        }

        private static void OnRenderingTick(object sender, EventArgs e)
        {
            if (_isMovingOrSizing)
            {
                SyncGeometry(false);
            }
        }

        private static void StartDragBoost()
        {
            _isMovingOrSizing = true;
            if (!_renderingSubscribed)
            {
                try { CompositionTarget.Rendering += OnRenderingTick; _renderingSubscribed = true; } catch { }
            }
            if (!_timerPeriodActive)
            {
                try { Native.timeBeginPeriod(1); _timerPeriodActive = true; } catch { }
            }
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High; } catch { }
            if (_dragTimer != null && !_dragTimer.IsEnabled) _dragTimer.Start();
        }

        private static void StopDragBoost()
        {
            _isMovingOrSizing = false;
            if (_renderingSubscribed)
            {
                try { CompositionTarget.Rendering -= OnRenderingTick; _renderingSubscribed = false; } catch { }
            }
            if (_dragTimer != null && _dragTimer.IsEnabled) _dragTimer.Stop();
            if (_timerPeriodActive)
            {
                try { Native.timeEndPeriod(1); _timerPeriodActive = false; } catch { }
            }
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal; } catch { }
        }

        private static void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (idObject != 0 || idChild != 0) return;
            if (eventType == Native.EVENT_SYSTEM_FOREGROUND)
            {
                StopDragBoost();
                _lastWindowDiscovery = DateTime.MinValue;
                _lastInsetCalib = DateTime.MinValue;
                SyncGeometry(true);
                return;
            }
            if (hwnd != _trackedAgy || _trackedAgy == IntPtr.Zero) return;
            if (eventType == Native.EVENT_SYSTEM_MINIMIZESTART)
            {
                StopDragBoost();
                RestoreAndHide();
                return;
            }
            if (eventType == Native.EVENT_SYSTEM_MINIMIZEEND)
            {
                _lastInsetCalib = DateTime.MinValue;
                _lastRawW = 0;
                _lastRawH = 0;
                SyncGeometry(true);
                if (_backdrop != null && _backdrop.WebViewReady)
                {
                    _backdrop.ForceSyncWebView(_cachedDpiScale);
                }
                return;
            }
            if (eventType == Native.EVENT_SYSTEM_MOVESIZESTART)
            {
                StartDragBoost();
                SyncGeometry(false);
                return;
            }
            if (eventType == Native.EVENT_SYSTEM_MOVESIZEEND)
            {
                StopDragBoost();
                _lastInsetCalib = DateTime.MinValue;
                _lastRawW = 0;
                _lastRawH = 0;
                _lastResizeApply = DateTime.MinValue;
                SyncGeometry(false);
                return;
            }
            if (!_alreadyHidden)
            {
                SyncGeometry(false);
            }
        }

        private static void StartUiaDocWatcher()
        {
            Thread t = new Thread(delegate()
            {
                while (_uiaRunning)
                {
                    try
                    {
                        IntPtr target = _trackedAgy;
                        if (!_isMovingOrSizing && _state.backdropEnabled && _state.docMaskEnabled && target != IntPtr.Zero && Native.IsWindowVisible(target) && !Native.IsIconic(target))
                        {
                            DetectDocPreviewPane(target);
                        }
                        else if (!_state.docMaskEnabled || target == IntPtr.Zero || Native.IsIconic(target))
                        {
                            lock (_docLock)
                            {
                                _docVisible = false;
                                _docMatchKind = "";
                            }
                        }
                    }
                    catch { }
                    Thread.Sleep(_isMovingOrSizing ? 120 : (_state.lowPowerMode ? 600 : 220));
                }
            });
            t.IsBackground = true;
            try { t.SetApartmentState(ApartmentState.MTA); } catch { }
            t.Start();
        }

        private static void DetectDocPreviewPane(IntPtr agyHwnd)
        {
            bool found = false;
            Native.RECT foundRect = new Native.RECT();
            string kind = "";

            try
            {
                AutomationElement root = AutomationElement.FromHandle(agyHwnd);
                if (root != null)
                {
                    Rect agyBounds = root.Current.BoundingRectangle;

                    // 1. Direct iframe Document named "文档预览"
                    System.Windows.Automation.Condition docCond = new System.Windows.Automation.AndCondition(
                        new System.Windows.Automation.PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document),
                        new System.Windows.Automation.PropertyCondition(AutomationElement.NameProperty, "文档预览")
                    );
                    AutomationElement docEl = root.FindFirst(TreeScope.Descendants, docCond);
                    if (docEl != null && !docEl.Current.IsOffscreen)
                    {
                        Rect r = docEl.Current.BoundingRectangle;
                        if (!r.IsEmpty && r.Width >= 140 && r.Height >= 140 && agyBounds.Width > 0)
                        {
                            found = true;
                            kind = "uia_document";
                            foundRect.Left = (int)Math.Floor(r.Left);
                            foundRect.Top = (int)Math.Floor(r.Top);
                            foundRect.Right = (int)Math.Ceiling(r.Right);
                            foundRect.Bottom = (int)Math.Ceiling(r.Bottom);
                        }
                    }

                    // 2. Fallback: active TabItem named "文档预览" -> compute right-pane viewport below tab bar
                    if (!found)
                    {
                        System.Windows.Automation.Condition tabCond = new System.Windows.Automation.AndCondition(
                            new System.Windows.Automation.PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem),
                            new System.Windows.Automation.PropertyCondition(AutomationElement.NameProperty, "文档预览")
                        );
                        AutomationElement tabEl = root.FindFirst(TreeScope.Descendants, tabCond);
                        if (tabEl != null && !tabEl.Current.IsOffscreen)
                        {
                            bool selected = false;
                            try
                            {
                                object pat;
                                if (tabEl.TryGetCurrentPattern(SelectionItemPattern.Pattern, out pat))
                                {
                                    selected = ((SelectionItemPattern)pat).Current.IsSelected;
                                }
                            }
                            catch { }

                            if (selected)
                            {
                                Rect tabStripRect = tabEl.Current.BoundingRectangle;
                                try
                                {
                                    AutomationElement parent = TreeWalker.ControlViewWalker.GetParent(tabEl);
                                    if (parent != null && parent.Current.ControlType == ControlType.Tab)
                                    {
                                        Rect pr = parent.Current.BoundingRectangle;
                                        if (!pr.IsEmpty && pr.Width >= 160) tabStripRect = pr;
                                    }
                                }
                                catch { }

                                if (!tabStripRect.IsEmpty && !agyBounds.IsEmpty)
                                {
                                    found = true;
                                    kind = "uia_tab";
                                    foundRect.Left = (int)Math.Floor(tabStripRect.Left);
                                    foundRect.Top = (int)Math.Floor(tabStripRect.Bottom);
                                    foundRect.Right = (int)Math.Ceiling(agyBounds.Right) - 2;
                                    foundRect.Bottom = (int)Math.Ceiling(agyBounds.Bottom) - 2;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            lock (_docLock)
            {
                _docVisible = found;
                _docMatchKind = kind;
                if (found) _docScreenRect = foundRect;
            }
        }

        private static void StartUdpLiveListener(Dispatcher dispatcher)
        {
            if (string.IsNullOrEmpty(_udpPortPath)) return;
            try
            {
                _udpServer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
                int boundPort = ((IPEndPoint)_udpServer.Client.LocalEndPoint).Port;
                File.WriteAllText(_udpPortPath, boundPort.ToString(), Encoding.UTF8);

                Thread t = new Thread(delegate()
                {
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    while (true)
                    {
                        try
                        {
                            byte[] data = _udpServer.Receive(ref remote);
                            if (data == null || data.Length == 0) continue;
                            string jsonText = Encoding.UTF8.GetString(data);
                            Dictionary<string, object> patch = _json.Deserialize<Dictionary<string, object>>(jsonText);
                            if (patch == null) continue;
                            dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(delegate
                            {
                                ApplyLivePatch(patch);
                            }));
                        }
                        catch
                        {
                            break;
                        }
                    }
                });
                t.IsBackground = true;
                t.Start();
            }
            catch { }
        }

        private static void ApplyLivePatch(Dictionary<string, object> patch)
        {
            bool hudDirty = false;
            bool alphaDirty = false;

            if (patch.ContainsKey("windowTranslucency"))
            {
                int v;
                if (int.TryParse(Convert.ToString(patch["windowTranslucency"]), out v))
                {
                    _state.windowTranslucency = Math.Max(0, Math.Min(55, v));
                    alphaDirty = true;
                }
            }
            if (patch.ContainsKey("backdropBlur"))
            {
                int v;
                if (int.TryParse(Convert.ToString(patch["backdropBlur"]), out v)) _state.backdropBlur = v;
            }
            if (patch.ContainsKey("backdropDim"))
            {
                int v;
                if (int.TryParse(Convert.ToString(patch["backdropDim"]), out v)) _state.backdropDim = v;
            }
            if (patch.ContainsKey("hudSize"))
            {
                int v;
                if (int.TryParse(Convert.ToString(patch["hudSize"]), out v))
                {
                    _state.hudSize = Math.Max(32, Math.Min(56, v));
                    hudDirty = true;
                }
            }
            if (patch.ContainsKey("hudAccent"))
            {
                _state.hudAccent = Convert.ToString(patch["hudAccent"]);
                hudDirty = true;
            }
            if (patch.ContainsKey("hudWarnColor"))
            {
                _state.hudWarnColor = Convert.ToString(patch["hudWarnColor"]);
                hudDirty = true;
            }
            if (patch.ContainsKey("hudDangerColor"))
            {
                _state.hudDangerColor = Convert.ToString(patch["hudDangerColor"]);
                hudDirty = true;
            }
            if (patch.ContainsKey("hudFont"))
            {
                _state.hudFont = Convert.ToString(patch["hudFont"]);
                hudDirty = true;
            }
            if (patch.ContainsKey("hudShowPercent"))
            {
                bool b;
                if (bool.TryParse(Convert.ToString(patch["hudShowPercent"]), out b))
                {
                    _state.hudShowPercent = b;
                    hudDirty = true;
                }
            }
            if (patch.ContainsKey("docMaskEnabled"))
            {
                bool b;
                if (bool.TryParse(Convert.ToString(patch["docMaskEnabled"]), out b)) _state.docMaskEnabled = b;
            }
            if (patch.ContainsKey("docMaskColor"))
            {
                _state.docMaskColor = Convert.ToString(patch["docMaskColor"]);
            }
            bool powerChanged = false;
            if (patch.ContainsKey("lowPowerMode"))
            {
                bool b;
                if (bool.TryParse(Convert.ToString(patch["lowPowerMode"]), out b))
                {
                    _state.lowPowerMode = b;
                    powerChanged = true;
                }
            }
            if (patch.ContainsKey("pauseWhenUnfocused"))
            {
                bool b;
                if (bool.TryParse(Convert.ToString(patch["pauseWhenUnfocused"]), out b))
                {
                    _state.pauseWhenUnfocused = b;
                    powerChanged = true;
                }
            }
            if (powerChanged)
            {
                if (_backdrop != null) _backdrop.ApplyMemoryTarget(_state.lowPowerMode);
                if (_geomTimer != null) _geomTimer.Interval = TimeSpan.FromMilliseconds(_state.lowPowerMode ? 480 : 240);
                SyncGeometry(false);
            }

            if (alphaDirty && _state.backdropEnabled && _trackedAgy != IntPtr.Zero && Native.IsWindowVisible(_trackedAgy) && !Native.IsIconic(_trackedAgy))
            {
                byte alpha = (byte)Math.Round(255.0 * (100 - _state.windowTranslucency) / 100.0);
                if (alpha != _lastAppliedAlpha)
                {
                    Native.SetWindowAlpha(_trackedAgy, alpha);
                    _lastAppliedAlpha = alpha;
                }
            }

            if (hudDirty && _hud != null)
            {
                _hud.RenderState(_state);
                SyncGeometry(true);
            }
        }

        private static bool IsParentDead()
        {
            if (_parentPid <= 0) return false;
            try
            {
                using (Process p = Process.GetProcessById(_parentPid))
                {
                    return p.HasExited;
                }
            }
            catch
            {
                return true;
            }
        }

        private static void LoadHudOffset()
        {
            if (string.IsNullOrEmpty(_hudPosPath) || !File.Exists(_hudPosPath)) return;
            try
            {
                string raw = File.ReadAllText(_hudPosPath, Encoding.UTF8);
                HudOffset o = _json.Deserialize<HudOffset>(raw);
                if (o != null)
                {
                    _hud.OffsetX = o.offsetX;
                    _hud.OffsetY = o.offsetY;
                }
            }
            catch { }
        }

        private static void SaveHudOffset(int ox, int oy)
        {
            if (string.IsNullOrEmpty(_hudPosPath)) return;
            try
            {
                HudOffset o = new HudOffset { offsetX = ox, offsetY = oy };
                File.WriteAllText(_hudPosPath, _json.Serialize(o), Encoding.UTF8);
            }
            catch { }
        }

        private static void PollStateFile()
        {
            if (string.IsNullOrEmpty(_statePath) || !File.Exists(_statePath)) return;
            try
            {
                DateTime wt = File.GetLastWriteTimeUtc(_statePath);
                if (wt == _lastStateWrite) return;
                _lastStateWrite = wt;
                string raw = File.ReadAllText(_statePath, Encoding.UTF8);
                CompanionState parsed = _json.Deserialize<CompanionState>(raw);
                if (parsed != null)
                {
                    _state = parsed;
                    if (_geomTimer != null)
                    {
                        _geomTimer.Interval = TimeSpan.FromMilliseconds(_state.lowPowerMode ? 480 : 240);
                    }
                    _backdrop.ApplyState(_state);
                    _hud.RenderState(_state);
                }
            }
            catch { }
        }

        private static void RestoreAndHide()
        {
            if (_alreadyHidden) return;
            _alreadyHidden = true;

            try
            {
                if (_trackedAgy != IntPtr.Zero)
                {
                    Native.SetWindowAlpha(_trackedAgy, 255);
                    _lastAppliedAlpha = 255;
                }
                Native.RestoreAllAntigravityWindows();
            }
            catch { }
            try
            {
                if (_backdrop != null && _backdrop.Handle != IntPtr.Zero && _lastBackdropShown)
                {
                    _backdrop.SetPlaybackActive(false);
                    Native.SetWindowPos(_backdrop.Handle, IntPtr.Zero, 0, 0, 0, 0,
                        Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_HIDEWINDOW);
                    _lastBackdropShown = false;
                }
                if (_state.lowPowerMode && _backdrop != null)
                {
                    _pausedByPower = true;
                    _pauseReason = "minimized";
                    _backdrop.TrySuspendWhenHidden(true);
                    Native.TrimWorkingSet();
                }
                if (_hud != null && _hud.Handle != IntPtr.Zero && _lastHudShown)
                {
                    Native.SetWindowPos(_hud.Handle, IntPtr.Zero, 0, 0, 0, 0,
                        Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_HIDEWINDOW);
                    _lastHudShown = false;
                }
            }
            catch { }
        }

        private static bool RectEqual(Native.RECT a, Native.RECT b)
        {
            return a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
        }

        private static void SyncGeometry(bool forceDiscovery)
        {
            if (_suspendedBySystem || _state.shutdown)
            {
                RestoreAndHide();
                return;
            }

            if (!_isMovingOrSizing)
            {
                bool needDiscover = forceDiscovery ||
                    _trackedAgy == IntPtr.Zero ||
                    !Native.IsWindow(_trackedAgy) ||
                    !Native.IsWindowVisible(_trackedAgy) ||
                    (DateTime.UtcNow - _lastWindowDiscovery).TotalMilliseconds >= (_alreadyHidden ? 500 : 800);

                if (needDiscover)
                {
                    _lastWindowDiscovery = DateTime.UtcNow;
                    _cachedAgyPids = Native.GetAntigravityPids();
                    IntPtr agy = Native.PickPrimaryAntigravityWindow();
                    if (agy != _trackedAgy && _trackedAgy != IntPtr.Zero)
                    {
                        Native.SetWindowAlpha(_trackedAgy, 255);
                        _lastAppliedAlpha = 255;
                        _lastInsetCalib = DateTime.MinValue;
                    }
                    _trackedAgy = agy;
                }
            }

            IntPtr tracked = _trackedAgy;
            if (tracked == IntPtr.Zero || !Native.IsWindow(tracked) || !Native.IsWindowVisible(tracked) || Native.IsIconic(tracked))
            {
                StopDragBoost();
                RestoreAndHide();
                return;
            }
            _alreadyHidden = false;

            // Detect Antigravity window title changes when not dragging
            if (!_isMovingOrSizing && !string.IsNullOrEmpty(_activeWinPath) && (DateTime.UtcNow - _lastTitlePoll).TotalMilliseconds >= 80)
            {
                _lastTitlePoll = DateTime.UtcNow;
                try
                {
                    StringBuilder titleBuf = new StringBuilder(256);
                    Native.GetWindowText(tracked, titleBuf, 256);
                    string curTitle = titleBuf.ToString();
                    if (curTitle != _lastReportedTitle)
                    {
                        _lastReportedTitle = curTitle;
                        var wObj = new Dictionary<string, object>();
                        wObj["windowTitle"] = curTitle;
                        wObj["hwnd"] = tracked.ToInt64();
                        wObj["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        File.WriteAllText(_activeWinPath, _json.Serialize(wObj), Encoding.UTF8);
                    }
                }
                catch { }
            }

            // Use instantaneous GetWindowRect + cached DWM border inset (eliminates 1-2 frame DwmGetWindowAttribute lag!)
            Native.RECT rawRect;
            if (!Native.GetWindowRect(tracked, out rawRect) || rawRect.Width < 200 || rawRect.Height < 140)
            {
                StopDragBoost();
                RestoreAndHide();
                return;
            }

            bool sizeChanged = (rawRect.Width != _lastRawW || rawRect.Height != _lastRawH);
            if (!_isMovingOrSizing && (sizeChanged || (DateTime.UtcNow - _lastInsetCalib).TotalMilliseconds >= 600))
            {
                _lastRawW = rawRect.Width;
                _lastRawH = rawRect.Height;
                _lastInsetCalib = DateTime.UtcNow;
                _cachedDpiScale = Native.GetDpiScale(tracked);
                Native.RECT extRect;
                if (Native.GetVisibleBounds(tracked, out extRect) && extRect.Width >= 200 && extRect.Height >= 140)
                {
                    _insetL = Math.Max(0, Math.Min(24, extRect.Left - rawRect.Left));
                    _insetT = Math.Max(0, Math.Min(24, extRect.Top - rawRect.Top));
                    _insetR = Math.Max(0, Math.Min(24, rawRect.Right - extRect.Right));
                    _insetB = Math.Max(0, Math.Min(24, rawRect.Bottom - extRect.Bottom));
                }
            }

            Native.RECT rect = new Native.RECT
            {
                Left = rawRect.Left + _insetL,
                Top = rawRect.Top + _insetT,
                Right = rawRect.Right - _insetR,
                Bottom = rawRect.Bottom - _insetB
            };
            if (rect.Width < 200 || rect.Height < 140)
            {
                rect = rawRect;
            }

            bool wantStageBoot = _state.backdropEnabled && !string.IsNullOrEmpty(_state.stageUrl) && _backdrop.Handle != IntPtr.Zero;
            bool wvBootSettled = _backdrop.WebViewReady || !string.IsNullOrEmpty(_backdrop.WebViewError) || (DateTime.UtcNow - _bootUtc).TotalMilliseconds >= 2500;
            bool wantBackdrop = wantStageBoot && wvBootSettled;
            bool wantHud = _state.hudEnabled && _hud.Handle != IntPtr.Zero;

            if (wantStageBoot && !wantBackdrop)
            {
                _backdrop.SetPlaybackActive(true);
            }

            if (wantBackdrop)
            {
                int translucency = Math.Max(0, Math.Min(55, _state.windowTranslucency));
                byte alpha = (byte)Math.Round(255.0 * (100 - translucency) / 100.0);
                if (alpha != _lastAppliedAlpha)
                {
                    Native.SetWindowAlpha(tracked, alpha);
                    _lastAppliedAlpha = alpha;
                }

                if (!_lastBackdropShown || rect.Width != _lastBackdropRect.Width || rect.Height != _lastBackdropRect.Height)
                {
                    _backdrop.SyncWpfSize(rect.Width, rect.Height, _cachedDpiScale);
                }
                _backdrop.SetPlaybackActive(true);

                bool shouldPause = false;
                string reason = "";
                if (_state.lowPowerMode)
                {
                    if (!_isMovingOrSizing && Native.IsWindowFullyOccluded(tracked, _backdrop.Handle, _hud.Handle, rect))
                    {
                        shouldPause = true;
                        reason = "occluded";
                    }
                    else if (_state.pauseWhenUnfocused && !_isMovingOrSizing)
                    {
                        IntPtr fg = Native.GetForegroundWindow();
                        uint fgPid = 0;
                        if (fg != IntPtr.Zero) Native.GetWindowThreadProcessId(fg, out fgPid);
                        bool isAgyFg = (fg == tracked) || (fgPid != 0 && _cachedAgyPids.Contains(fgPid));
                        if (!isAgyFg)
                        {
                            if (_unfocusedSinceUtc == DateTime.MinValue) _unfocusedSinceUtc = DateTime.UtcNow;
                            if ((DateTime.UtcNow - _unfocusedSinceUtc).TotalMilliseconds >= 1200)
                            {
                                shouldPause = true;
                                reason = "unfocused";
                            }
                        }
                        else
                        {
                            _unfocusedSinceUtc = DateTime.MinValue;
                        }
                    }
                }
                else
                {
                    _unfocusedSinceUtc = DateTime.MinValue;
                }
                _pausedByPower = shouldPause;
                _pauseReason = reason;
                _backdrop.SetPowerPaused(shouldPause, _state.lowPowerMode);

                // Sync doc-preview region mask (relative to backdrop window in CSS DIPs)
                if (_state.docMaskEnabled)
                {
                    bool dVis = false;
                    Native.RECT dRect = new Native.RECT();
                    lock (_docLock)
                    {
                        dVis = _docVisible;
                        dRect = _docScreenRect;
                    }
                    if (dVis)
                    {
                        int cl = Math.Max(rect.Left, dRect.Left);
                        int ct = Math.Max(rect.Top, dRect.Top);
                        int cr = Math.Min(rect.Right, dRect.Right);
                        int cb = Math.Min(rect.Bottom, dRect.Bottom);
                        if (cr - cl >= 80 && cb - ct >= 80)
                        {
                            double scale = _cachedDpiScale > 0 ? _cachedDpiScale : 1.0;
                            _backdrop.PushDocMask(
                                true,
                                (cl - rect.Left) / scale,
                                (ct - rect.Top) / scale,
                                (cr - cl) / scale,
                                (cb - ct) / scale,
                                _state.docMaskColor);
                        }
                        else
                        {
                            _backdrop.PushDocMask(false, 0, 0, 0, 0, "");
                        }
                    }
                    else
                    {
                        _backdrop.PushDocMask(false, 0, 0, 0, 0, "");
                    }
                }
                else
                {
                    _backdrop.PushDocMask(false, 0, 0, 0, 0, "");
                }
            }
            else
            {
                _pausedByPower = false;
                _pauseReason = "";
                _unfocusedSinceUtc = DateTime.MinValue;
                if (_lastAppliedAlpha != 255)
                {
                    Native.SetWindowAlpha(tracked, 255);
                    _lastAppliedAlpha = 255;
                }
                if (_backdrop.Handle != IntPtr.Zero)
                {
                    _backdrop.SetPlaybackActive(false);
                }
            }

            // Compute desired backdrop & HUD operations and commit atomically with BeginDeferWindowPos
            bool needBackdropOp = false;
            IntPtr bgInsertAfter = IntPtr.Zero;
            uint bgFlags = Native.SWP_NOACTIVATE;
            Native.RECT bgTargetRect = rect;
            if (wantBackdrop)
            {
                bool bgSizeChanging = !_lastBackdropShown || rect.Width != _lastBackdropRect.Width || rect.Height != _lastBackdropRect.Height;
                if (_isMovingOrSizing && _lastBackdropShown && bgSizeChanging)
                {
                    if ((DateTime.UtcNow - _lastResizeApply).TotalMilliseconds < 36)
                    {
                        bgTargetRect.Right = bgTargetRect.Left + _lastBackdropRect.Width;
                        bgTargetRect.Bottom = bgTargetRect.Top + _lastBackdropRect.Height;
                    }
                    else
                    {
                        _lastResizeApply = DateTime.UtcNow;
                    }
                }
                else if (bgSizeChanging)
                {
                    _lastResizeApply = DateTime.UtcNow;
                }

                bool zOk = Native.GetWindow(tracked, Native.GW_HWNDNEXT) == _backdrop.Handle;
                bool rOk = _lastBackdropShown && RectEqual(_lastBackdropRect, bgTargetRect);
                if (!zOk || !rOk)
                {
                    needBackdropOp = true;
                    if (!zOk) bgInsertAfter = tracked;
                    else bgFlags |= Native.SWP_NOZORDER;
                    if (_lastBackdropShown && _lastBackdropRect.Width == bgTargetRect.Width && _lastBackdropRect.Height == bgTargetRect.Height)
                        bgFlags |= Native.SWP_NOSIZE;
                    if (!_lastBackdropShown) bgFlags |= Native.SWP_SHOWWINDOW;
                }
            }
            else if (_lastBackdropShown && _backdrop.Handle != IntPtr.Zero)
            {
                needBackdropOp = true;
                bgFlags |= Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_HIDEWINDOW;
            }

            bool needHudOp = false;
            IntPtr hudInsertAfter = IntPtr.Zero;
            uint hudFlags = Native.SWP_NOACTIVATE;
            Native.RECT hudRect = new Native.RECT();
            if (wantHud)
            {
                int pxSize = (int)Math.Round(_hud.GetSizeDip() * _cachedDpiScale);
                int hx = rect.Right + _hud.OffsetX;
                int hy = rect.Bottom + _hud.OffsetY;
                hx = Math.Max(rect.Left + 8, Math.Min(rect.Right - pxSize - 8, hx));
                hy = Math.Max(rect.Top + 36, Math.Min(rect.Bottom - pxSize - 8, hy));
                hudRect.Left = hx;
                hudRect.Top = hy;
                hudRect.Right = hx + pxSize;
                hudRect.Bottom = hy + pxSize;

                IntPtr prevWin = Native.GetWindow(tracked, Native.GW_HWNDPREV);
                bool zOk = prevWin == _hud.Handle;
                bool rOk = _lastHudShown && RectEqual(_lastHudRect, hudRect);
                if (!zOk || !rOk)
                {
                    needHudOp = true;
                    if (!zOk) hudInsertAfter = (prevWin == IntPtr.Zero) ? Native.HWND_TOP : prevWin;
                    else hudFlags |= Native.SWP_NOZORDER;
                    if (_lastHudShown && _lastHudRect.Width == hudRect.Width && _lastHudRect.Height == hudRect.Height)
                        hudFlags |= Native.SWP_NOSIZE;
                    if (!_lastHudShown) hudFlags |= Native.SWP_SHOWWINDOW;
                }
            }
            else if (_lastHudShown && _hud.Handle != IntPtr.Zero)
            {
                needHudOp = true;
                hudFlags |= Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_HIDEWINDOW;
            }

            if (!needBackdropOp && !needHudOp) return;

            bool becameVisible = wantBackdrop && !_lastBackdropShown;
            int opCount = (needBackdropOp ? 1 : 0) + (needHudOp ? 1 : 0);
            IntPtr hdwp = Native.BeginDeferWindowPos(opCount);
            if (hdwp != IntPtr.Zero)
            {
                if (needBackdropOp)
                {
                    hdwp = Native.DeferWindowPos(hdwp, _backdrop.Handle, bgInsertAfter, bgTargetRect.Left, bgTargetRect.Top, bgTargetRect.Width, bgTargetRect.Height, bgFlags);
                }
                if (needHudOp && hdwp != IntPtr.Zero)
                {
                    hdwp = Native.DeferWindowPos(hdwp, _hud.Handle, hudInsertAfter, hudRect.Left, hudRect.Top, hudRect.Width, hudRect.Height, hudFlags);
                }
                if (hdwp != IntPtr.Zero)
                {
                    Native.EndDeferWindowPos(hdwp);
                }
            }
            else
            {
                if (needBackdropOp) Native.SetWindowPos(_backdrop.Handle, bgInsertAfter, bgTargetRect.Left, bgTargetRect.Top, bgTargetRect.Width, bgTargetRect.Height, bgFlags);
                if (needHudOp) Native.SetWindowPos(_hud.Handle, hudInsertAfter, hudRect.Left, hudRect.Top, hudRect.Width, hudRect.Height, hudFlags);
            }

            if (needBackdropOp)
            {
                _lastBackdropShown = wantBackdrop;
                if (wantBackdrop) _lastBackdropRect = bgTargetRect;
                if (becameVisible)
                {
                    _backdrop.ForceSyncWebView(_cachedDpiScale);
                }
            }
            if (needHudOp)
            {
                _lastHudShown = wantHud;
                if (wantHud) _lastHudRect = hudRect;
            }
        }
    }
}
