using System.Windows.Media.Imaging;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// 远端同步冲突预检结果对话框。
	/// 支持"先弹框显示检测中、后台检测完成后更新结果"的异步流程：构造时 status 传 null 进入 Checking 态，
	/// 检测完成后调 <see cref="UpdateResult"/> 刷新为最终三态结果。
	/// </summary>
	public partial class ForkSyncCheckWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;
		private readonly Remote _upstreamRemote;
		private readonly LocalBranch _localBranch;
		private readonly string _branchName;
		private ForkSyncStatus? _status;

		public ForkSyncCheckWindow(
			RepositoryUserControl repositoryUserControl,
			Remote upstreamRemote,
			LocalBranch localBranch,
			string branchName,
			ForkSyncStatus? status)
		{
			_repositoryUserControl = repositoryUserControl;
			_upstreamRemote = upstreamRemote;
			_localBranch = localBranch;
			_branchName = branchName;
			_status = status;
			InitializeComponent();
			DialogTitle = PreferencesLocalization.Current("Remote Sync Status");
			DialogDescription = string.Empty;
			ConfigureForStatus();
		}

		/// <summary>检测中（status 为 null）时不允许提交，按钮自动禁用。</summary>
		protected override bool IsSubmitAllowed => _status.HasValue && base.IsSubmitAllowed;

		/// <summary>
		/// 后台检测完成后调用，把对话框从"检测中"更新为最终结果。
		/// </summary>
		public void UpdateResult(ForkSyncStatus status)
		{
			_status = status;
			ConfigureForStatus();
			UpdateSubmitButton();
		}

		private void ConfigureForStatus()
		{
			string upstreamRef = _upstreamRemote.Name + "/" + _branchName;
			if (!_status.HasValue)
			{
				// 检测中：按钮由 IsSubmitAllowed 守卫自动禁用，提示用户正在检测
				StatusIcon.Source = null;
				StatusText.Text = PreferencesLocalization.FormatCurrent("Checking remote sync: {0}/{1}", _upstreamRemote.Name, _branchName);
				DetailText.Text = PreferencesLocalization.Current("Checking... Please wait.");
				SubmitButtonTitle = PreferencesLocalization.Current("OK");
				ShowCancelButton = false;
				UpdateSubmitButton();
				return;
			}
			switch (_status.Value)
			{
				case ForkSyncStatus.SafeToPush:
					StatusIcon.Source = new BitmapImage(SuccessIcon);
					StatusText.Text = PreferencesLocalization.Current("Safe to push");
					DetailText.Text = PreferencesLocalization.FormatCurrent(
						"'{0}' is up-to-date with {1}. You can push without syncing.",
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
						"{0} has new commits that would conflict with '{1}'. You must pull and resolve the conflicts before pushing.",
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
			// 检测中不应触发任何操作（按钮已禁用，兜底防御）
			if (!_status.HasValue)
			{
				return;
			}
			// 对于"安全 push"和"无法判断"等无需操作的状态，点击主按钮即关闭
			if (_status.Value == ForkSyncStatus.SafeToPush
				|| _status.Value == ForkSyncStatus.NoUpstreamBranch
				|| _status.Value == ForkSyncStatus.Unknown)
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
