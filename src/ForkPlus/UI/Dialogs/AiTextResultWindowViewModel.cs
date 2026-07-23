using ForkPlus.Jobs;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// AiTextResultWindow 的 ViewModel（零 WPF 依赖）。
	/// 继承 AiStreamingMarkdownViewModel 获得流式渲染状态/协议/Markdown转换/CSS。
	/// 额外承载：模型下拉加载标志。
	///
	/// View（AiTextResultWindow.xaml.cs）职责：
	/// - 持有调用方传入的 requestAction 委托（签名 Action&lt;AiTextResultWindow, JobMonitor&gt;）
	/// - RunRequest 时调 _viewModel.ResetForNewRequest() + Task.Run 执行 requestAction
	/// - requestAction 内调 View.OnChunk → _viewModel.OnChunk → Dispatcher 渲染
	/// - WebView2 实例操作 / Dispatcher / UI 状态切换 / ComboBox 填充
	/// </summary>
	public class AiTextResultWindowViewModel : AiStreamingMarkdownViewModel
	{
		/// <summary>模型下拉列表是否已完成后台加载（防止重复填充 ComboBox）。</summary>
		public bool ModelListLoaded { get; set; }
	}
}
