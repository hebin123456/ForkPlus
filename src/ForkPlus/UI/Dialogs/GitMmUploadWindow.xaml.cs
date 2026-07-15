using System.Collections.Generic;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class GitMmUploadWindow : ForkPlusDialogWindow
	{
		public string[] UploadArgs { get; private set; }

		public GitMmUploadWindow(string workspacePath)
		{
			InitializeComponent();
			base.DialogTitle = Translate("Upload git mm");
			base.DialogDescription = Translate("Upload git mm changes for review");
			base.SubmitButtonTitle = Translate("Upload");
			WorkspacePathTextBlock.Text = workspacePath ?? "";
			WorkspacePathTextBlock.ToolTip = WorkspacePathTextBlock.Text;
			ForceUploadWarningImage.ToolTip = Translate("Force upload even if git mm reports safety checks.");
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			RestoreDialogOptions();
			InitializeCommandPreviewHandlers();
			UploadArgs = CreateArgs();
			RefreshCommandPreview();
			base.Loaded += delegate
			{
				Dispatcher.BeginInvoke(new System.Action(RefreshCommandPreview), System.Windows.Threading.DispatcherPriority.Loaded);
			};
		}

		protected override void OnSubmit()
		{
			UploadArgs = CreateArgs();
			SaveDialogOptions();
			base.OnSubmit();
		}

		private string[] CreateArgs()
		{
			List<string> args = new List<string> { "upload" };
			if (ForceUploadCheckBox.IsChecked.GetValueOrDefault())
			{
				args.Add("-f");
			}
			if (AssumeYesCheckBox.IsChecked.GetValueOrDefault())
			{
				args.Add("-y");
			}
			string title = CommitTitleTextBox.Text.Trim();
			if (!string.IsNullOrWhiteSpace(title))
			{
				args.Add("-T");
				args.Add(title);
			}
			AddTextArg(args, "--topic", TopicTextBox.Text);
			AddTextArg(args, "-R", ReviewersTextBox.Text);
			AddTextArg(args, "--cc", CcTextBox.Text);
			AddFlag(args, CurrentBranchOnlyCheckBox, "--cbr");
			AddFlag(args, HeadCheckBox, "--head");
			AddFlag(args, ReadyCheckBox, "--ready");
			AddFlag(args, WipCheckBox, "--wip");
			AddFlag(args, NoUpdateManifestCheckBox, "-N");
			return args.ToArray();
		}

		private static void AddTextArg(List<string> args, string flag, string value)
		{
			string trimmedValue = value?.Trim();
			if (!string.IsNullOrWhiteSpace(trimmedValue))
			{
				args.Add(flag);
				args.Add(trimmedValue);
			}
		}

		private static void AddFlag(List<string> args, System.Windows.Controls.CheckBox checkBox, string flag)
		{
			if (checkBox.IsChecked.GetValueOrDefault())
			{
				args.Add(flag);
			}
		}

		private void RestoreDialogOptions()
		{
			ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
			CommitTitleTextBox.Text = settings.GetDialogOption("upload.title");
			TopicTextBox.Text = settings.GetDialogOption("upload.topic");
			ReviewersTextBox.Text = settings.GetDialogOption("upload.reviewers");
			CcTextBox.Text = settings.GetDialogOption("upload.cc");
			ForceUploadCheckBox.IsChecked = IsDialogOptionChecked(settings, "upload.force");
			AssumeYesCheckBox.IsChecked = IsDialogOptionChecked(settings, "upload.assumeYes", defaultValue: true);
			CurrentBranchOnlyCheckBox.IsChecked = IsDialogOptionChecked(settings, "upload.currentBranchOnly");
			HeadCheckBox.IsChecked = IsDialogOptionChecked(settings, "upload.head");
			ReadyCheckBox.IsChecked = IsDialogOptionChecked(settings, "upload.ready");
			WipCheckBox.IsChecked = IsDialogOptionChecked(settings, "upload.wip");
			NoUpdateManifestCheckBox.IsChecked = IsDialogOptionChecked(settings, "upload.noUpdateManifest");
		}

		private static bool IsDialogOptionChecked(ForkPlusSettings.GitMmSettings settings, string key, bool defaultValue = false)
		{
			return string.Equals(settings.GetDialogOption(key, defaultValue ? "true" : "false"), "true", System.StringComparison.OrdinalIgnoreCase);
		}

		private void InitializeCommandPreviewHandlers()
		{
			CommitTitleTextBox.TextChanged += delegate { RefreshCommandPreview(); };
			TopicTextBox.TextChanged += delegate { RefreshCommandPreview(); };
			ReviewersTextBox.TextChanged += delegate { RefreshCommandPreview(); };
			CcTextBox.TextChanged += delegate { RefreshCommandPreview(); };
			foreach (System.Windows.Controls.CheckBox checkBox in new System.Windows.Controls.CheckBox[]
			{
				ForceUploadCheckBox,
				AssumeYesCheckBox,
				CurrentBranchOnlyCheckBox,
				HeadCheckBox,
				ReadyCheckBox,
				WipCheckBox,
				NoUpdateManifestCheckBox
			})
			{
				checkBox.Checked += delegate { RefreshCommandPreview(); };
				checkBox.Unchecked += delegate { RefreshCommandPreview(); };
			}
		}

		private void RefreshCommandPreview()
		{
			if (CommandPreviewTextBlock != null)
			{
				string cmd = GitMmCommandPreviewHelper.Format(CreateArgs());
				CommandPreviewTextBlock.Text = cmd;
				// 鼠标悬停显示完整命令文本（预览区可能因 MaxHeight 截断）
				CommandPreviewTextBlock.ToolTip = cmd;
			}
		}

		private void SaveDialogOptions()
		{
			ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
			Dictionary<string, string> dialogOptions = new Dictionary<string, string>(settings.DialogOptions, System.StringComparer.OrdinalIgnoreCase);
			dialogOptions["upload.title"] = CommitTitleTextBox.Text.Trim();
			dialogOptions["upload.topic"] = TopicTextBox.Text.Trim();
			dialogOptions["upload.reviewers"] = ReviewersTextBox.Text.Trim();
			dialogOptions["upload.cc"] = CcTextBox.Text.Trim();
			SaveCheckBox(dialogOptions, "upload.force", ForceUploadCheckBox);
			SaveCheckBox(dialogOptions, "upload.assumeYes", AssumeYesCheckBox);
			SaveCheckBox(dialogOptions, "upload.currentBranchOnly", CurrentBranchOnlyCheckBox);
			SaveCheckBox(dialogOptions, "upload.head", HeadCheckBox);
			SaveCheckBox(dialogOptions, "upload.ready", ReadyCheckBox);
			SaveCheckBox(dialogOptions, "upload.wip", WipCheckBox);
			SaveCheckBox(dialogOptions, "upload.noUpdateManifest", NoUpdateManifestCheckBox);
			ForkPlusSettings.Default.GitMm = new ForkPlusSettings.GitMmSettings(
				settings.Workspaces,
				settings.ActiveWorkspace,
				settings.ActiveSubrepo,
				settings.ActiveSubrepos,
				settings.SubrepoOrders,
				settings.VisibleSubrepos,
				settings.CommandOutputCollapsed,
				settings.CommandOutputHeight,
				settings.CommandHistory,
				settings.UploadLinks,
				settings.UploadLinksByWorkspace,
				settings.SyncJobs,
				settings.StartBranch,
				settings.InitUrl,
				settings.InitManifest,
				settings.InitBranch,
				settings.InitGroup,
				dialogOptions);
			ForkPlusSettings.Default.Save();
		}

		private static void SaveCheckBox(Dictionary<string, string> dialogOptions, string key, System.Windows.Controls.CheckBox checkBox)
		{
			dialogOptions[key] = checkBox.IsChecked.GetValueOrDefault() ? "true" : "false";
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
