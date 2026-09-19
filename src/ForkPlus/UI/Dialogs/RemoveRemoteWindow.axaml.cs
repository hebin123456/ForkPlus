using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Threading;

namespace ForkPlus.UI.Dialogs
{
	public partial class RemoveRemoteWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly Remote _remote;

		public RemoveRemoteWindow(RepositoryUserControl repositoryUserControl, Remote remote)
		{
			InitializeComponent();
			_repositoryUserControl = repositoryUserControl;
			_remote = remote;
			base.DialogTitle = PreferencesLocalization.Current("Delete Remote");
			base.DialogDescription = PreferencesLocalization.Current("Delete remote repository reference");
			base.SubmitButtonTitle = PreferencesLocalization.Current("Delete");
			RemoteNameTextBlock.Text = remote.Name;
			// InitializeComponent 期间 AddCommandPreview 已执行，但此时 _remote 尚未赋值，
			// 导致首次 RefreshCommandPreview 返回 null 折叠了预览。此处补刷一次以显示默认命令。
			RefreshCommandPreview();
		}

		protected override string GetCommandPreview()
		{
			if (_remote == null)
			{
				return null;
			}
			// 与 RemoveRemoteGitCommand 实际执行的 git remote remove <name> 一致。
			return "git remote remove " + _remote.Name;
		}

		protected override void OnSubmit()
		{
			DisableEditableControls();
			GitModule gitModule = _repositoryUserControl.GitModule;
			string name = PreferencesLocalization.FormatCurrent("Delete remote '{0}'", _remote.Name);
			_repositoryUserControl.AddUndoable(name, delegate(JobMonitor monitor)
			{
				GitCommandResult result = new RemoveRemoteGitCommand().Execute(gitModule, _remote, monitor);
				base.Dispatcher.Post(delegate
				{
					Close(result);
				});
				return result;
			}, JobFlags.SaveToLog);
		}
	}
}