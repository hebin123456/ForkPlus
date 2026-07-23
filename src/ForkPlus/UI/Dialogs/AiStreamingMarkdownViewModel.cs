using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using ForkPlus.Biturbo;
using ForkPlus.Git.Commands;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// AI 流式 Markdown 响应的 ViewModel 基类（零 WPF 依赖）。
	/// 承载三个 WebView2 AI 窗口（AiTextResult/AiCodeReview/AiDevelopment）共有的：
	/// 1. 流式缓冲 + 节流渲染协议（_streamingMarkdown/_streamingLock/_lastStreamingRenderUtc）
	/// 2. 滚动位置跟踪协议（_streamingUserAtBottom/_pendingStreamingScrollToEnd）
	/// 3. Markdown→HTML 转换（native Bt.bt_md_to_html）
	/// 4. CSS 加载（嵌入资源 md-ai-output.css，静态缓存）
	/// 5. HTML 文档构建（含滚动位置上报脚本）
	///
	/// View 职责（不在此类）：WebView2 实例创建/初始化/NavigateToString、Dispatcher 调度、
	/// Visibility/ProgressBar/StatusText 切换、ComboBox 填充、Clipboard、PreferencesLocalization。
	///
	/// 线程模型：OnChunk 由后台 job 线程调用（加锁追加），渲染判定 ShouldRenderNow/GetMarkdownSnapshot
	/// 由 UI 线程调用。View 收到 OnChunk 返回值后用 Dispatcher.Async 调度渲染。
	/// </summary>
	public abstract class AiStreamingMarkdownViewModel
	{
		private StringBuilder _streamingMarkdown;
		private readonly object _streamingLock = new object();
		private DateTime _lastStreamingRenderUtc = DateTime.MinValue;
		private bool _streamingActive;
		private bool _pendingStreamingScrollToEnd;
		private bool _streamingUserAtBottom = true;

		/// <summary>流式渲染节流间隔（毫秒）。首个 chunk 立即渲染，之后按此间隔节流。</summary>
		protected const int StreamingRenderIntervalMs = 400;

		private static string _cachedCss;

		/// <summary>当前流式是否处于活动状态（未完成/取消/出错）。</summary>
		public bool IsStreamingActive => _streamingActive;

		/// <summary>用户当前是否停留在页面底部（用于决定渲染后是否自动滚到底）。</summary>
		public bool StreamingUserAtBottom => _streamingUserAtBottom;

		/// <summary>重置为新一轮请求的初始状态（开始流式前调用）。</summary>
		public void ResetForNewRequest()
		{
			lock (_streamingLock)
			{
				_streamingMarkdown = new StringBuilder();
			}
			_lastStreamingRenderUtc = DateTime.MinValue;
			_streamingActive = true;
			_streamingUserAtBottom = true;
			_pendingStreamingScrollToEnd = false;
		}

		/// <summary>停止流式渲染（完成/取消/出错时调用，阻止后续渲染写入 WebView）。</summary>
		public void StopStreaming()
		{
			_streamingActive = false;
		}

		/// <summary>
		/// 流式 chunk 追加（由后台 job 线程调用，线程安全）。
		/// 返回 (shouldRender, lengthSoFar)：View 据此用 Dispatcher.Async 调度渲染。
		/// </summary>
		public (bool ShouldRender, int LengthSoFar) OnChunk(string chunk)
		{
			if (string.IsNullOrEmpty(chunk) || !_streamingActive)
			{
				return (false, 0);
			}
			lock (_streamingLock)
			{
				if (_streamingMarkdown == null)
				{
					_streamingMarkdown = new StringBuilder();
				}
				_streamingMarkdown.Append(chunk);
			}
			int lengthSoFar;
			lock (_streamingLock)
			{
				lengthSoFar = _streamingMarkdown.Length;
			}
			return (true, lengthSoFar);
		}

		/// <summary>
		/// 节流判定：是否应立即渲染。首个 chunk（_lastStreamingRenderUtc==MinValue）立即渲染，
		/// 之后每隔 StreamingRenderIntervalMs 渲染一次。判定通过时内部更新节流时间戳。
		/// </summary>
		public bool ShouldRenderNow()
		{
			if (!_streamingActive)
			{
				return false;
			}
			DateTime now = DateTime.UtcNow;
			if (now - _lastStreamingRenderUtc < TimeSpan.FromMilliseconds(StreamingRenderIntervalMs))
			{
				return false;
			}
			_lastStreamingRenderUtc = now;
			return true;
		}

		/// <summary>取当前流式 Markdown 的快照（加锁，供 UI 线程渲染用）。可能返回空字符串。</summary>
		public string GetMarkdownSnapshot()
		{
			lock (_streamingLock)
			{
				return _streamingMarkdown?.ToString() ?? "";
			}
		}

		/// <summary>WebView2 JS postMessage 上报的滚动位置更新（由 View 的 WebMessageReceived 转发）。</summary>
		public void SetUserAtBottom(bool atBottom)
		{
			_streamingUserAtBottom = atBottom;
		}

		/// <summary>标记本次渲染后需要滚到底部（渲染前调用，NavigationCompleted 时由 View 消费）。</summary>
		public void RequestScrollToEndIfNeeded()
		{
			if (_streamingUserAtBottom)
			{
				_pendingStreamingScrollToEnd = true;
			}
		}

		/// <summary>消费并清除"待滚到底部"标记（NavigationCompleted 事件中调用）。</summary>
		public bool ConsumeScrollToEndRequest()
		{
			if (!_pendingStreamingScrollToEnd)
			{
				return false;
			}
			_pendingStreamingScrollToEnd = false;
			return true;
		}

		/// <summary>取最终完整 Markdown（请求成功后用，可能为 null/空）。</summary>
		public string GetFinalMarkdown()
		{
			lock (_streamingLock)
			{
				return _streamingMarkdown?.ToString() ?? "";
			}
		}

		/// <summary>清空流式缓冲（切换到下一轮对话/气泡时调用）。</summary>
		public void ClearStreamingBuffer()
		{
			lock (_streamingLock)
			{
				_streamingMarkdown = null;
			}
			_lastStreamingRenderUtc = DateTime.MinValue;
		}

		// ── Markdown→HTML 转换 + CSS + HTML 文档构建（纯逻辑，三窗口共用） ──

		/// <summary>调 native Biturbo 库把 Markdown 转为 HTML 片段。</summary>
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

		/// <summary>读取嵌入资源 md-ai-output.css，带静态缓存（三窗口共享）。</summary>
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

		/// <summary>把 body HTML 片段包装为完整 HTML 文档（含 CSS）。</summary>
		public static string BuildHtmlDocument(string bodyHtml)
		{
			string css = GetCss();
			return "<!DOCTYPE html>\n<html>\n<head><meta charset='utf-8'><style>" + css + "\n</style></head>\n<body>" + bodyHtml + "\n</body>\n</html>";
		}

		/// <summary>把 Markdown 渲染为完整 HTML 文档（含 CSS），失败时回退为 HTML 转义纯文本。</summary>
		public static string RenderMarkdownToHtmlDocument(string markdown)
		{
			string body;
			try
			{
				GitCommandResult<string> htmlResult = ConvertMarkdownToHtml(markdown);
				body = htmlResult.Succeeded ? htmlResult.Result : WebUtility.HtmlEncode(markdown);
			}
			catch (Exception ex)
			{
				Log.Warn("AI markdown render failed: " + ex.Message);
				body = WebUtility.HtmlEncode(markdown);
			}
			return BuildHtmlDocument(body);
		}

		/// <summary>把 Markdown 渲染为完整 HTML 文档（含 CSS + 滚动位置上报脚本）。
		/// 供流式渲染窗口调用：WebView2 导航该 HTML 后，scroll 事件会通过 postMessage 上报用户是否在底部。</summary>
		public static string RenderMarkdownToHtmlDocumentWithScrollScript(string markdown)
		{
			string body;
			try
			{
				GitCommandResult<string> htmlResult = ConvertMarkdownToHtml(markdown);
				body = htmlResult.Succeeded ? htmlResult.Result : WebUtility.HtmlEncode(markdown);
			}
			catch (Exception ex)
			{
				Log.Warn("AI markdown render failed: " + ex.Message);
				body = WebUtility.HtmlEncode(markdown);
			}
			string css = GetCss();
			string scrollScript = BuildScrollScript();
			return "<!DOCTYPE html>\n<html>\n<head><meta charset='utf-8'><style>" + css + "\n</style></head>\n<body>" + body + "\n" + scrollScript + "\n</body>\n</html>";
		}

		/// <summary>构建错误信息的完整 HTML 文档（红色文本）。</summary>
		public static string BuildErrorHtmlDocument(string errorMessage)
		{
			string escaped = WebUtility.HtmlEncode(errorMessage ?? "");
			return "<!DOCTYPE html><html><head><meta charset='utf-8'><style>" + GetCss() + "</style></head><body><p style='color:#d33'>" + escaped + "</p></body></html>";
		}

		/// <summary>滚动位置上报脚本（注入 HTML，通过 postMessage 把"是否在底部"上报给 C# 端）。
		/// View 在 WebMessageReceived 中收到 'scroll-at-bottom:1/0' 后调用 SetUserAtBottom。</summary>
		public static string BuildScrollScript()
		{
			return "<script>"
				+ "(function(){"
				+ "function sendAtBottom(){"
				+ "var st=document.documentElement.scrollTop||document.body.scrollTop;"
				+ "var sh=document.documentElement.scrollHeight||document.body.scrollHeight;"
				+ "var ch=document.documentElement.clientHeight;"
				+ "var atBottom=ch<=0||(st+ch>=sh-80);"
				+ "window.chrome.webview.postMessage('scroll-at-bottom:'+(atBottom?'1':'0'));"
				+ "}"
				+ "window.addEventListener('scroll',sendAtBottom,{passive:true});"
				+ "window.addEventListener('load',sendAtBottom);"
				+ "if(document.readyState==='complete'||document.readyState==='interactive'){sendAtBottom();}"
				+ "})();"
				+ "</script>";
		}

		/// <summary>解析 WebView2 postMessage 是否为滚动位置上报，返回是否在底部（null 表示非该类消息）。</summary>
		public static bool? TryParseScrollMessage(string message)
		{
			const string prefix = "scroll-at-bottom:";
			if (message != null && message.StartsWith(prefix, StringComparison.Ordinal))
			{
				return message.Substring(prefix.Length) == "1";
			}
			return null;
		}
	}
}
