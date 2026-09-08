// WPF → Avalonia 迁移兼容层：WebView2 兼容控件。
//
// 演化史：
// v1（迁移期）：TextBlock 纯文本占位——AI 弹窗/git mm 手册无排版（真机 bug#10 根因）。
// v2：ScrollViewer + MarkdownHtmlRenderer 自研 HTML→控件树渲染，视觉对齐 md-ai-output.css。
//     但自研渲染器持续漏 HTML 语义（滚动条主题断链、文字选区画刷缺失、表格/嵌套列表边角
//     行为……），且每次补丁都是"追认式"修复。
// v3（2026-09-04，"还是给我引入官方 Native WebView 吧，自己写的终归会漏掉东西"）：
//     内部改为官方 Avalonia.Controls.WebView（NativeWebView）优先——真浏览器引擎渲染，
//     滚动/选区/CSS 全部原生正确；仅当原生引擎不可用时降级回自研渲染器：
//       · headless 测试环境（无原生合成器，WebView 不可用）→ 降级，保证截图证据链
//       · Windows 未装 WebView2 运行时 / Linux 未装 WPE WebKit 与 WebKitGTK → 降级
//       · macOS WKWebView 系统自带 → 恒走原生
//     探测用 WebViewAdapterInfo.GetAdapterInfo(type).IsSupported/IsInstalled（官方静态 API）。
//
// 调用面保持不变：NavigateToString / EnsureCoreWebView2Async / CoreWebView2(事件) /
// ExecuteScriptAsync / DefaultBackgroundColor / Dispose。
//
// 双路径行为映射：
//  - NavigationCompleted：原生=真实导航完成事件（含初始 about:blank，调用方量高度逻辑
//    与 WPF 版一致）；降级=内容布局完成后延迟一帧触发。
//  - ExecuteScriptAsync：原生=InvokeScript 真 JS（scrollHeight/scrollTo 原生可用）；
//    降级=翻译成 MeasureContentHeight/ScrollToEnd。
//  - WebMessageReceived：原生=NativeWebView.WebMessageReceived（JS 侧 invokeCSharpAction，
//    见 AiStreamingWebView.BuildHtmlDocument 的双桥 post 函数）；降级=渲染器回调。
//  - ContextMenuRequested（调用方全部 Handled=true 抑制右键菜单）：原生=导航完成后注入
//    contextmenu preventDefault 脚本；降级=无右键菜单（控件树本身不弹）。
//  - Profile.PreferredColorScheme：记录偏好；原生路径通过对 HTML 文本做 prefers-color-scheme
//    强制改写（引擎无关的 CSS 技巧）让页面配色跟随应用皮肤而非操作系统。
using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using ForkPlus;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.WpfCompat;
using NativeWebView = Avalonia.Controls.NativeWebView;
using WebViewAdapterInfo = Avalonia.Platform.WebViewAdapterInfo;
using WebViewAdapterType = Avalonia.Platform.WebViewAdapterType;

namespace Microsoft.Web.WebView2.Core
{
    /// <summary>CoreWebView2Profile.PreferredColorScheme 枚举（兼容层）。</summary>
    public enum CoreWebView2PreferredColorScheme
    {
        Auto = 0,
        Light = 1,
        Dark = 2,
    }

    /// <summary>CoreWebView2Profile 兼容层（PreferredColorScheme 赋值回写宿主控件触发重导航）。</summary>
    public sealed class CoreWebView2Profile
    {
        internal Microsoft.Web.WebView2.Wpf.WebView2 Owner { get; set; }

        private CoreWebView2PreferredColorScheme _scheme = CoreWebView2PreferredColorScheme.Auto;

        public CoreWebView2PreferredColorScheme PreferredColorScheme
        {
            get => _scheme;
            set
            {
                _scheme = value;
                // WPF 原版赋值即时生效；兼容层转发给控件记录明暗并按需重导航（CSS 强制）
                Owner?.SetPreferredColorScheme(value);
            }
        }
    }

    /// <summary>CoreWebView2Environment 兼容层（WebView2EnvironmentHelper 返回值形状）。</summary>
    public sealed class CoreWebView2Environment
    {
        /// <summary>原 WinRT API CoreWebView2Environment.CreateAsync（兼容层返回空环境）。</summary>
        public static Task<CoreWebView2Environment> CreateAsync(string browserExecutableFolder = null, string userDataFolder = null)
            => Task.FromResult(new CoreWebView2Environment());
    }

    public class CoreWebView2ContextMenuRequestedEventArgs : EventArgs
    {
        public bool Handled { get; set; }
    }

    public class CoreWebView2NavigationCompletedEventArgs : EventArgs
    {
        public bool IsSuccess { get; set; }
    }

    public class CoreWebView2WebMessageReceivedEventArgs : EventArgs
    {
        public string Message { get; }
        public CoreWebView2WebMessageReceivedEventArgs(string message) { Message = message; }
        public string TryGetWebMessageAsString() => Message;
    }

    /// <summary>CoreWebView2 兼容层：事件由两条渲染路径（原生导航 / 降级渲染）驱动。</summary>
    public sealed class CoreWebView2
    {
        internal CoreWebView2(Microsoft.Web.WebView2.Wpf.WebView2 owner)
        {
            Owner = owner;
            Profile.Owner = owner;
        }

        /// <summary>宿主控件（脚本调用转发目标）。</summary>
        internal Microsoft.Web.WebView2.Wpf.WebView2 Owner { get; }

        public CoreWebView2Profile Profile { get; } = new();

        public event EventHandler<CoreWebView2ContextMenuRequestedEventArgs> ContextMenuRequested;
        public event EventHandler<CoreWebView2NavigationCompletedEventArgs> NavigationCompleted;
        public event EventHandler<CoreWebView2WebMessageReceivedEventArgs> WebMessageReceived;

        /// <summary>WPF CoreWebView2.ExecuteScriptAsync：转发宿主控件（原生=InvokeScript 真 JS，
        /// 降级=翻译 scrollHeight/scrollTo 两个真实用例）。修复（2026-09-04）：此前为返回 "null"
        /// 的空壳，而 AiStreamingWebView 流式滚底 / AiDevelopmentWindow 气泡自动高度都经
        /// CoreWebView2 调用，两条路径下全部静默失效。</summary>
        public Task<string> ExecuteScriptAsync(string script)
        {
            Microsoft.Web.WebView2.Wpf.WebView2 owner = Owner;
            return owner != null ? owner.ExecuteScriptAsync(script) : Task.FromResult("null");
        }

        internal void RaiseNavigationCompleted(bool success)
            => NavigationCompleted?.Invoke(this, new CoreWebView2NavigationCompletedEventArgs { IsSuccess = success });

        internal void RaiseWebMessageReceived(string message)
            => WebMessageReceived?.Invoke(this, new CoreWebView2WebMessageReceivedEventArgs(message));
    }
}

namespace Microsoft.Web.WebView2.Wpf
{
    using Microsoft.Web.WebView2.Core;

    /// <summary>
    /// WebView2 兼容控件（v3 混合架构）：官方 NativeWebView 优先，自研 MarkdownHtmlRenderer 降级。
    /// 明暗主题跟随 NotificationCenter.ApplicationThemeChanged（原生路径=重导航+CSS 强制，降级路径=重渲染）。
    /// </summary>
    public class WebView2 : ContentControl
    {
        private CoreWebView2 _core;
        private string _lastHtml;
        private bool _themeHooked;
        private bool? _preferredDark;

        // ── 原生路径状态 ──
        private NativeWebView _native;
        private bool _nativeAdapterReady;
        private string _pendingNativeHtml;
        private static bool? _nativeSupportedCache;

        // ── 降级路径状态 ──
        private ScrollViewer _fallback;

        public WebView2()
        {
            Background = Brushes.Transparent;
            // 主题表面：挂树前先按当前明暗铺底（挂树后由主题钩子持续跟随）
            ApplyThemedBackground();
            // 挂树期间监听主题切换（离树退订，重挂再订——控件复用/窗口重建场景不丢事件）
            AttachedToVisualTree += delegate { HookThemeChange(); };
            DetachedFromVisualTree += delegate { UnhookThemeChange(); };
        }

        /// <summary>
        /// 主题表面修复（2026-09-07，"AI 弹窗刚打开一片黑"）：
        /// 控件自身 + 原生 WebView 的背景从 Transparent 改为"按当前明暗的主题底色"——
        /// 原生引擎（WPE/WebKitGTK 离屏合成）在首次绘制前透明表面会呈现为黑色，
        /// 内容到达前整个区域黑屏。铺主题底色后，加载期与正文底色一致。
        /// 调用方显式设置过非透明 DefaultBackgroundColor 时优先尊重调用方。
        /// </summary>
        private void ApplyThemedBackground()
        {
            System.Drawing.Color explicitColor = DefaultBackgroundColor;
            Avalonia.Media.Color surface = explicitColor.A > 0
                ? Avalonia.Media.Color.FromArgb(explicitColor.A, explicitColor.R, explicitColor.G, explicitColor.B)
                : (CurrentDark()
                    ? Avalonia.Media.Color.FromRgb(0x28, 0x28, 0x28)
                    : Avalonia.Media.Color.FromRgb(0xFA, 0xFA, 0xFA));
            var brush = new SolidColorBrush(surface);
            Background = brush;
            if (_native != null)
            {
                _native.Background = brush;
            }
        }

        private void HookThemeChange()
        {
            if (_themeHooked)
            {
                return;
            }
            _themeHooked = true;
            ForkPlus.NotificationCenter.Current.ApplicationThemeChanged += OnApplicationThemeChanged;
        }

        private void UnhookThemeChange()
        {
            if (!_themeHooked)
            {
                return;
            }
            _themeHooked = false;
            ForkPlus.NotificationCenter.Current.ApplicationThemeChanged -= OnApplicationThemeChanged;
        }

        private void OnApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
        {
            // 主题底色始终跟随刷新（原先只在已有内容时重导航，空白态表面留在旧色）
            ApplyThemedBackground();
            if (_lastHtml != null)
            {
                // 两条路径都用当前明暗重新呈现（原生=重导航带 CSS 强制；降级=重渲染）
                NavigateCurrent();
            }
        }

        /// <summary>WPF WebView2.WebView → 兼容层直接暴露自身。</summary>
        public WebView2 CoreWebView2Host => this;

        /// <summary>WPF WebView2.DefaultBackgroundColor（兼容层：无文档/加载期呈现的底色）。
        /// 透明时按主题底色呈现（见 ApplyThemedBackground），非透明值原样尊重。</summary>
        public System.Drawing.Color DefaultBackgroundColor
        {
            get => _defaultBackgroundColor;
            set
            {
                _defaultBackgroundColor = value;
                ApplyThemedBackground();
            }
        }

        private System.Drawing.Color _defaultBackgroundColor = System.Drawing.Color.Transparent;

        /// <summary>WPF WebView2.Dispose()：释放原生 WebView 适配器（若实现了 IDisposable）。</summary>
        public void Dispose()
        {
            if (_native is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Warn("NativeWebView dispose failed: " + ex.Message);
                }
            }
        }

        public Task EnsureCoreWebView2Async(object userData = null)
        {
            // 原生可用时提前创建控件，挂进视觉树即开始创建适配器；
            // 不可用时无副作用（降级渲染在首次 NavigateToString 时惰性准备）。
            if (NativeAvailable())
            {
                EnsureNative();
            }
            return Task.CompletedTask;
        }

        public CoreWebView2 CoreWebView2 => _core ??= new CoreWebView2(this);

        /// <summary>渲染 HTML 文档：原生路径 NavigateToString 真导航；降级路径解析为控件树。</summary>
        public void NavigateToString(string html)
        {
            _lastHtml = html ?? string.Empty;
            NavigateCurrent();
        }

        private void NavigateCurrent()
        {
            bool dark = CurrentDark();
            ApplyThemedBackground();
            string forced = ForcePreferredColorScheme(_lastHtml, dark);
            if (NativeAvailable())
            {
                EnsureNative();
                if (_nativeAdapterReady)
                {
                    try
                    {
                        _native.NavigateToString(forced, new Uri("about:blank"));
                    }
                    catch (Exception ex)
                    {
                        // 导航失败（如适配器中途销毁）不炸 UI：转降级路径兜底
                        Log.Warn("NativeWebView navigate failed, falling back: " + ex.Message);
                        _nativeSupportedCache = false;
                        RenderFallback(dark, forced);
                    }
                }
                else
                {
                    // 适配器尚未就绪（控件刚挂树）：暂存，AdapterCreated 后再导航
                    _pendingNativeHtml = forced;
                }
            }
            else
            {
                RenderFallback(dark, forced);
            }
        }

        private bool CurrentDark()
        {
            if (_preferredDark.HasValue)
            {
                return _preferredDark.Value;
            }
            try
            {
                return ForkPlusSettings.Default.Theme.IsDarkBase();
            }
            catch
            {
                return false;
            }
        }

        // ── 原生路径 ──

        /// <summary>进程级缓存：当前环境是否有可用的原生 WebView 引擎。
        /// headless 测试环境恒 false（证据截图链走降级渲染器）。</summary>
		private static bool NativeAvailable()
		{
			if (_nativeSupportedCache.HasValue)
			{
				return _nativeSupportedCache.Value;
			}
			bool ok = false;
			try
			{
				if (!IsHeadlessRuntime())
				{
					if (OperatingSystem.IsWindows())
					{
						ok = AdapterUsable(WebViewAdapterType.WebView2);
					}
					else if (OperatingSystem.IsMacOS())
					{
						ok = AdapterUsable(WebViewAdapterType.WkWebView);
					}
					else if (OperatingSystem.IsLinux())
					{
						// WPE 优先（离屏合成进视觉树），未装再试 WebKitGTK
						ok = AdapterUsable(WebViewAdapterType.WpeWebKit) || AdapterUsable(WebViewAdapterType.WebKitGtk);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Warn("NativeWebView availability probe failed: " + ex.Message);
				ok = false;
			}
			_nativeSupportedCache = ok;
			return ok;
		}

        /// <summary>headless 运行时探测：测试宿主（testhost* / *Tests）作为入口进程。
        /// 证据截图/像素断言必须走降级渲染器（原生 WebView 内容不进 Avalonia 渲染管线，截不到）。
        /// 注：Avalonia 12 移除了 IWindowingPlatform 服务定位，改按入口程序集判定，
        /// 正式运行（ForkPlus）恒 false，Avalonia.Headless 单测宿主恒 true。</summary>
        private static bool IsHeadlessRuntime()
        {
            try
            {
                string entry = Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;
                return entry.StartsWith("testhost", StringComparison.OrdinalIgnoreCase)
                    || entry.IndexOf(".Tests", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return true;
            }
        }

        private static bool AdapterUsable(WebViewAdapterType type)
        {
            try
            {
                var info = WebViewAdapterInfo.GetAdapterInfo(type);
                return info != null && info.IsSupported && info.IsInstalled;
            }
            catch
            {
                return false;
            }
        }

        private void EnsureNative()
        {
            if (_native != null)
            {
                return;
            }
            _native = new NativeWebView
            {
                // 主题底色（非 Transparent）：原生引擎首帧前的表面不再黑屏
                Background = Brushes.Transparent,
            };
            ApplyThemedBackground();
            _native.AdapterCreated += delegate
            {
                _nativeAdapterReady = true;
                string pending = _pendingNativeHtml;
                if (pending != null)
                {
                    _pendingNativeHtml = null;
                    try
                    {
                        _native.NavigateToString(pending, new Uri("about:blank"));
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("NativeWebView pending navigate failed: " + ex.Message);
                    }
                }
            };
            _native.NavigationCompleted += delegate(object sender, Avalonia.Controls.WebViewNavigationCompletedEventArgs e)
            {
                // 调用方全部用 ContextMenuRequested(Handled=true) 抑制右键菜单：
                // 原生路径在每次导航完成后注入 preventDefault（DOM 是新的，需逐次注入）
                if (_native != null)
                {
                    _ = TryInvokeScriptAsync("document.addEventListener('contextmenu',function(e){e.preventDefault();});");
                }
                CoreWebView2.RaiseNavigationCompleted(e.IsSuccess);
            };
            _native.WebMessageReceived += delegate(object sender, Avalonia.Controls.WebMessageReceivedEventArgs e)
            {
                CoreWebView2.RaiseWebMessageReceived(e.Body);
            };
            Content = _native;
        }

        private async Task TryInvokeScriptAsync(string script)
        {
            try
            {
                await _native.InvokeScript(script);
            }
            catch (Exception ex)
            {
                Log.Warn("NativeWebView script failed: " + ex.Message);
            }
        }

        // ── 降级路径 ──

        private void RenderFallback(bool dark, string forcedHtml)
        {
            if (_fallback == null)
            {
                _fallback = new ScrollViewer
                {
                    Background = Brushes.Transparent,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                };
            }
            string body = ExtractBody(forcedHtml);
            _fallback.Content = MarkdownHtmlRenderer.Render(body, dark, delegate (string message)
            {
                CoreWebView2.RaiseWebMessageReceived(message);
            });
            Content = _fallback;
            // 兼容桥：内容布局完成后触发 NavigationCompleted（延迟到渲染帧之后），
            // AiDevelopmentWindow 依赖此事件 + ExecuteScriptAsync(scrollHeight) 做气泡自动高度。
            Dispatcher.UIThread.Post(delegate
            {
                Dispatcher.UIThread.Post(delegate
                {
                    CoreWebView2.RaiseNavigationCompleted(success: true);
                }, DispatcherPriority.Render);
            }, DispatcherPriority.Render);
        }

        /// <summary>提取 &lt;body&gt; 内容；无 body 标签时按片段整体处理。</summary>
        private static string ExtractBody(string html)
        {
            int bodyStart = html.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
            if (bodyStart >= 0)
            {
                int openEnd = html.IndexOf('>', bodyStart);
                if (openEnd >= 0)
                {
                    int bodyEnd = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
                    if (bodyEnd > openEnd)
                    {
                        return html.Substring(openEnd + 1, bodyEnd - openEnd - 1);
                    }
                    return html.Substring(openEnd + 1);
                }
            }
            return html;
        }

        /// <summary>
        /// 脚本桥：原生路径转发真 JS；降级路径只翻译 WPF 调用方真实用到的两个脚本——
        /// "document.documentElement.scrollHeight"（量内容高度）与 "window.scrollTo(0,…)"（滚到底）。
        /// </summary>
        public Task<string> ExecuteScriptAsync(string script)
        {
            if (script != null && NativeAvailable() && _native != null && _nativeAdapterReady)
            {
                return ExecuteNativeScriptAsync(script);
            }
            return Task.FromResult(FallbackScript(script));
        }

        private async Task<string> ExecuteNativeScriptAsync(string script)
        {
            try
            {
                string result = await _native.InvokeScript(script);
                if (script.Contains("scrollHeight"))
                {
                    // 归一化引擎间差异：WebView2 返回 JSON 编码（"1234"带引号），
                    // WebKit 系可能返回裸值——统一解析为整数像素高度字符串。
                    string trimmed = (result ?? string.Empty).Trim().Trim('"');
                    return double.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out double h)
                        ? Math.Max(0, (int)h).ToString(CultureInfo.InvariantCulture)
                        : "0";
                }
                return result ?? "null";
            }
            catch (Exception ex)
            {
                Log.Warn("NativeWebView ExecuteScriptAsync failed: " + ex.Message);
                return FallbackScript(script);
            }
        }

        private string FallbackScript(string script)
        {
            if (script != null)
            {
                // 注意顺序：流式滚底脚本 "window.scrollTo(0, document.documentElement.scrollHeight)"
                // 同时含两个 token——必须先匹配 scrollTo（动作语义），否则只量高度不滚动
                // （回归测试 CoreWebView2_ExecuteScriptAsync_ScrollTo_ScrollsFallbackToBottom 锁住）。
                if (script.Contains("scrollTo"))
                {
                    ScrollToEnd();
                    return "true";
                }
                if (script.Contains("scrollHeight"))
                {
                    return MeasureContentHeight().ToString("0", CultureInfo.InvariantCulture);
                }
            }
            return "null";
        }

        /// <summary>当前内容完整高度（对应 DOM scrollHeight，无视视口裁剪）。降级路径专用。</summary>
        private double MeasureContentHeight()
        {
            if (_fallback?.Content is Control control)
            {
                double width = _fallback.Bounds.Width;
                if (width <= 0)
                {
                    width = control.DesiredSize.Width;
                }
                control.Measure(new Size(Math.Max(width, 50.0), double.PositiveInfinity));
                return control.DesiredSize.Height;
            }
            return 0.0;
        }

        /// <summary>滚动到底部（对应 window.scrollTo(0, scrollHeight)）。降级路径专用。</summary>
        public void ScrollToEnd()
        {
            if (_fallback != null)
            {
                double target = Math.Max(0.0, _fallback.Extent.Height - _fallback.Viewport.Height);
                _fallback.Offset = new Vector(0, target);
            }
        }

        /// <summary>
        /// 对 HTML 里的 @media (prefers-color-scheme: dark) 做文本级强制改写，让页面配色跟随
        /// 应用皮肤而非操作系统（NativeWebView 无 PreferredColorScheme 等价 API，且各引擎不一致；
        /// 此 CSS 技巧引擎无关，对 GitMmReferenceWindow / md-ai-output.css 的暗色块统一生效）：
        ///   强制暗："(prefers-color-scheme: dark)" → "(prefers-color-scheme: dark), (min-width: 0px)"（恒真）
        ///   强制亮："(prefers-color-scheme: dark)" → "(prefers-color-scheme: dark) and (max-width: 0.001px)"（恒假）
        /// 改写只动 token，原有括号结构保持合法。
        /// </summary>
        internal static string ForcePreferredColorScheme(string html, bool dark)
        {
            if (string.IsNullOrEmpty(html) || html.IndexOf("prefers-color-scheme", StringComparison.Ordinal) < 0)
            {
                return html;
            }
            const string token = "prefers-color-scheme: dark";
            const string tokenCompact = "prefers-color-scheme:dark";
            if (dark)
            {
                return html
                    .Replace(token, "prefers-color-scheme: dark), (min-width: 0px")
                    .Replace(tokenCompact, "prefers-color-scheme:dark), (min-width: 0px");
            }
            return html
                .Replace(token, "prefers-color-scheme: dark) and (max-width: 0.001px")
                .Replace(tokenCompact, "prefers-color-scheme:dark) and (max-width: 0.001px");
        }

        /// <summary>兼容层内部：记录 PreferredColorScheme 设置（调用方经 CoreWebView2.Profile）。</summary>
        internal void SetPreferredColorScheme(CoreWebView2PreferredColorScheme scheme)
        {
            _preferredDark = scheme switch
            {
                CoreWebView2PreferredColorScheme.Dark => true,
                CoreWebView2PreferredColorScheme.Light => false,
                _ => null,
            };
            // 表面底色跟随明暗偏好（亮暗切换时底色先于内容更新）
            ApplyThemedBackground();
            if (_lastHtml != null)
            {
                NavigateCurrent();
            }
        }
    }
}
