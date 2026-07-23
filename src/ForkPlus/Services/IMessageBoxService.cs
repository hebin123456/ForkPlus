namespace ForkPlus.Services
{
	/// <summary>
	/// 平台无关的消息框抽象（替换 WPF <c>System.Windows.MessageBox</c>）。
	/// 阶段 0 仅提供接口与 WPF 实现，现有 48 处 <c>MessageBox.Show</c> 调用点
	/// 将在阶段 2（Commands 层去 WPF 化）和阶段 3（ViewModel 抽取）逐步迁移到此接口。
	/// </summary>
	public interface IMessageBoxService
	{
		/// <summary>显示消息框，返回用户点击的按钮结果。</summary>
		/// <param name="message">消息正文。</param>
		/// <param name="title">窗口标题，为 null 时使用平台默认。</param>
		/// <param name="buttons">按钮组合，默认仅 OK。</param>
		/// <param name="icon">图标类型，默认无图标。</param>
		MessageBoxResult Show(
			string message,
			string title = null,
			MessageBoxButton buttons = MessageBoxButton.OK,
			MessageBoxImage icon = MessageBoxImage.None);
	}

	/// <summary>消息框按钮组合（与 WPF <c>MessageBoxButton</c> 对应，但平台无关）。</summary>
	public enum MessageBoxButton
	{
		OK = 0,
		OKCancel = 1,
		YesNo = 3,
		YesNoCancel = 4
	}

	/// <summary>消息框图标（与 WPF <c>MessageBoxImage</c> 对应，但平台无关）。</summary>
	public enum MessageBoxImage
	{
		None = 0,
		Information = 4,
		Warning = 2,
		Error = 3,
		Question = 0x20
	}

	/// <summary>消息框返回结果（与 WPF <c>MessageBoxResult</c> 对应，但平台无关）。</summary>
	public enum MessageBoxResult
	{
		None = 0,
		OK = 1,
		Cancel = 2,
		Yes = 6,
		No = 7
	}
}
