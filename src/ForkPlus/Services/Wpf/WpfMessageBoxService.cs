namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// WPF 平台的 <see cref="IMessageBoxService"/> 实现，委托给 <c>System.Windows.MessageBox</c>。
	/// 阶段 0 仅注册到 ServiceLocator，不替换现有调用点。
	/// </summary>
	public class WpfMessageBoxService : IMessageBoxService
	{
		public MessageBoxResult Show(
			string message,
			string title = null,
			MessageBoxButton buttons = MessageBoxButton.OK,
			MessageBoxImage icon = MessageBoxImage.None)
		{
			System.Windows.MessageBoxButton wpfButtons = MapButtons(buttons);
			System.Windows.MessageBoxImage wpfIcon = MapIcon(icon);
			System.Windows.MessageBoxResult wpfResult = System.Windows.MessageBox.Show(
				message, title ?? string.Empty, wpfButtons, wpfIcon);
			return MapResult(wpfResult);
		}

		private static System.Windows.MessageBoxButton MapButtons(MessageBoxButton buttons)
		{
			switch (buttons)
			{
				case MessageBoxButton.OKCancel:
					return System.Windows.MessageBoxButton.OKCancel;
				case MessageBoxButton.YesNo:
					return System.Windows.MessageBoxButton.YesNo;
				case MessageBoxButton.YesNoCancel:
					return System.Windows.MessageBoxButton.YesNoCancel;
				default:
					return System.Windows.MessageBoxButton.OK;
			}
		}

		private static System.Windows.MessageBoxImage MapIcon(MessageBoxImage icon)
		{
			switch (icon)
			{
				case MessageBoxImage.Information:
					return System.Windows.MessageBoxImage.Information;
				case MessageBoxImage.Warning:
					return System.Windows.MessageBoxImage.Warning;
				case MessageBoxImage.Error:
					return System.Windows.MessageBoxImage.Error;
				case MessageBoxImage.Question:
					return System.Windows.MessageBoxImage.Question;
				default:
					return System.Windows.MessageBoxImage.None;
			}
		}

		private static MessageBoxResult MapResult(System.Windows.MessageBoxResult result)
		{
			switch (result)
			{
				case System.Windows.MessageBoxResult.OK:
					return MessageBoxResult.OK;
				case System.Windows.MessageBoxResult.Cancel:
					return MessageBoxResult.Cancel;
				case System.Windows.MessageBoxResult.Yes:
					return MessageBoxResult.Yes;
				case System.Windows.MessageBoxResult.No:
					return MessageBoxResult.No;
				default:
					return MessageBoxResult.None;
			}
		}
	}
}
