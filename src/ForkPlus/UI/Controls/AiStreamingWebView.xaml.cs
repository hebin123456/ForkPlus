using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ForkPlus.Biturbo;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Utils.Http;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// v3.9.0：统一的 AI 流式 Markdown 渲染控件。封装 WebView2 + 节流渲染 + 滚动跟随 + CSS/markdown 转换。
	/// AiTextResultWindow / AiCodeReviewWindow 用此控件替换各自重复的流式渲染代码。
	/// AiDevelopmentWindow 复用本控件的静态 GetCss() / ConvertMarkdownToHtml() 方法。
	///
	/// 流式渲染模式：调用方 StartStreaming() → 每个 chunk 调 AppendChunk() → 完成后 RenderFinal()。
	/// 渲染节流（StreamingRenderIntervalMs）：避免每个 chunk 都触发 markdown→html→NavigateToString 造成卡顿。
	/// 滚动跟随：用户在底部附近时自动滚到底部跟随新内容；用户主动上滚时不打断阅读。
	///   NavigateToString 会重置 DOM 滚动位置，所以通过 HTML scroll 事件 postMessage 上报"是否在底部"，
	///   C# 端维护 _streamingUserAtBottom 状态，渲染前快照决定是否滚到底部。
	/// </summary>
	public partial class AiStreamingWebView : UserControl
	{
		// ── 静态共享：CSS 缓存 + Markdown→HTML 转换（所有 AI 窗口共用） ──

		private static string _cachedCss;

		/// <summary>读取 md-ai-output.css 嵌入资源（静态缓存）。所有 AI 窗口共用同一份 CSS。</summary>
		public static string GetCss()
		{
			if (_cachedCss != null)
			{
				return _cachedCss;
			}
			try
			{
				Assembly executingAssembly = Assembly.GetExecutingAssembly();
				string name = "ForkPlus.Assets.md-ai-output.css";
				using Stream stream = executingAssembly.GetManifestResourceStream(name);
				using StreamReader streamReader = new StreamReader(stream);
				_cachedCss = streamReader.ReadToEnd();
				return _cachedCss;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to read CSS resource", ex);
				return string.Empty;
			}
		}

		/// <summary>Markdown → HTML（通过 Biturbo 库）。所有 AI 窗口共用同一转换逻辑。</summary>
		public static GitCommandResult<string> ConvertMarkdownToHtml(string markdown)
		{
			return BtRequest.Run(() => default(BtMdToHtmlResult), delegate(ref BtMdToHtmlResult x)
			{
				return Bt.bt_md_to_html(markdown, ref x);
			}, delegate(ref BtMdToHtmlResult x)
			{
				return GitCommandResult<string>.Success(x.html.GetUtf8String());
			}, delegate(ref BtMdToHtmlResult x)
			{
				Bt.bt_release_md_to_html(ref x);
			});
		}

		/// <summary>构建 HTML 文档外壳：CSS + body + 可选 scroll 上报脚本。</summary>
		public static string BuildHtmlDocument(string bodyHtml, bool includeScrollScript = false)
		{
			string scrollScript = includeScrollScript
				? "<script>(function(){function s(){var st=document.documentElement.scrollTop||document.body.scrollTop;var sh=document.documentElement.scrollHeight||document.body.scrollHeight;var ch=document.documentElement.clientHeight;var at=ch<=0||(st+ch>=sh-80);window.chrome.webview.postMessage('scroll-at-bottom:'+(at?'1':'0'));}window.addEventListener('scroll',s,{passive:true});window.addEventListener('load',s);if(document.readyState==='complete'||document.readyState==='interactive'){s();}})();</script>"
				: "";
			return "<!DOCTYPE html>\n<html>\n<head><meta charset='utf-8'><style>" + GetCss() + "\n</style></head>\n<body>" + bodyHtml + "\n" + scrollScript + "\n</body>\n</html>";
		}

		// ── 实例流式渲染状态 ──

		private StringBuilder _streamingMarkdown;
		private readonly object _streamingLock = new object();
		private DateTime _lastStreamingRenderUtc = DateTime.MinValue;
		private const int StreamingRenderIntervalMs = 400;
		private bool _streamingActive;
		private bool _pendingStreamingScrollToEnd;
		private bool _streamingUserAtBottom = true;

		/// <summary>非 scroll-at-bottom 的 web 消息转发给调用方处理（如 AiCodeReviewWindow 的 suggestion 按钮）。</summary>
		public event Action<string> WebMessageReceived;

		/// <summary>流式渲染时每帧更新已接收字数，调用方可用于状态栏展示。</summary>
		public event Action<int> StreamingProgress;

		public AiStreamingWebView()
		{
			InitializeComponent();
		}

		/// <summary>暴露内部 WebView2 供调用方做自定义渲染（如 AiCodeReviewWindow 的 RenderAiReviewOutput）。</summary>
		public WebView2 WebView => AiResponseWebView;

		/// <summary>暴露 BusyIndicator 供调用方控制加载动画显隐。</summary>
		public ProgressBar BusyIndicatorBar => BusyIndicator;

		/// <summary>暴露 Fallback 控件供调用方设置错误标题/消息。</summary>
		public FallbackUserControl Fallback => AiResponseFallback;

		/// <summary>异步初始化 WebView2：创建环境、禁用右键菜单、监听导航完成和 web 消息。</summary>
		public async Task InitializeAsync()
		{
			try
			{
				await AiResponseWebView.EnsureCoreWebView2Async(await WebView2EnvironmentHelper.GetEnvironmentAsync());
				UpdateTheme();
				AiResponseWebView.CoreWebView2.ContextMenuRequested += delegate(object s, CoreWebView2ContextMenuRequestedEventArgs e)
				{
					e.Handled = true;
				};
				AiResponseWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
				// 流式渲染时 NavigateToString 会重置滚动位置。监听导航完成事件，
				// 如果渲染前用户在底部附近（_pendingStreamingScrollToEnd），就自动滚到底部。
				AiResponseWebView.CoreWebView2.NavigationCompleted += delegate(object s, CoreWebView2NavigationCompletedEventArgs e)
				{
					if (!e.IsSuccess || !_pendingStreamingScrollToEnd)
					{
						return;
					}
					_pendingStreamingScrollToEnd = false;
					try
					{
						AiResponseWebView.CoreWebView2.ExecuteScriptAsync("window.scrollTo(0, document.documentElement.scrollHeight || document.body.scrollHeight)");
					}
					catch (Exception ex)
					{
						Log.Warn("Streaming scroll-to-end failed: " + ex.Message);
					}
				};
			}
			catch (Exception ex)
			{
				Log.Error("AiStreamingWebView WebView2 init failed", ex);
				ShowError(ex.Message);
			}
		}

		/// <summary>根据当前主题更新 WebView2 配色（Dark/Light）。</summary>
		public void UpdateTheme()
		{
			if (AiResponseWebView?.CoreWebView2 != null)
			{
				AiResponseWebView.CoreWebView2.Profile.PreferredColorScheme =
					ForkPlus.Settings.ForkPlusSettings.Default.Theme.IsDarkBase()
						? CoreWebView2PreferredColorScheme.Dark
						: CoreWebView2PreferredColorScheme.Light;
			}
		}

		/// <summary>开始流式渲染：重置 markdown 缓冲和节流计时器。</summary>
		public void StartStreaming()
		{
			_streamingMarkdown = new StringBuilder();
			_lastStreamingRenderUtc = DateTime.MinValue;
			_streamingActive = true;
			_streamingUserAtBottom = true;
			_pendingStreamingScrollToEnd = false;
		}

		/// <summary>恢复流式渲染但保留已有缓冲内容（用于部分重试：旧内容 + 新 chunk 继续渲染）。</summary>
		public void ResumeStreaming()
		{
			lock (_streamingLock)
			{
				if (_streamingMarkdown == null)
				{
					_streamingMarkdown = new StringBuilder();
				}
			}
			_lastStreamingRenderUtc = DateTime.MinValue;
			_streamingActive = true;
			_streamingUserAtBottom = true;
			_pendingStreamingScrollToEnd = false;
		}

		/// <summary>追加流式 chunk：写入缓冲 + 节流触发渲染。由调用方在 AI 请求的 onChunk 中调用。</summary>
		public void AppendChunk(string chunk)
		{
			if (string.IsNullOrEmpty(chunk) || !_streamingActive)
			{
				return;
			}
			lock (_streamingLock)
			{
				_streamingMarkdown?.Append(chunk);
			}
			int lengthSoFar;
			lock (_streamingLock)
			{
				lengthSoFar = _streamingMarkdown?.Length ?? 0;
			}
			Dispatcher.Async(delegate { TryRenderStreamingPreview(lengthSoFar); });
		}

		/// <summary>渲染最终内容并停止流式。如果传入 finalMarkdown 则覆盖渲染，否则渲染当前缓冲。</summary>
		public void RenderFinal(string finalMarkdown = null)
		{
			_streamingActive = false;
			string md = finalMarkdown;
			if (string.IsNullOrEmpty(md))
			{
				lock (_streamingLock)
				{
					md = _streamingMarkdown?.ToString() ?? "";
				}
			}
			if (!string.IsNullOrEmpty(md))
			{
				RenderMarkdown(md, scrollToEnd: true);
			}
		}

		/// <summary>停止流式渲染（取消/出错时调用，阻止已排队的渲染任务写入 WebView）。</summary>
		public void StopStreaming()
		{
			_streamingActive = false;
		}

		/// <summary>获取当前 markdown 缓冲内容（用于 Copy 等操作）。</summary>
		public string GetMarkdown()
		{
			lock (_streamingLock)
			{
				return _streamingMarkdown?.ToString() ?? "";
			}
		}

		/// <summary>流式是否处于活动状态。</summary>
		public bool IsStreamingActive => _streamingActive;

		/// <summary>直接导航到自定义 HTML（供 AiCodeReviewWindow 等做复杂 HTML 渲染，不含 scroll 脚本）。</summary>
		public void NavigateToHtml(string html)
		{
			try
			{
				AiResponseWebView.NavigateToString(html);
				AiResponseWebView.Show();
			}
			catch (Exception ex)
			{
				Log.Warn("AiStreamingWebView navigate failed: " + ex.Message);
			}
		}

		/// <summary>显示错误信息（HTML 转义 + 红色文字）。</summary>
		public void ShowError(string message)
		{
			_streamingActive = false;
			string escaped = WebUtility.HtmlEncode(message ?? "");
			string html = "<!DOCTYPE html><html><head><meta charset='utf-8'><style>" + GetCss() + "</style></head><body><p style='color:#d33'>" + escaped + "</p></body></html>";
			try
			{
				AiResponseWebView.NavigateToString(html);
				AiResponseWebView.Show();
			}
			catch (Exception ex)
			{
				Log.Warn("AiStreamingWebView ShowError navigate failed: " + ex.Message);
			}
		}

		/// <summary>显示 WebView、隐藏 BusyIndicator 和 Fallback。</summary>
		public void ShowContent()
		{
			AiResponseWebView.Show();
			BusyIndicator.Collapse();
			AiResponseFallback.Collapse();
		}

		/// <summary>显示 BusyIndicator 加载动画。</summary>
		public void ShowBusy()
		{
			BusyIndicator.Show();
		}

		/// <summary>隐藏 BusyIndicator 加载动画。</summary>
		public void HideBusy()
		{
			BusyIndicator.Collapse();
		}

		/// <summary>显示 Fallback 错误面板（标题 + 消息），隐藏 WebView。</summary>
		public void ShowFallback(string title, string message)
		{
			AiResponseWebView.Collapse();
			BusyIndicator.Collapse();
			AiResponseFallback.Show();
			AiResponseFallback.FallbackTitle = title;
			AiResponseFallback.FallbackMessage = message;
		}

		// ── 内部实现 ──

		/// <summary>节流后的实时预览渲染：把当前已收到的 Markdown 转为 HTML 并写入 WebView。</summary>
		private void TryRenderStreamingPreview(int lengthSoFar)
		{
			if (!_streamingActive || AiResponseWebView?.CoreWebView2 == null)
			{
				return;
			}
			DateTime now = DateTime.UtcNow;
			if (now - _lastStreamingRenderUtc < TimeSpan.FromMilliseconds(StreamingRenderIntervalMs))
			{
				return;
			}
			_lastStreamingRenderUtc = now;
			StreamingProgress?.Invoke(lengthSoFar);
			string md;
			lock (_streamingLock)
			{
				md = _streamingMarkdown?.ToString() ?? "";
			}
			if (string.IsNullOrEmpty(md))
			{
				return;
			}
			RenderMarkdown(md, scrollToEnd: _streamingUserAtBottom);
		}

		/// <summary>把 Markdown 渲染为 HTML 并导航到 WebView。scrollToEnd=true 时渲染后自动滚到底部。</summary>
		private void RenderMarkdown(string markdown, bool scrollToEnd)
		{
			string body;
			try
			{
				GitCommandResult<string> htmlResult = ConvertMarkdownToHtml(markdown);
				body = htmlResult.Succeeded ? htmlResult.Result : WebUtility.HtmlEncode(markdown);
			}
			catch (Exception ex)
			{
				Log.Warn("AiStreamingWebView markdown render failed: " + ex.Message);
				body = WebUtility.HtmlEncode(markdown);
			}
			if (scrollToEnd)
			{
				_pendingStreamingScrollToEnd = true;
			}
			string html = BuildHtmlDocument(body, includeScrollScript: true);
			try
			{
				AiResponseWebView.NavigateToString(html);
				AiResponseWebView.Show();
				BusyIndicator.Collapse();
			}
			catch (Exception ex)
			{
				Log.Warn("AiStreamingWebView navigate failed: " + ex.Message);
			}
		}

		/// <summary>处理 WebView 消息：scroll-at-bottom 内部消费，其余转发给 WebMessageReceived 事件。</summary>
		private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
		{
			string message = e.TryGetWebMessageAsString();
			if (message == null)
			{
				return;
			}
			if (message.StartsWith("scroll-at-bottom:", StringComparison.Ordinal))
			{
				string value = message.Substring("scroll-at-bottom:".Length);
				_streamingUserAtBottom = value == "1";
				return;
			}
			WebMessageReceived?.Invoke(message);
		}
	}
}
