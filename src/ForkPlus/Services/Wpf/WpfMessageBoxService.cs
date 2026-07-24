using ForkPlus.UI.Dialogs;
using ForkPlus.UI;

namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// 平台无关的 <see cref="IMessageBoxService"/> 实现，委托给 <c>MessageBoxWindow</c>。
	/// 阶段 4.5：WPF System.Windows.MessageBox → Avalonia MessageBoxWindow.ShowDialog()。
	/// </summary>
	public class WpfMessageBoxService : IMessageBoxService
	{
		public MessageBoxResult Show(
			string message,
			string title = null,
			MessageBoxButton buttons = MessageBoxButton.OK,
			MessageBoxImage icon = MessageBoxImage.None)
		{
			bool showCancelButton = buttons != MessageBoxButton.OK;
			// MessageBoxButton.YesNo / YesNoCancel：Submit 按钮文案用 "Yes"，Cancel 用 "No"（仅 YesNo）或 "Cancel"（YesNoCancel）。
			// MessageBoxButton.OK / OKCancel：Submit 用 "OK"，Cancel 用 "Cancel"。
			string submitTitle = (buttons == MessageBoxButton.YesNo || buttons == MessageBoxButton.YesNoCancel) ? "Yes" : "OK";
			string cancelTitle = buttons == MessageBoxButton.YesNo ? "No" : "Cancel";
			bool showWarningIcon = icon == MessageBoxImage.Warning || icon == MessageBoxImage.Error;

			MessageBoxWindow window = new MessageBoxWindow(
				title ?? string.Empty,
				message,
				submitTitle,
				cancelTitle,
				showCancelButton: showCancelButton,
				width: 600.0,
				showWarningIcon: showWarningIcon);

			bool submitted = window.ShowDialog().GetValueOrDefault();

			switch (buttons)
			{
				case MessageBoxButton.YesNo:
					return submitted ? MessageBoxResult.Yes : MessageBoxResult.No;
				case MessageBoxButton.YesNoCancel:
					return submitted ? MessageBoxResult.Yes : MessageBoxResult.Cancel;
				case MessageBoxButton.OKCancel:
					return submitted ? MessageBoxResult.OK : MessageBoxResult.Cancel;
				default:
					return MessageBoxResult.OK;
			}
		}
	}
}
