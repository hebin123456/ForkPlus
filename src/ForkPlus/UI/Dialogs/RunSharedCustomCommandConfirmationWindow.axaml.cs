using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Dialogs
{
	public partial class RunSharedCustomCommandConfirmationWindow : ForkPlusDialogWindow
	{

		public bool TrustThisRepository => TrustThisRepositoryCheckBox.IsChecked.GetValueOrDefault();

		public RunSharedCustomCommandConfirmationWindow(string repositoryName)
		{
			InitializeComponent();
			// Migration note：WPF 构造期 chrome 已就绪；Avalonia 12 延迟初始化，
			// 经 Customize* pending 机制在 chrome 就绪后应用（构造期安全）。
			CustomizeTitleTextBlock(delegate(TextBlock t)
			{
				t.TextTrimming = TextTrimming.CharacterEllipsis;
				t.TextWrapping = TextWrapping.Wrap;
				t.MaxHeight = 80.0;
			});
			CustomizeDescriptionTextBlock(delegate(TextBlock t)
			{
				t.TextTrimming = TextTrimming.CharacterEllipsis;
				t.TextWrapping = TextWrapping.Wrap;
				t.MaxHeight = 80.0;
			});
			base.DialogTitle = PreferencesLocalization.FormatCurrent("The custom command has come from the '{0}' repository", repositoryName);
			base.DialogDescription = PreferencesLocalization.Current("You should only run custom commands from trustworthy repositories. Do you really want to run it?");
			base.SubmitButtonTitle = PreferencesLocalization.Current("Run");
			base.CancelButtonTitle = PreferencesLocalization.Current("Cancel");
			base.ShowCancelButton = true;
			base.Width = 600.0;
			base.ShowWarningIcon = true;
			TrustThisRepositoryCheckBox.Content = PreferencesLocalization.FormatCurrent("Trust custom commands in '{0}'", repositoryName);
		}

	}
}
