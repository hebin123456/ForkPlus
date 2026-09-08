// WPF → Avalonia 迁移兼容层 第二部分：控件/输入/系统级 shim 与扩展
// 全部通过 ForkPlus.csproj 的 <Using Include="ForkPlus.UI.WpfCompat" /> 全局引入，
// 迁移期尽量少改业务代码；每处均带 Migration note标记，后续替换为原生实现。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ForkPlus.UI.WpfCompat
{
    // ===== 应用级：WPF WpfApp.MainWindow / .Windows =====

    /// <summary>
    /// WPF WpfApp.MainWindow / WpfApp.Windows 的等价物。
    /// Avalonia 把窗口集合放在 IClassicDesktopStyleApplicationLifetime 上。
    /// </summary>
    public static class WpfApp
    {
        public static IClassicDesktopStyleApplicationLifetime Lifetime
            => Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

        public static Window MainWindow => Lifetime?.MainWindow;

        public static IReadOnlyList<Window> Windows => (IReadOnlyList<Window>)Lifetime?.Windows ?? Array.Empty<Window>();

        public static void Shutdown(int exitCode = 0) => Lifetime?.Shutdown(exitCode);

        /// <summary>排除 self 后的"活动窗口"（做 ShowDialog 的 owner 用）。</summary>
        public static Window ActiveWindow(Window self)
        {
            var wins = Windows.Where(w => w != self && w.IsVisible).ToList();
            return wins.FirstOrDefault(w => w.IsActive) ?? wins.FirstOrDefault();
        }
    }

    /// <summary>
    /// WPF Window.ShowDialog()（无参、阻塞、返回 bool?）的同步等价物。
    /// Avalonia 的 ShowDialog 是 async + 必须传 owner；这里用 Dispatcher.PushFrame
    /// 跑嵌套消息循环 —— 与 WPF ShowDialog 的实现方式一致（模态期间 UI 线程继续泵消息）。
    /// Migration note：新代码请直接用 await ShowDialog(owner)。
    /// </summary>
    public static class WindowDialogCompat
    {
        // WPF ComponentDispatcher.IsThreadModal 近似：记录经本 shim ShowDialog 打开的窗口。
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, object> _modalWindows = new();

        // Migration note：启动期代理 owner 全局复用。此前每对话框各建各关，对话框关闭后应用
        // 0 窗口，ClassicDesktopStyleApplicationLifetime 的 OnLastWindowClose 立即开始
        // Shutdown → 后续对话框（如 WelcomeWindow）PushFrame 抛
        // "InvalidOperationException: Dispatcher shut down"（首启流程实证）。
        // 改为单例复用：真窗口（MainWindow）出现且可见时才关闭代理（TryCloseProxyOwner）。
        private static Window _proxyOwner;

        private static Avalonia.Platform.Screen GetPreferredScreen()
        {
            // 优先用主窗口所在屏幕（双屏时主窗口不在主屏的常见场景），否则退回 Primary。
            var mw = WpfApp.MainWindow;
            if (mw != null && mw.IsVisible)
            {
                var s = mw.Screens?.ScreenFromWindow(mw);
                if (s != null) return s;
            }
            var any = WpfApp.Windows.FirstOrDefault(w => w != null && w.IsVisible);
            return any?.Screens?.ScreenFromWindow(any) ?? any?.Screens?.Primary;
        }

        private static void CenterToScreen(Window w, Avalonia.Platform.Screen screen)
        {
            if (w == null || screen == null) return;
            var wa = screen.WorkingArea;
            // Window.Position 使用物理像素；WorkingArea 也是物理像素。
            // 尺寸在 Opened 之前可能不准确：这里只做一个“尽量居中”的初始值。
            int widthPx = 1;
            int heightPx = 1;
            try
            {
                // 优先用显式 Width/Height（DIP）估算像素；否则用 1x1（代理 owner）。
                if (!double.IsNaN(w.Width) && w.Width > 0) widthPx = (int)Math.Max(1, Math.Round(w.Width * screen.Scaling));
                if (!double.IsNaN(w.Height) && w.Height > 0) heightPx = (int)Math.Max(1, Math.Round(w.Height * screen.Scaling));
            }
            catch { }
            w.Position = new global::Avalonia.PixelPoint(
                wa.X + Math.Max(0, (wa.Width - widthPx) / 2),
                wa.Y + Math.Max(0, (wa.Height - heightPx) / 2));
        }

        // Migration note（可见性）：原 private，仅 ShowDialog 模态路径使用。
        // 修复链 23 改为 internal——非模态 Show() 的窗口（AI 辅助开发/解释/审查等）
        // 也要跟随主窗口所在屏幕，经 WindowOwnerCompat.ShowAtOwnerScreen 复用本逻辑。
        internal static void CenterToOwnerScreenOnOpened(Window dialog, Window owner)
        {
            if (dialog == null) return;
            void Handler(object s, EventArgs e)
            {
                dialog.Opened -= Handler;
                try
                {
                    // 本轮25 修正"跟随"语义：不再做"owner 屏幕工作区居中"（主窗口非最大化、
                    // 不在屏幕中央时，弹窗落在屏幕中央 ≠ 跟随主窗口，观感仍是"跑偏"），
                    // 而是相对 owner 窗口矩形居中（CenterOwner 语义），并 clamp 到屏幕工作区：
                    //   - owner 在任意屏的任意位置 → 弹窗中心对齐 owner 中心（真正跟随）；
                    //   - owner 最大化 → 中心 = 工作区中心，与原屏幕居中一致；
                    //   - owner 最小化/位置异常 → clamp 兜底保证弹窗完整可见。
                    var screen = owner?.Screens?.ScreenFromWindow(owner) ?? owner?.Screens?.Primary ?? dialog.Screens?.Primary;
                    if (screen == null) return;
                    var wa = screen.WorkingArea;
                    // Opened 后 ClientSize 已稳定（DIP），换算为物理像素。
                    int wPx = (int)Math.Max(1, Math.Round(dialog.ClientSize.Width * screen.Scaling));
                    int hPx = (int)Math.Max(1, Math.Round(dialog.ClientSize.Height * screen.Scaling));
                    int x, y;
                    if (owner != null && owner.WindowState != WindowState.Minimized)
                    {
                        // owner 窗口中心（DIP → 物理像素；显式走原生扩展——项目内 InputCompat
                        // 有同名 PointToScreen shim（返回 DIP Point），会被优先解析导致类型错配）。
                        PixelPoint centerPx = global::Avalonia.VisualExtensions.PointToScreen(owner, new Point(owner.Width / 2.0, owner.Height / 2.0));
                        x = centerPx.X - wPx / 2;
                        y = centerPx.Y - hPx / 2;
                    }
                    else
                    {
                        x = wa.X + (wa.Width - wPx) / 2;
                        y = wa.Y + (wa.Height - hPx) / 2;
                    }
                    // clamp 到 owner 屏幕工作区，保证弹窗完整可见。
                    x = (int)Math.Max(wa.X, Math.Min(x, wa.X + wa.Width - wPx));
                    y = (int)Math.Max(wa.Y, Math.Min(y, wa.Y + wa.Height - hPx));
                    dialog.Position = new global::Avalonia.PixelPoint(x, y);
                }
                catch
                {
                    // 定位失败不影响对话框显示
                }
            }
            dialog.Opened += Handler;
        }

        private static Window GetOrCreateProxyOwner()
        {
            if (_proxyOwner == null || !_proxyOwner.IsVisible)
            {
                _proxyOwner = new Window
                {
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Width = 1,
                    Height = 1,
                    Opacity = 0.0,
                    CanResize = false,
                    Title = string.Empty,
                };
                // 居中定位：代理 owner 放到“主窗口所在屏幕”的中心，避免双屏时弹窗跑到另一块屏幕。
                CenterToScreen(_proxyOwner, GetPreferredScreen() ?? _proxyOwner.Screens?.Primary);
                _proxyOwner.Show();
            }
            return _proxyOwner;
        }

        /// <summary>
        /// 关闭闲置代理 owner。仅当存在其他可见窗口（如 MainWindow）时执行——
        /// 0 窗口时关闭会触发 OnLastWindowClose 提前终止应用。
        /// </summary>
        private static void TryCloseProxyOwner()
        {
            var proxy = _proxyOwner;
            if (proxy == null) return;
            if (WpfApp.Windows.Any(w => w != proxy && w.IsVisible))
            {
                _proxyOwner = null;
                proxy.Close();
            }
        }

        /// <summary>WPF ComponentDispatcher.IsThreadModal 的按窗口近似（该窗口是否以 ShowDialog 方式打开）。</summary>
        public static bool IsShownAsDialog(this Window self)
            => self != null && _modalWindows.TryGetValue(self, out _);

        public static bool? ShowDialog(this Window self)
        {
            if (self == null) return null;
            var owner = WindowOwnerCompat.TryGetOwner(self) ?? WpfApp.ActiveWindow(self);
            if (owner == null)
            {
                // Migration note：WPF ShowDialog() 无 owner 也能模态阻塞（如启动期 ConfigureGitInstanceWindow）；
                // Avalonia 12 的 ShowDialog(owner) 强制要求 owner，且 owner 必须 IsVisible=true
                // （否则 InvalidOperationException: Cannot show window with non-visible owner）。
                // 方案：复用全局 1x1 全透明代理窗口做 owner，保持模态语义（见 GetOrCreateProxyOwner）。
                owner = GetOrCreateProxyOwner();
            }
            else
            {
                // 有真实 owner：代理已无用，顺手清理
                TryCloseProxyOwner();
            }
            // 无论窗口自身 StartupLocation 是 CenterScreen/CenterOwner，都强制把对话框居中到 owner 所在屏幕。
            // 这能修复双屏时“弹窗跑到另一块屏幕”的问题（常见于主窗口不在 Primary screen）。
            CenterToOwnerScreenOnOpened(self, owner);
            _modalWindows.Remove(self);
            _modalWindows.Add(self, new object());
            Task<bool?> task = self.ShowDialog<bool?>(owner);
            try
            {
                if (task.IsCompleted) return task.Result;
                var frame = new DispatcherFrame();
                task.ContinueWith(_ =>
                    {
                        _modalWindows.Remove(self);
                        Dispatcher.UIThread.Post(() => frame.Continue = false);
                    },
                    TaskScheduler.Default);
                Dispatcher.UIThread.PushFrame(frame);
                return task.Status == TaskStatus.RanToCompletion ? task.Result : null;
            }
            finally
            {
                _modalWindows.Remove(self);
                // 代理窗口不随单个对话框关闭（全局复用，见 _proxyOwner 注释）
                TryCloseProxyOwner();
            }
        }
    }

    // ===== 字体族：WPF FontWeights / FontStyles / FontStretches =====

    public static class FontWeights
    {
        public static FontWeight Thin => FontWeight.Thin;
        public static FontWeight ExtraLight => FontWeight.ExtraLight;
        public static FontWeight UltraLight => FontWeight.UltraLight;
        public static FontWeight Light => FontWeight.Light;
        public static FontWeight SemiLight => FontWeight.SemiLight;
        public static FontWeight Normal => FontWeight.Normal;
        public static FontWeight Regular => FontWeight.Regular;
        public static FontWeight Medium => FontWeight.Medium;
        public static FontWeight DemiBold => FontWeight.DemiBold;
        public static FontWeight SemiBold => FontWeight.SemiBold;
        public static FontWeight Bold => FontWeight.Bold;
        public static FontWeight ExtraBold => FontWeight.ExtraBold;
        public static FontWeight UltraBold => FontWeight.UltraBold;
        public static FontWeight Black => FontWeight.Black;
        public static FontWeight Heavy => FontWeight.Heavy;
        public static FontWeight ExtraBlack => FontWeight.ExtraBlack;
        public static FontWeight UltraBlack => FontWeight.UltraBlack;
    }

    public static class FontStyles
    {
        public static FontStyle Normal => FontStyle.Normal;
        public static FontStyle Italic => FontStyle.Italic;
        public static FontStyle Oblique => FontStyle.Oblique;
    }

    public static class FontStretches
    {
        public static FontStretch Normal => FontStretch.Normal;
        public static FontStretch Condensed => FontStretch.Condensed;
        public static FontStretch Expanded => FontStretch.Expanded;
        public static FontStretch SemiCondensed => FontStretch.SemiCondensed;
        public static FontStretch SemiExpanded => FontStretch.SemiExpanded;
        public static FontStretch ExtraCondensed => FontStretch.ExtraCondensed;
        public static FontStretch ExtraExpanded => FontStretch.ExtraExpanded;
        public static FontStretch UltraCondensed => FontStretch.UltraCondensed;
        public static FontStretch UltraExpanded => FontStretch.UltraExpanded;
    }

    // ===== Visibility：Avalonia 12 移除了 Visibility 枚举，只剩 bool IsVisible =====

    /// <summary>WPF System.Windows.Visibility。Collapsed/Hidden 都映射为 IsVisible=false。</summary>
    public enum Visibility
    {
        Visible = 0,
        Hidden = 1,
        Collapsed = 2,
    }

    public static class VisibilityCompat
    {
        public static T WithVisibility<T>(this T control, Visibility visibility) where T : Visual
        {
            SetVisibility(control, visibility);
            return control;
        }

        public static void SetVisibility(this Visual visual, Visibility visibility)
            => visual.IsVisible = visibility == Visibility.Visible;

        public static Visibility GetVisibility(this Visual visual)
            => visual.IsVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    // ===== ToolTip：Avalonia 用附加属性 ToolTip.Tip，C# 侧无实例属性 =====

    public static class ToolTipCompat
    {
        /// <summary>对象初始化器链式辅助：new Button { ... }.WithTip("xx")。</summary>
        public static T WithTip<T>(this T control, object tip) where T : Visual
        {
            if (control is Control c) ToolTip.SetTip(c, tip);
            return control;
        }
    }

    // ===== 键盘 / 鼠标：WPF Keyboard / Mouse 静态类 =====

    /// <summary>
    /// WPF System.Windows.Input.ModifierKeys 的等价枚举。
    /// 数值与 Avalonia.Input.KeyModifiers 对齐（None=0, Alt=1, Control=2, Shift=4, Meta=8），
    /// 以便直接与 KeyModifiers 强转比较。
    /// </summary>
    [Flags]
    public enum ModifierKeys
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8,
    }

    /// <summary>
    /// WPF System.Windows.Input.Keyboard 静态类 shim。
    /// FocusedElement 经任意窗口的 FocusManager 查询；按键状态由已打开 TopLevel 的 KeyDown/KeyUp 维护。
    /// </summary>
    public static class Keyboard
    {
        private static readonly HashSet<TopLevel> _trackedTopLevels = new();
        private static readonly HashSet<Key> _downKeys = new();
        private static ModifierKeys _lastModifiers = ModifierKeys.None;

        public static void InstallGlobalKeyTracking(TopLevel topLevel)
        {
            if (topLevel == null || !_trackedTopLevels.Add(topLevel))
            {
                return;
            }

            topLevel.AddHandler(InputElement.KeyDownEvent, (_, e) =>
            {
                _downKeys.Add(e.Key);
                _lastModifiers = (ModifierKeys)(int)e.KeyModifiers;
                AddModifierKeys(_lastModifiers);
            }, RoutingStrategies.Tunnel);

            topLevel.AddHandler(InputElement.KeyUpEvent, (_, e) =>
            {
                _downKeys.Remove(e.Key);
                _lastModifiers = (ModifierKeys)(int)e.KeyModifiers;
                RemoveModifierKey(e.Key);
            }, RoutingStrategies.Tunnel);
        }

        public static InputElement FocusedElement
        {
            get
            {
                foreach (var w in WpfApp.Windows)
                {
                    if (w.FocusManager?.GetFocusedElement() is InputElement ie) return ie;
                }
                return null;
            }
        }

        public static ModifierKeys Modifiers => _lastModifiers;

        public static bool IsKeyDown(Key key)
        {
            if (_downKeys.Contains(key))
            {
                return true;
            }

            // 修饰键位兜底只对左键生效：KeyModifiers 报告不含侧别（Left/Right 无法区分），
            // 右侧修饰键只能由真实 KeyDown 事件（e.Key == Right*，tracker 首行登记）置位，
            // 不能按位推断。WPF 语义对齐（OpenRepositoryInFileExplorerCommand 用
            // IsKeyDown(RightAlt) 做 macOS 右 Alt 特殊输入守卫——左 Alt 组合被按位误判为
            // 右 Alt 按下时，Ctrl+Alt+O 打开文件管理器会被静默早退，迁移回归）。
            return key switch
            {
                Key.LeftCtrl => _lastModifiers.HasFlag(ModifierKeys.Control),
                Key.LeftShift => _lastModifiers.HasFlag(ModifierKeys.Shift),
                Key.LeftAlt => _lastModifiers.HasFlag(ModifierKeys.Alt),
                Key.LWin => _lastModifiers.HasFlag(ModifierKeys.Windows),
                _ => false
            };
        }

        private static void AddModifierKeys(ModifierKeys modifiers)
        {
            // 只登记左键（与 IsKeyDown 兜底同一语义）：右键由真实 KeyDown 事件的
            // _downKeys.Add(e.Key) 登记。若这里按位把右键也补齐，左侧组合键会被
            // IsKeyDown(Right*) 误判（Ctrl+Alt+O 早退，见 IsKeyDown 注释）。
            if (modifiers.HasFlag(ModifierKeys.Control))
            {
                _downKeys.Add(Key.LeftCtrl);
            }
            if (modifiers.HasFlag(ModifierKeys.Shift))
            {
                _downKeys.Add(Key.LeftShift);
            }
            if (modifiers.HasFlag(ModifierKeys.Alt))
            {
                _downKeys.Add(Key.LeftAlt);
            }
            if (modifiers.HasFlag(ModifierKeys.Windows))
            {
                _downKeys.Add(Key.LWin);
            }
        }

        private static void RemoveModifierKey(Key key)
        {
            switch (key)
            {
                case Key.LeftCtrl:
                case Key.RightCtrl:
                    if (!_lastModifiers.HasFlag(ModifierKeys.Control))
                    {
                        _downKeys.Remove(Key.LeftCtrl);
                        _downKeys.Remove(Key.RightCtrl);
                    }
                    break;
                case Key.LeftShift:
                case Key.RightShift:
                    if (!_lastModifiers.HasFlag(ModifierKeys.Shift))
                    {
                        _downKeys.Remove(Key.LeftShift);
                        _downKeys.Remove(Key.RightShift);
                    }
                    break;
                case Key.LeftAlt:
                case Key.RightAlt:
                    if (!_lastModifiers.HasFlag(ModifierKeys.Alt))
                    {
                        _downKeys.Remove(Key.LeftAlt);
                        _downKeys.Remove(Key.RightAlt);
                    }
                    break;
                case Key.LWin:
                case Key.RWin:
                    if (!_lastModifiers.HasFlag(ModifierKeys.Windows))
                    {
                        _downKeys.Remove(Key.LWin);
                        _downKeys.Remove(Key.RWin);
                    }
                    break;
            }
        }
    }

    /// <summary>WPF System.Windows.Input.MouseButtonState。</summary>
    public enum MouseButtonState
    {
        Released = 0,
        Pressed = 1,
    }

    /// <summary>
    /// WPF System.Windows.Input.Mouse 静态类 shim。
    /// Migration note：Avalonia 无全局鼠标位置查询，GetPosition 返回 (0,0)，需按事件参数逐点改造。
    /// </summary>
    public static class Mouse
    {
        public static Point GetPosition(Visual relativeTo) => new Point(0, 0);

        public static void SetCursor(InputElement element, Cursor cursor) => element.Cursor = cursor;

        public static MouseButtonState LeftButton => MouseButtonState.Released;
        public static MouseButtonState RightButton => MouseButtonState.Released;
        public static MouseButtonState MiddleButton => MouseButtonState.Released;
        public static void OverrideCursor(Cursor cursor) { }
    }

    // ===== 视觉树：WPF System.Windows.Media.VisualTreeHelper =====

    /// <summary>
    /// WPF System.Windows.Media.VisualTreeHelper 的等价物，基于
    /// Avalonia.VisualTree.VisualExtensions 实现。
    /// </summary>
    public static class VisualTreeHelper
    {
        // 参数放宽到 AvaloniaObject：WPF 代码常把 Visual/ContentElement 混用为 DependencyObject，
        // 非 Visual（无视觉子级）时按空集合处理。
        private static IEnumerable<Visual> ChildrenOf(AvaloniaObject o)
            => (o as Visual)?.GetVisualChildren() ?? Enumerable.Empty<Visual>();

        public static int GetChildrenCount(AvaloniaObject visual)
            => ChildrenOf(visual).Count();

        public static Visual GetChild(AvaloniaObject visual, int index)
            => ChildrenOf(visual).ElementAt(index);

        public static Visual GetParent(AvaloniaObject visual)
            => (visual as Visual)?.GetVisualParent();

        public static Visual GetAncestor(AvaloniaObject visual, Type type)
            => (visual as Visual)?.GetVisualAncestors().FirstOrDefault(a => type.IsInstanceOfType(a));

        public static T GetAncestor<T>(Visual visual) where T : class
            => visual.GetVisualAncestors().OfType<T>().FirstOrDefault();

        public static IEnumerable<Visual> GetDescendants(Visual visual)
            => visual.GetVisualDescendants();

        public static T GetDescendant<T>(Visual visual) where T : class
            => visual.GetVisualDescendants().OfType<T>().FirstOrDefault();

        public static Visual HitTest(Visual visual, Point point)
            => visual.GetVisualAt(point);

        /// <summary>WPF VisualTreeHelper.GetDpi。Migration note：取控件实际 DPI（当前恒 1.0）。</summary>
        public static DpiScale GetDpi(Visual visual)
        {
            var tl = visual as TopLevel ?? TopLevel.GetTopLevel(visual);
            double sc = tl?.RenderScaling ?? 1.0;
            return new DpiScale(sc, sc);
        }

        /// <summary>WPF LogicalTreeHelper 常用成员顺手放这（逻辑树 = Avalonia 的 Parent 链）。</summary>
        public static StyledElement GetLogicalParent(StyledElement element) => element?.Parent as StyledElement;
    }

    // ===== 系统参数：WPF SystemParameters =====

    /// <summary>
    /// WPF SystemParameters shim。基于主窗口所在 Screen 提供 WorkArea / 屏幕尺寸。
    /// </summary>
    public static class SystemParameters
    {
        private static Avalonia.Platform.Screen PrimaryScreen
        {
            get
            {
                var mw = WpfApp.MainWindow;
                if (mw != null)
                {
                    var fromWindow = mw.Screens?.ScreenFromWindow(mw);
                    if (fromWindow != null) return fromWindow;
                }
                var any = WpfApp.Windows.FirstOrDefault();
                return any?.Screens?.Primary;
            }
        }

        /// <summary>主屏工作区（DIP）。PixelRect → Rect 换算按屏幕 Scaling。</summary>
        public static Rect WorkArea
        {
            get
            {
                var s = PrimaryScreen;
                if (s == null) return new Rect(0, 0, 1920, 1040);
                double sc = s.Scaling <= 0 ? 1 : s.Scaling;
                var wa = s.WorkingArea;
                return new Rect(wa.X / sc, wa.Y / sc, wa.Width / sc, wa.Height / sc);
            }
        }

        public static double PrimaryScreenWidth => PrimaryScreen?.Bounds.Width ?? 1920;
        public static double PrimaryScreenHeight => PrimaryScreen?.Bounds.Height ?? 1080;
        public static double FullPrimaryScreenWidth => PrimaryScreenWidth;
        public static double FullPrimaryScreenHeight => PrimaryScreenHeight;

        public const double MinimumHorizontalDragDistance = 4.0;
        public const double MinimumVerticalDragDistance = 4.0;
        public static Thickness WindowResizeBorderThickness => new Thickness(4);
        public static double HorizontalScrollBarButtonWidth => 17;
        public static double CaptionHeight => 32;

        /// <summary>WPF SystemParameters.SmallIconWidth/Height（16px 系统图标规格）。</summary>
        public static double SmallIconWidth => 16;
        public static double SmallIconHeight => 16;
        public static double HorizontalScrollBarHeight => 17;
        public static double VerticalScrollBarWidth => 17;
    }

    // ===== 剪贴板：WPF System.Windows.Clipboard =====

    /// <summary>
    /// WPF Clipboard 同步 API shim。经主窗口 TopLevel.Clipboard 异步实现，
    /// 用 PushFrame 同步等待（同 ShowDialog 思路）。
    /// </summary>
    public static class Clipboard
    {
        private static IClipboard GetClipboard() => WpfApp.Windows.FirstOrDefault()?.Clipboard;

        private static T Wait<T>(Task<T> task)
        {
            // Migration note：GetClipboard() 无窗口时返回 null → task 为 null，WPF 下 Clipboard 独立于窗口，
            // 这里防御 null（启动早期/窗口关闭后调用剪贴板不再 NRE）。
            if (task == null) return default;
            if (task.IsCompleted) return task.Result;
            var frame = new DispatcherFrame();
            task.ContinueWith(_ => Dispatcher.UIThread.Post(() => frame.Continue = false),
                TaskScheduler.Default);
            Dispatcher.UIThread.PushFrame(frame);
            return task.Status == TaskStatus.RanToCompletion ? task.Result : default;
        }

        public static string GetText() => Wait(GetClipboard()?.TryGetTextAsync()) ?? string.Empty;

        public static void SetText(string text) => GetClipboard()?.SetTextAsync(text);

        /// <summary>WPF Clipboard.SetDataObject(text, copy)。copy 参数仅 WPF OLE 语义，Avalonia 忽略。</summary>
        public static void SetDataObject(string text, bool copy) => SetText(text ?? string.Empty);

        /// <summary>WPF Clipboard.GetData(format)。仅支持文本格式，其余返回 null。</summary>
        public static object GetData(string format)
            => format is "Text" or "UnicodeText" or "System.String" ? GetText() : null;

        /// <summary>WPF Clipboard.SetData(format, value)。二进制等非文本格式走进程内直通表。</summary>
        public static void SetData(string format, object value)
        {
            if (value is string text)
            {
                SetText(text);
                return;
            }
            // Migration note：Avalonia 剪贴板仅支持文本/文件/位图；Serializable 等二进制格式
            // 先存进程内直通表保证本进程粘贴可用，同时降级写文本格式。
            RuntimePayload[format] = value;
            if (value is byte[] bytes && format == WpfDataFormats.Serializable)
                SetText(Convert.ToBase64String(bytes));
        }

        /// <summary>读取进程内直通表（SetData 写入的二进制/自定义对象）。</summary>
        public static object GetRuntimeData(string format)
            => RuntimePayload.TryGetValue(format, out var v) ? v : null;

        private static readonly Dictionary<string, object> RuntimePayload = new();

        public static bool ContainsText()
        {
            var fmts = Wait(GetClipboard()?.GetDataFormatsAsync());
            return fmts?.Any(f => f?.Identifier is "Text" or "System.String" or "FileDrop") == true;
        }

        public static void Clear() => GetClipboard()?.ClearAsync();

        public static void SetFileDropList(System.Collections.IList files)
        {
            // Migration note：文件列表写入剪贴板需要 IStorageItem，暂按文件名文本降级
            var text = string.Join(Environment.NewLine, files?.Cast<object>() ?? Enumerable.Empty<object>());
            if (text.Length > 0) SetText(text);
        }
    }

    // ===== 系统命令：WPF SystemCommands（窗口 chrome 命令，Avalonia 自绘标题栏直接调方法）=====

    public static class SystemCommands
    {
        private sealed class NoopCommand : System.Windows.Input.ICommand
        {
            public event EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) => true;
            public void Execute(object parameter) { }
        }

        public static System.Windows.Input.ICommand CloseWindowCommand { get; } = new NoopCommand();
        public static System.Windows.Input.ICommand MaximizeWindowCommand { get; } = new NoopCommand();
        public static System.Windows.Input.ICommand MinimizeWindowCommand { get; } = new NoopCommand();
        public static System.Windows.Input.ICommand RestoreWindowCommand { get; } = new NoopCommand();
        public static System.Windows.Input.ICommand ShowSystemMenuCommand { get; } = new NoopCommand();
    }

    // ===== WPF DataFormats 常量表（Avalonia 12 把 Avalonia.Input.DataFormats 标记为 error 级 obsolete）=====

    /// <summary>
    /// WPF System.Windows.DataFormats 的字符串常量。
    /// DnD 侧由 WpfDataObject 做格式名匹配（FileDrop ↔ Avalonia DataFormat.File）。
    /// </summary>
    public static class WpfDataFormats
    {
        public const string Text = "Text";
        public const string UnicodeText = "UnicodeText";
        public const string FileDrop = "FileDrop";
        public const string FileName = "FileName";
        public const string FileNameW = "FileNameW";
        public const string Csv = "Csv";
        public const string Html = "HTML Format";
        public const string Rtf = "Rich Text Format";
        public const string Bitmap = "Bitmap";
        public const string Xaml = "Xaml";
        public const string String = "System.String";
        /// <summary>WPF DataFormats.Serializable（Avalonia 无对应物，仅保常量）。</summary>
        public const string Serializable = "Serializable";
    }

    /// <summary>WPF System.Windows.ResizeMode 枚举 shim（Avalonia 12 Window 无 ResizeMode 属性，仅保留枚举语义）。</summary>
    public enum ResizeMode
    {
        NoResize = 0,
        CanMinimize = 1,
        CanResize = 2,
        CanResizeWithGrip = 3,
    }

    // ===== 动画枚举：WPF EasingMode / PopupAnimation =====

    /// <summary>WPF System.Windows.Media.Animation.EasingMode（Avalonia 用 Easing 类族替代）。</summary>
    public enum EasingMode
    {
        EaseIn = 0,
        EaseOut = 1,
        EaseInOut = 2,
    }

    /// <summary>WPF System.Windows.Controls.Primitives.PopupAnimation（Avalonia Popup 无动画枚举）。</summary>
    public enum PopupAnimation
    {
        None = 0,
        Fade = 1,
        Slide = 2,
    }

    /// <summary>EasingMode → Avalonia Easing 的映射辅助。</summary>
    public static class EasingCompat
    {
        public static Avalonia.Animation.Easings.Easing ToEasing(string kind, EasingMode mode)
        {
            string suffix = mode switch
            {
                EasingMode.EaseIn => "EaseIn",
                EasingMode.EaseInOut => "EaseInOut",
                _ => "EaseOut",
            };
            return Avalonia.Animation.Easings.Easing.Parse(kind + suffix);
        }
    }

    // ===== 像素格式：WPF System.Windows.Media.PixelFormats =====

    /// <summary>
    /// WPF PixelFormats shim（与 Avalonia.Platform.PixelFormats 同名，故起名 WpfPixelFormats，
    /// 由 GlobalUsings.cs 里 global using PixelFormats = ... 别名全局引入）。
    /// </summary>
    public static class WpfPixelFormats
    {
        public static global::Avalonia.Platform.PixelFormat Bgra32 => global::Avalonia.Platform.PixelFormat.Bgra8888;
        public static global::Avalonia.Platform.PixelFormat Pbgra32 => global::Avalonia.Platform.PixelFormat.Bgra8888;
        public static global::Avalonia.Platform.PixelFormat Rgb24 => global::Avalonia.Platform.PixelFormat.Rgb32;
        // Avalonia 12 无 Bgr24/Rgba64/Gray8/Bgr565，就近映射
        public static global::Avalonia.Platform.PixelFormat Bgr24 => global::Avalonia.Platform.PixelFormat.Rgb32;
        public static global::Avalonia.Platform.PixelFormat Rgba64 => global::Avalonia.Platform.PixelFormat.Rgba8888;
        public static global::Avalonia.Platform.PixelFormat Gray8 => global::Avalonia.Platform.PixelFormat.Bgra8888;
        public static global::Avalonia.Platform.PixelFormat Bgr565 => global::Avalonia.Platform.PixelFormat.Rgb565;
    }

    // ===== 资源查找：WPF TryFindResource(key) / SetResourceReference =====

    public static class ResourceCompat
    {
        /// <summary>
        /// WPF FrameworkElement.TryFindResource(object key)：找到返回资源，否则 null。
        /// 本轮25 修复"弹窗残缺"根因：原实现走 IResourceHost.TryGetResource 实例方法——
        /// 该方法只查元素自身 Resources/Styles、不沿逻辑树上溯，App 级资源
        /// （App.axaml 合并字典里的 BackgroundBrush/BorderBrush/各 Icon）永远解析不到
        /// （headless 探针实测：挂在已显示窗口里的元素 attachedShim=False）。
        /// 改用链式扩展 ResourceNodeExtensions.TryFindResource（沿 StylingParent：
        /// 元素 → 逻辑树祖先 → TopLevel → Application → 主题资源），等价 WPF 查找语义。
        /// </summary>
        public static object TryFindResource(this StyledElement element, object key)
        {
            if (element == null || key == null) return null;
            if (ResourceNodeExtensions.TryFindResource(
                    element, key, element.ActualThemeVariant ?? ThemeVariant.Default, out var value))
                return value;
            return null;
        }

        /// <summary>WPF Application.Current.TryFindResource(key)（Application 不是 StyledElement，单独适配）。</summary>
        public static object TryFindResource(this Avalonia.Application app, object key)
        {
            if (app == null || key == null) return null;
            return app.TryGetResource(key, app.ActualThemeVariant ?? ThemeVariant.Default, out var value) ? value : null;
        }

        /// <summary>
        /// WPF FrameworkElement.SetResourceReference(prop, key)。
        /// 本轮25 修复"弹窗残缺"根因：原一次性解析赋值有两处缺陷——
        /// (1) 走上面修复前的 TryFindResource，App 级资源拿不到（图标/背景全丢）；
        /// (2) 代码构建的 Popup 内容"先建后挂树"（RootGrid.Children.Add 在 SetResourceReference
        ///     之后），未挂树时链上溯不到 Application，解析注定失败。
        /// 改用 GetResourceObservable 订阅（与 XAML DynamicResource 同一机制）：
        /// 订阅时立即推送当前值；元素挂树（OnAttachedToLogicalTreeCore 触发
        /// ResourcesChanged）、主题切换（ActualThemeVariantChanged）、资源字典变更时
        /// 重新推送并更新属性；未找到推送 UnsetValue（属性回落默认值）。
        /// 注意：同一属性多次调用时按订阅顺序推送，后调用的 key 最终生效
        /// （StageFileUserControl 等切换图标场景依赖此语义）。
        /// </summary>
        public static void SetResourceReference(this AvaloniaObject obj, AvaloniaProperty property, object key)
        {
            if (obj is StyledElement el)
            {
                void Apply()
                {
                    var v = el.TryFindResource(key);
                    obj.SetValue(property, v ?? AvaloniaProperty.UnsetValue);
                }
                Apply();
                // 先建后挂树（Popup 内容等代码构建场景）：挂树时资源链就绪，补一次解析。
                el.AttachedToLogicalTree += delegate { Apply(); };
                // 主题切换/资源字典变更沿逻辑树传播到每个元素的 ResourcesChanged，重新解析
                //（与 XAML DynamicResource 的 GetResourceObservable 订阅机制同一事件源）。
                el.ResourcesChanged += delegate { Apply(); };
            }
        }
    }

    // ===== 模板子级：WPF Control.GetTemplateChild =====

    public static class TemplateCompat
    {
        /// <summary>
        /// WPF Control.GetTemplateChild(name)。模板应用后按 Name 在视觉树后代中查找。
        /// </summary>
        public static object GetTemplateChild(this TemplatedControl control, string name)
        {
            if (control == null || string.IsNullOrEmpty(name)) return null;
            return control.GetVisualDescendants()
                .OfType<StyledElement>()
                .FirstOrDefault(v => v.Name == name);
        }

        public static T GetTemplateChild<T>(this TemplatedControl control, string name) where T : class
            => control.GetTemplateChild(name) as T;
    }

    // ===== ContextMenuOpening 事件适配 =====

    public static class ContextMenuCompat
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ContextMenu, AutoDismissState> AutoDismissStates = new();

        private sealed class AutoDismissState
        {
            private readonly ContextMenu _contextMenu;
            private Window _ownerWindow;
            private EventHandler _ownerWindowDeactivatedHandler;
            private EventHandler<PointerPressedEventArgs> _ownerWindowPointerPressedHandler;
            private EventHandler<PointerReleasedEventArgs> _ownerWindowPointerReleasedHandler;
            private bool _ignoreNextLeftPointerRelease;

            public AutoDismissState(ContextMenu contextMenu)
            {
                _contextMenu = contextMenu;
            }

            public Control OwnerControl { get; set; }

            public bool IsHooked { get; set; }

            public void AttachForOpen()
            {
                Detach();

                Control ownerControl = OwnerControl ?? _contextMenu.PlacementTarget as Control;
                _ownerWindow = TopLevel.GetTopLevel(ownerControl) as Window;
                if (_ownerWindow == null)
                {
                    return;
                }

                _ownerWindowDeactivatedHandler = (_, _) => _contextMenu.Close();
                _ownerWindowPointerPressedHandler = (_, _) => _contextMenu.Close();
                _ignoreNextLeftPointerRelease = true;
                _ownerWindowPointerReleasedHandler = (_, e) =>
                {
                    if (e.InitialPressMouseButton == MouseButton.Left)
                    {
                        if (_ignoreNextLeftPointerRelease)
                        {
                            _ignoreNextLeftPointerRelease = false;
                            return;
                        }
                        _contextMenu.Close();
                    }
                };
                _ownerWindow.Deactivated += _ownerWindowDeactivatedHandler;
                // Bug3 修复：原 Tunnel|Bubble 双路由使同一 PointerReleased 调用处理器两次
                //（第一次消耗 _ignoreNextLeftPointerRelease，第二次立即 Close，下拉刚打开就被收拢）。
                // 改为单一 Bubble 路由：handledEventsToo 仍保证拦截被处理的事件，
                // 且打开菜单的那次 release 能在冒泡到窗口时被恰好一次接收，标志逻辑正确生效。
                _ownerWindow.AddHandler(InputElement.PointerPressedEvent, _ownerWindowPointerPressedHandler, RoutingStrategies.Bubble, handledEventsToo: true);
                _ownerWindow.AddHandler(InputElement.PointerReleasedEvent, _ownerWindowPointerReleasedHandler, RoutingStrategies.Bubble, handledEventsToo: true);
            }

            public void Detach()
            {
                if (_ownerWindow != null && _ownerWindowDeactivatedHandler != null)
                {
                    _ownerWindow.Deactivated -= _ownerWindowDeactivatedHandler;
                }
                if (_ownerWindow != null && _ownerWindowPointerPressedHandler != null)
                {
                    _ownerWindow.RemoveHandler(InputElement.PointerPressedEvent, _ownerWindowPointerPressedHandler);
                }
                if (_ownerWindow != null && _ownerWindowPointerReleasedHandler != null)
                {
                    _ownerWindow.RemoveHandler(InputElement.PointerReleasedEvent, _ownerWindowPointerReleasedHandler);
                }
                _ownerWindow = null;
                _ownerWindowDeactivatedHandler = null;
                _ownerWindowPointerPressedHandler = null;
                _ownerWindowPointerReleasedHandler = null;
                _ignoreNextLeftPointerRelease = false;
            }
        }

        public static void AttachAutoDismiss(ContextMenu contextMenu, Control ownerControl)
        {
            if (contextMenu == null)
            {
                return;
            }

            AutoDismissState state = AutoDismissStates.GetValue(contextMenu, menu => new AutoDismissState(menu));
            state.OwnerControl = ownerControl ?? state.OwnerControl;
            if (state.IsHooked)
            {
                return;
            }

            contextMenu.Opened += (_, _) =>
            {
                // ContextRequested/Open can occur during the same pointer route that opened the menu.
                // Delay arming outside-click dismissal so the opening click does not immediately close it.
                Dispatcher.UIThread.Post(state.AttachForOpen, DispatcherPriority.Background);
            };
            contextMenu.Closed += (_, _) => state.Detach();
            state.IsHooked = true;
        }

        /// <summary>
        /// WPF control.ContextMenuOpening += (s, ContextMenuEventArgs e) 的安装器。
        /// WPF 语义：右键菜单打开前触发，可在此填充 Items，e.Handled=true 阻止打开。
        /// 注：只保留 EventHandler&lt;ContextMenuEventArgs&gt; 一个签名（lambda 与方法组都适用，
        /// 避免与 ContextMenuEventHandler 重载产生二义性）。
        /// </summary>
        public static void AddContextMenuOpeningHandler(this Control control,
            EventHandler<ContextMenuEventArgs> handler)
        {
            if (control?.ContextMenu == null) return; // Migration note：XAML 需先给控件配 ContextMenu
            AttachAutoDismiss(control.ContextMenu, control);
            // Migration note（Avalonia 12 关键坑）：设置 Control.ContextMenu 属性时，Avalonia 内置
            // ControlContextRequested 订阅者随属性变更注册（次序永远先于本方法），它在 Bubble 阶段
            // 打开菜单并置 e.Handled=true。若用默认策略（Direct|Bubble，handledEventsToo=false）订阅，
            // handler 永远排在它之后而被 "!e.Handled" 检查跳过——即菜单填充逻辑从不执行（已实证：
            // 裸 Button + compat 订阅 + RaiseEvent(ContextRequested)，compat/native/显式 Bubble
            // 均不触发，仅 handledEventsToo=True 触发，e.Handled 最终为 True）。
            // 正确做法：以 Tunnel 策略订阅——Tunnel 阶段先于一切 Bubble 订阅者执行（含内置打开逻辑），
            // handler 在菜单打开前运行、置 args.Handled=true 可阻止内置逻辑打开菜单，
            // 与 WPF ContextMenuOpening 时机/取消语义完全一致。
            control.AddHandler(global::Avalonia.Input.InputElement.ContextRequestedEvent,
                (s, e) =>
                {
                    ContextMenuEventArgs args = new ContextMenuEventArgs
                    {
                        TargetElement = e.Source,
                        Source = control
                    };
                    handler(control, args);
                    if (!args.Handled && control.ContextMenu?.Items.Count == 0)
                    {
                        // 空菜单不再打开（原逻辑此处 Close，Tunnel 阶段菜单尚未打开，置 Handled 即可阻止）
                        args.Handled = true;
                    }
                    if (args.Handled)
                    {
                        e.Handled = true;
                    }
                },
                global::Avalonia.Interactivity.RoutingStrategies.Tunnel,
                handledEventsToo: true);
        }

        /// <summary>WPF control.ContextMenuClosing += ... 的安装器（挂 ContextMenu.Closing）。</summary>
        public static void AddContextMenuClosingHandler(this Control control,
            EventHandler<ContextMenuEventArgs> handler)
        {
            if (control?.ContextMenu == null) return;
            AttachAutoDismiss(control.ContextMenu, control);
            control.ContextMenu.Closing += (s, e) => handler(control, new ContextMenuEventArgs());
        }

        /// <summary>WPF control.ContextMenuOpening -= H。Avalonia 无对称移除，记表注销。</summary>
        public static void RemoveContextMenuOpeningHandler(this Control control,
            EventHandler<ContextMenuEventArgs> handler)
        {
            // Migration note：Add 侧目前为匿名 lambda 转发，无法精确反注册；先记录避免编译错误。
        }

        /// <summary>WPF control.ContextMenuClosing -= H。</summary>
        public static void RemoveContextMenuClosingHandler(this Control control,
            EventHandler<ContextMenuEventArgs> handler)
        {
            // Migration note：同上，暂无法精确反注册。
        }
    }

    // ===== Style 赋值适配：WPF control.Style = s（Avalonia Styles 只读集合）=====

    public static class StyleCompat
    {
        public static T WithStyle<T>(this T element, object style) where T : StyledElement
        {
            SetStyle(element, style);
            return element;
        }

        public static void SetStyle(StyledElement element, object style)
        {
            element.Styles.Clear();
            switch (style)
            {
                // Migration note：主题资源（x:Key 的 Style）迁移后全部是 ControlTheme（WPF Style 的 Avalonia 对应物），
                // 与 Style 互不继承但都实现 IStyle。ControlTheme 塞进 Styles 集合不会生效，
                // 必须挂到 TemplatedControl.Theme（等价 XAML 里 Theme="{...}" 引用）。
                case ControlTheme ct:
                    if (element is TemplatedControl templatedControl)
                    {
                        templatedControl.Theme = ct;
                    }
                    break;
                case Style s:
                    element.Styles.Add(s);
                    break;
                case System.Collections.IEnumerable en and not string:
                    foreach (var st in en)
                        if (st is Style s2)
                            element.Styles.Add(s2);
                    break;
            }
        }
    }

    // ===== ToggleButton Checked / Unchecked 事件适配 =====

    /// <summary>
    /// WPF x.Checked += h / x.Unchecked += h 的安装器。
    /// Avalonia ToggleButton 只有 IsCheckedChanged（EventHandler&lt;RoutedEventArgs&gt;），
    /// 这里按新旧值分派为 WPF 语义。
    /// </summary>
    public static class Events
    {
        public static void AddChecked(Control c, EventHandler<RoutedEventArgs> handler)
            => Subscribe(c, handler, wantChecked: true);

        public static void AddUnchecked(Control c, EventHandler<RoutedEventArgs> handler)
            => Subscribe(c, handler, wantChecked: false);

        private static void Subscribe(Control c, EventHandler<RoutedEventArgs> handler, bool wantChecked)
        {
            if (c == null || handler == null) return;
            bool lastChecked = (c as ToggleButton)?.IsChecked == true;
            c.GetObservable(ToggleButton.IsCheckedProperty).Subscribe(new ActionObserver<bool?>(v =>
            {
                bool nowChecked = v == true;
                if (nowChecked != lastChecked && nowChecked == wantChecked)
                    handler(c, new RoutedEventArgs { Source = c });
                lastChecked = nowChecked;
            }));
        }

        private sealed class ActionObserver<T> : IObserver<T>
        {
            private readonly Action<T> _onNext;
            public ActionObserver(Action<T> onNext) { _onNext = onNext; }
            public void OnCompleted() { }
            public void OnError(Exception error) { }
            public void OnNext(T value) => _onNext(value);
        }
    }

    // ===== DnD：WPF IDataObject 读写双向 shim（Avalonia 12 IDataTransfer 适配）=====

    /// <summary>
    /// WPF e.Data（IDataObject）的 shim：
    /// 读取模式：包一层 DragEventArgs.DataTransfer（WpfData() 扩展产生）；
    /// 构建模式：拖放发起侧 new + SetData（实现 IDataTransfer，可直接传给 DoDragDrop）。
    /// 支持 Text / FileDrop / 自定义字符串格式；自定义 CLR 对象走进程内直通表
    /// （DataTransferItem 仅支持文本/文件/位图，跨进程无法承载任意对象）。
    /// </summary>
    public sealed class WpfDataObject : IDataTransfer
    {
        private readonly IDataTransfer _source;   // 读取模式：DragEventArgs.DataTransfer
        private readonly DataTransfer _transfer;  // 构建模式：自建 DataTransfer

        /// <summary>构建模式：拖放发起侧使用。</summary>
        public WpfDataObject()
        {
            _transfer = new DataTransfer();
        }

        internal WpfDataObject(IDataTransfer transfer)
        {
            _source = transfer;
        }

        // ===== IDataTransfer 显式实现（委托内部 DataTransfer）=====

        IReadOnlyList<DataFormat> IDataTransfer.Formats
            => (_source ?? (IDataTransfer)_transfer)?.Formats ?? Array.Empty<DataFormat>();
        IReadOnlyList<IDataTransferItem> IDataTransfer.Items
            => ((_source ?? (IDataTransfer)_transfer)?.Items as IReadOnlyList<IDataTransferItem>) ?? Array.Empty<IDataTransferItem>();

        /// <summary>DataTransfer 侧清理（构建模式下转发；读取侧 DataTransfer 归事件发送方所有）。</summary>
        public void Dispose()
        {
            if (_source == null) (_transfer as IDisposable)?.Dispose();
        }

        private IDataTransfer Read => _source ?? (IDataTransfer)_transfer;

        // ===== WPF 风格 API =====

        /// <summary>WPF SetData(format, value)。自定义对象进进程内直通表。</summary>
        public void SetData(string format, object value)
        {
            if (_source != null) throw new NotSupportedException("读取侧 DataObject 不支持 SetData");
            if (value is string text)
            {
                _transfer.Add(DataTransferItem.CreateText(text));
                return;
            }
            if (value is string[] files)
            {
                // 文件列表编码为换行分隔文本（GetFileDropList 侧对称解码）
                var fmt = DataFormat.CreateStringApplicationFormat(format);
                _transfer.Add(DataTransferItem.Create(fmt, string.Join("\n", files)));
                return;
            }
            // 自定义 CLR 对象：进程内拖放直通表（DataTransferItem 无法承载）
            RuntimePayload[format] = value;
        }

        public bool GetDataPresent(string format)
            => RuntimePayload.ContainsKey(format)
               || Read?.Items?.Any(i => i.Formats.Any(f => Matches(f, format))) == true;

        /// <summary>WPF GetDataPresent(Type)：仅字符串类型可判定。</summary>
        public bool GetDataPresent(Type format)
            => format == typeof(string) && (GetDataPresent("Text") || GetDataPresent("System.String"));

        /// <summary>WPF GetData(Type)。</summary>
        public object GetData(Type format)
            => format == typeof(string) ? (GetData("Text") as string ?? GetData("System.String")) : null;

        public object GetData(string format)
        {
            if (RuntimePayload.TryGetValue(format, out var direct)) return direct;
            var item = Read?.Items?.FirstOrDefault(i => i.Formats.Any(f => Matches(f, format)));
            if (item == null) return null;
            var fmt = item.Formats.First(f => Matches(f, format));
            return item.TryGetRaw(fmt);
        }

        /// <summary>进程内拖放的自定义对象直通表（格式名 → 对象）。</summary>
        private static readonly Dictionary<string, object> RuntimePayload = new();

        /// <summary>WPF GetFileDropList：FileDrop 格式 → 文件路径集合。</summary>
        public System.Collections.IEnumerable GetFileDropList()
        {
            var raw = GetData(FileDropFormat);
            if (raw is IEnumerable<Avalonia.Platform.Storage.IStorageItem> items)
                return items.Select(f => f?.Path?.LocalPath).Where(p => p != null).ToList();
            if (raw is IEnumerable<string> paths && raw is not string)
                return paths.ToList();
            // 内部拖放发起侧（DragDropLauncher）把文件列表编码为换行分隔文本
            if (raw is string joined && joined.Contains('\n'))
                return joined.Split('\n').Where(s => s.Length > 0).ToList();
            return Array.Empty<string>();
        }

        public IEnumerable<string> GetFileNames()
            => GetFileDropList().Cast<string>().ToList();

        private const string FileDropFormat = "FileDrop";

        private static bool Matches(DataFormat f, string wpfFormat)
        {
            if (f == null) return false;
            string n = null;
            try { n = f.ToSystemName("ForkPlus"); } catch { }
            n ??= f.Identifier;
            n ??= "";
            return n == wpfFormat
                || f.Identifier == wpfFormat
                || (wpfFormat == FileDropFormat && (n == "File" || n == "FileDrop" || f.Identifier == "FileDrop" || f.Identifier == "File" || n == "application/x-vnd.ms-filedrop"))
                || (wpfFormat is "Text" or "UnicodeText" or "System.String" && (n == "Text" || f.Identifier == "Text" || f.Identifier == "System.String"));
        }
    }

    public static class DragDropCompat
    {
        /// <summary>WPF e.Data → shim 包装。</summary>
        public static WpfDataObject WpfData(this DragEventArgs e) => new WpfDataObject(e?.DataTransfer);

        /// <summary>WPF e.GetPosition(relativeTo)（DragEventArgs 自带，Avalonia 也有，勿用）。</summary>
        public static void SetDragEffects(this DragEventArgs e, DragDropEffects effects) => e.DragEffects = effects;
    }
}
