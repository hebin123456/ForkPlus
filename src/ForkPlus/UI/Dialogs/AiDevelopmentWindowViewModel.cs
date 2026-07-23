namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// AiDevelopmentWindow 的 ViewModel（零 WPF 依赖）。
	/// 继承 AiStreamingMarkdownViewModel 获得流式缓冲+节流协议+Markdown转换+CSS。
	/// 额外承载：模型下拉加载标志。
	///
	/// 与 AiTextResult/AiCodeReview 的差异：
	/// - 多对话气泡模式（_streamingWebView 动态创建，非 XAML 声明），流式 WebView 由 View 管理
	/// - 无 scroll-at-bottom 跟踪（气泡用自动高度 + 总是 ScrollToEnd），不用 VM 的滚动协议
	/// - 无显式 _streamingActive 标志（通过 _streamingWebView==null 停止渲染），不用 VM.StopStreaming
	/// - 工具调用循环（MaxToolRounds）+ 多轮对话历史 + 文件变更/撤销 全留 View（业务逻辑）
	///
	/// 使用的 VM API：OnChunk(追加) / ShouldRenderNow(节流) / GetMarkdownSnapshot / GetFinalMarkdown /
	/// ClearStreamingBuffer(工具轮次间清缓冲) + 基类静态方法(ConvertMarkdownToHtml/GetCss/RenderMarkdownToHtmlDocument)
	/// </summary>
	public class AiDevelopmentWindowViewModel : AiStreamingMarkdownViewModel
	{
		/// <summary>模型下拉列表是否已完成后台加载（防止重复填充 ComboBox）。</summary>
		public bool ModelListLoaded { get; set; }
	}
}
