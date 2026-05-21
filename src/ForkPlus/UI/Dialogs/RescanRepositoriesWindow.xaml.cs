using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;

namespace ForkPlus.UI.Dialogs
{
	public partial class RescanRepositoriesWindow : ForkPlusDialogWindow
	{
		private bool _isCloseAllowed = true;

		public RescanRepositoriesWindow()
		{
			InitializeComponent();
			base.DialogTitle = "Rescan repositories in source folder";
			string text = RepositoryManager.Instance.SourceDirs.Map((string x) => "'" + x + "'").Joined(", ");
			base.DialogDescription = "Do you want to rescan repositories in '" + text + "'?";
			base.SubmitButtonTitle = "Rescan";
			ResetDeletedCheckbox.IsChecked = false;
		}

		protected override async void OnSubmit()
		{
			_isCloseAllowed = false;
			bool reset = ResetDeletedCheckbox.IsChecked.GetValueOrDefault();
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, "Scanning repositories...");
			await Task.Run(delegate
			{
				new RescanUserRepositoriesCommand().Execute(reset);
			});
			if (reset)
			{
				ForkPlusSettings.Default.RepositoryManagerTreeViewExpandedItems = null;
			}
			_isCloseAllowed = true;
			SetStatus(ForkPlusDialogStatus.Success, "Done");
			await Task.Delay(1500);
			CloseWithOk();
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			if (!_isCloseAllowed)
			{
				e.Cancel = true;
			}
			else
			{
				base.OnClosing(e);
			}
		}

	}
}
