namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// Avalonia 平台的 <see cref="IMessageBoxService"/> 实现。
	/// 阶段 4.5：原 WPF System.Windows.MessageBox 已不可用（UseWPF=false）。
	/// 此处保留为占位实现，返回默认 OK，后续里程碑接入 Avalonia 原生对话框。
	/// </summary>
	public class WpfMessageBoxService : IMessageBoxService
	{
		public MessageBoxResult Show(
			string message,
			string title = null,
			MessageBoxButton buttons = MessageBoxButton.OK,
			MessageBoxImage icon = MessageBoxImage.None)
		{
			// TODO(4.5+): 接入 Avalonia 原生对话框（需 async 改造调用方）。
			// 当前同步签名约束下，仅记录消息并返回 OK/Yes 以不阻塞编译与基本流程。
			Log.Info($"[WpfMessageBoxService] {title ?? "Message"}: {message}");
			return buttons == MessageBoxButton.YesNo ? MessageBoxResult.Yes : MessageBoxResult.OK;
		}
	}
}
