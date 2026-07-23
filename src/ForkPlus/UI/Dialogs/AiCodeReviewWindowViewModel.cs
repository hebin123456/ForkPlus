using System.Collections.Generic;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// AiCodeReviewWindow 的 ViewModel（零 WPF 依赖）。
	/// 继承 AiStreamingMarkdownViewModel 获得流式渲染状态/协议/Markdown转换/CSS。
	/// 额外承载：模型下拉加载标志。
	///
	/// View（AiCodeReviewWindow.xaml.cs）保留的职责：
	/// - _isClosed 标志（窗口关闭后阻止渲染，View 在调 VM.ShouldRenderNow 前先检查）
	/// - suggestion 的 UI 交互（preview/apply 通过 WebView2 postMessage）
	/// - 文件 review 切换 + diff HTML 缓存（_fileReviewHtmlCache，依赖 WebView2 渲染）
	/// - OpenAiService 直接调用（View 可访问 internal 类型，VM 不可）
	/// - WebView2 实例操作 / Dispatcher / UI 状态切换 / ComboBox 填充
	/// </summary>
	public class AiCodeReviewWindowViewModel : AiStreamingMarkdownViewModel
	{
		/// <summary>模型下拉列表是否已完成后台加载（防止重复填充 ComboBox）。</summary>
		public bool ModelListLoaded { get; set; }
	}
}
