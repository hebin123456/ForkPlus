using System.Windows.Media.Imaging;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// Fork 工作流同步冲突预检结果对话框。
	/// 根据 <see cref="ForkSyncStatus"/> 显示三态结果，并在需要同步时提供"拉取并解决"按钮。
	/// </summary>
	public partial class ForkSyncCheckWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;
		private readonly Remote _upstreamRemote;
		private readonly LocalBranch _localBranch;
		private readonly ForkSyncStatus _status;
		private readonly string _branchName;

		public ForkSyncCheckWindow(
			RepositoryUserControl repositoryUserControl,
			Remote upstreamRemote,
			LocalBranch localBranch,
			string branchName,
			ForkSyncStatus status)
		{
			_repositoryUserControl = repositoryUserControl;
			_upstreamRemote = upstreamRemote;
			_localBranch = localBranch;
			_branchName = branchName;
			_status = status;
			InitializeComponent();
			DialogTitle = PreferencesLocalization.Current("Fork Sync Status");
			ConfigureForStatus();
		}

		private void ConfigureForStatus()
		{
			string upstreamRef = _upstreamRemote.Name + "/" + _branchName;
			switch (_status)
			{
				case ForkSyncStatus.SafeToPush:
					StatusIcon.Source = new BitmapImage(SuccessIcon);
					StatusText.Text = PreferencesLocalization.Current("Safe to push");
					DetailText.Text = PreferencesLocalization.FormatCurrent(
						"'{0}' is up-to-date with {1}. You can push to your fork without syncing.",
						_localBranch.Name, upstreamRef);
					SubmitButtonTitle = PreferencesLocalization.Current("OK");
					ShowCancelButton = false;
					break;
				case ForkSyncStatus.ShouldSyncNoConflict:
					StatusIcon.Source = new BitmapImage(WarningIcon);
					StatusText.Text = PreferencesLocalization.Current("Recommended to sync");
					DetailText.Text = PreferencesLocalization.FormatCurrent(
						"{0} has new commits that are not in '{1}', but a merge would not produce conflicts. You can push now, but it's recommended to pull first to stay in sync.",
						upstreamRef, _localBranch.Name);
					SubmitButtonTitle = PreferencesLocalization.Current("Pull from upstream");
					CancelButtonTitle = PreferencesLocalization.Current("Skip and push later");
					break;
				case ForkSyncStatus.MustSyncWithConflict:
					StatusIcon.Source = new BitmapImage(ErrorIcon);
					StatusText.Text = PreferencesLocalization.Current("Conflicts detected");
					DetailText.Text = PreferencesLocalization.FormatCurrent(
						"{0} has new commits that would conflict with '{1}'. You must pull and resolve the conflicts before pushing to your fork.",
						upstreamRef, _localBranch.Name);
					SubmitButtonTitle = PreferencesLocalization.Current("Pull and resolve");
					CancelButtonTitle = PreferencesLocalization.Current("Close");
					break;
				case ForkSyncStatus.NoUpstreamBranch:
					StatusIcon.Source = new BitmapImage(WarningIcon);
					StatusText.Text = PreferencesLocalization.Current("Upstream branch not found");
					DetailText.Text = PreferencesLocalization.FormatCurrent(
						"No remote branch '{0}' found on the upstream remote. Please verify the upstream remote and branch name.",
						upstreamRef);
					SubmitButtonTitle = PreferencesLocalization.Current("OK");
					ShowCancelButton = false;
					break;
				default:
					StatusIcon.Source = new BitmapImage(WarningIcon);
					StatusText.Text = PreferencesLocalization.Current("Unable to determine sync status");
					DetailText.Text = PreferencesLocalization.FormatCurrent(
						"Could not determine whether '{0}' would conflict with {1}. Please pull manually to verify.",
						_localBranch.Name, upstreamRef);
					SubmitButtonTitle = PreferencesLocalization.Current("OK");
					ShowCancelButton = false;
					break;
			}
		}

		protected override void OnSubmit()
		{
			// 对于"安全 push"和"无法判断"等无需操作的状态，点击主按钮即关闭
			if (_status == ForkSyncStatus.SafeToPush
				|| _status == ForkSyncStatus.NoUpstreamBranch
				|| _status == ForkSyncStatus.Unknown)
			{
				base.OnSubmit();
				return;
			}

			// 对于需要同步的状态，点击主按钮打开 Pull 窗口让用户拉取 upstream 并解决冲突
		// Commands 是 RepositoryUserControl 的静态属性，须用类型名访问（不能用实例引用）
		RepositoryUserControl.Commands?.ShowPullWindow?.Execute(_repositoryUserControl, null);
		base.OnSubmit();
		}
	}
}
