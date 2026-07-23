using Avalonia.Controls;
using Avalonia.Threading;
using System;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class RenameStashWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly StashRevision _stash;

		// 阶段 3：承接输入校验与命令预览的纯业务逻辑（零 WPF using）。OnSubmit 的 JobQueue/Dispatcher 耦合暂留 View。
		private readonly RenameStashWindowViewModel _viewModel;

		public Sha? OutResultSha { get; private set; }

		protected override bool IsSubmitAllowed
		{
			get
			{
				// 副作用留 View（原 override 即如此），纯判断委托 VM。
				SetStatus(ForkPlusDialogStatus.None, string.Empty);
				_viewModel.StashName = StashNameTextBox.Text;
				return _viewModel.IsSubmitAllowed;
			}
		}

		public RenameStashWindow(RepositoryUserControl repositoryUserControl, StashRevision stash)
		{
			_repositoryUserControl = repositoryUserControl;
			_stash = stash;
			_viewModel = new RenameStashWindowViewModel(stash.Message, stash.ReflogName);
			InitializeComponent();
			base.DialogTitle = Translate("Rename Stash");
			base.DialogDescription = Translate("Update stash message");
			base.SubmitButtonTitle = Translate("Rename");
			StashNameTextBox.Text = stash.Message;
		StashNameTextBox.SelectAll();
		UpdateSubmitButton();
		RefreshCommandPreview();
	}

	protected override string GetCommandPreview()
	{
		_viewModel.StashName = StashNameTextBox.Text;
		return _viewModel.CommandPreview;
	}

	protected override void OnSubmit()
	{
		GitModule gitModule = _repositoryUserControl.GitModule;
		StashRevision stash = _stash;
		string newMessage = StashNameTextBox.Text;
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, Translate("Updating message..."));
			_repositoryUserControl.JobQueue.Add(string.Format(Translate("Rename stash '{0}'"), _stash.Message), delegate(JobMonitor monitor)
			{
				GitCommandResult<Sha> renameResult = new RenameStashGitCommand().Execute(gitModule, stash.ReflogName, newMessage, monitor);
				OutResultSha = renameResult.Result;
				base.Dispatcher.Async(delegate
				{
					Close(renameResult.ToGitCommandResult());
				});
			}, JobFlags.ShowOnToolbar);
		}

		private void StashName_TextChanged(object sender, TextChangedEventArgs e)
	{
		UpdateSubmitButton();
		RefreshCommandPreview();
	}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
