using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using ForkPlus.Git;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.UserControls.RepositorySettings;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Dialogs
{
	public partial class RepositorySettingsWindow : ForkPlusDialogWindow
	{
		private readonly GitModule _gitModule;

		private readonly RepositoryData _repositoryData;

		public RepositorySettingsWindow(GitModule gitModule, RepositoryData repositoryData)
		{
			_gitModule = gitModule;
			_repositoryData = repositoryData;
			base.ShowLogo = false;
			// 仓库设置窗口不需要顶部标题区域；该标题会覆盖通用按钮/内容区域。
			base.ShowHeader = false;
			InitializeComponent();
			base.ShowCancelButton = false;
			base.SubmitButtonTitle = PreferencesLocalization.Current("Close");
			base.SizeToContent = global::Avalonia.Controls.SizeToContent.WidthAndHeight;
			Initialize();
		}

		public void Initialize()
		{
			GeneralUserControl.Initialize(_gitModule);
			IssueTrackerUserControl.Initialize(this, _gitModule);
			CommitTemplateUserControl.Initialize(_gitModule);
			CustomCommandsUserControl.InitializeLocal(this, _gitModule, _repositoryData);
		}

		protected override void OnSubmit()
		{
			base.OnSubmit();
			GeneralUserControl.Save();
			IssueTrackerUserControl.Save();
			CommitTemplateUserControl.Save();
			CustomCommandsUserControl.Save();
		}

		protected override void OnClosing(global::Avalonia.Controls.WindowClosingEventArgs e)
		{
			base.OnClosing(e);
			GeneralUserControl.Save();
			IssueTrackerUserControl.Save();
			CommitTemplateUserControl.Save();
			CustomCommandsUserControl.Save();
		}

	}
}
