using Avalonia.Controls;
using Avalonia.Threading;
using System.Collections.Generic;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class GitMmSyncWindow : ForkPlusDialogWindow
	{
		public string[] SyncArgs { get; private set; }

		public int CheckoutJobs { get; private set; } = 4;

		public GitMmSyncWindow(string workspacePath)
		{
			InitializeComponent();
			base.DialogTitle = Translate("Sync git mm");
			base.DialogDescription = Translate("Sync git mm workspace");
			base.SubmitButtonTitle = Translate("Sync");
			WorkspacePathTextBlock.Text = workspacePath ?? "";
			WorkspacePathTextBlock.ToolTip = WorkspacePathTextBlock.Text;
			ForceSyncWarningImage.ToolTip = Translate("Discard local sync state and force git mm to resync projects.");
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			SelectCheckoutJobs(ForkPlusSettings.Default.GitMm.SyncJobs);
			SelectJobs(FetchJobsComboBox, ForkPlusSettings.Default.GitMm.GetDialogOption("sync.fetchJobs"), defaultValue: 8);
			RestoreDialogOptions();
			InitializeCommandPreviewHandlers();
			SyncArgs = CreateArgs();
			RefreshCommandPreview();
			base.Loaded += delegate
			{
				Dispatcher.BeginInvoke(new System.Action(RefreshCommandPreview), System.Windows.Threading.DispatcherPriority.Loaded);
			};
		}

		protected override void OnSubmit()
		{
			CheckoutJobs = SelectedCheckoutJobs();
			SyncArgs = CreateArgs();
			SaveDialogOptions();
			base.OnSubmit();
		}

		private string[] CreateArgs()
		{
			List<string> args = new List<string> { "sync" };
			args.Add("-J");
			args.Add(SelectedCheckoutJobs().ToString());
			args.Add("-j");
			args.Add(SelectedFetchJobs().ToString());
			if (ForceSyncCheckBox.IsChecked.GetValueOrDefault())
			{
				args.Add("--force-sync");
			}
			if (DetachCheckBox.IsChecked.GetValueOrDefault())
			{
				args.Add("-d");
			}
			if (UpdateManifestCheckBox.IsChecked.GetValueOrDefault())
			{
				args.Add("--update-manifest");
			}
			AddFlag(args, LocalOnlyCheckBox, "-l");
			AddFlag(args, NetworkOnlyCheckBox, "-n");
			AddFlag(args, FailFastCheckBox, "--fail-fast");
			AddFlag(args, AllBranchesCheckBox, "-a");
			AddFlag(args, TagsCheckBox, "--tags");
			AddFlag(args, FetchSubmodulesCheckBox, "--fetch-submodules");
			AddFlag(args, ForceCheckoutCheckBox, "--force-checkout");
			AddFlag(args, ForceFetchCheckBox, "--force-fetch");
			AddFlag(args, ForceRemoveDirtyCheckBox, "--force-remove-dirty");
			return args.ToArray();
		}

		private static void AddFlag(List<string> args, CheckBox checkBox, string flag)
		{
			if (checkBox.IsChecked.GetValueOrDefault())
			{
				args.Add(flag);
			}
		}

		private void SelectCheckoutJobs(string value)
		{
			SelectJobs(CheckoutJobsComboBox, value, defaultValue: 4);
		}

		private static void SelectJobs(ComboBox comboBox, string value, int defaultValue)
		{
			int jobs = defaultValue;
			if (int.TryParse(value, out int parsedJobs))
			{
				jobs = ClampCheckoutJobs(parsedJobs);
			}
			comboBox.SelectedIndex = jobs - 1;
		}

		private int SelectedCheckoutJobs()
		{
			return SelectedJobs(CheckoutJobsComboBox, defaultValue: 4);
		}

		private int SelectedFetchJobs()
		{
			return SelectedJobs(FetchJobsComboBox, defaultValue: 8);
		}

		private static int SelectedJobs(ComboBox comboBox, int defaultValue)
		{
			if (comboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out int jobs))
			{
				return ClampCheckoutJobs(jobs);
			}
			return defaultValue;
		}

		private static int ClampCheckoutJobs(int jobs)
		{
			return System.Math.Max(1, System.Math.Min(10, jobs));
		}

		private void RestoreDialogOptions()
		{
			ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
			ForceSyncCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.forceSync");
			DetachCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.detach");
			UpdateManifestCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.updateManifest");
			LocalOnlyCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.localOnly");
			NetworkOnlyCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.networkOnly");
			FailFastCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.failFast");
			AllBranchesCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.allBranches");
			TagsCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.tags");
			FetchSubmodulesCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.fetchSubmodules");
			ForceCheckoutCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.forceCheckout");
			ForceFetchCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.forceFetch");
			ForceRemoveDirtyCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.forceRemoveDirty");
		}

		private static bool IsDialogOptionChecked(ForkPlusSettings.GitMmSettings settings, string key)
		{
			return string.Equals(settings.GetDialogOption(key), "true", System.StringComparison.OrdinalIgnoreCase);
		}

		private void SaveDialogOptions()
		{
			ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
			Dictionary<string, string> dialogOptions = new Dictionary<string, string>(settings.DialogOptions, System.StringComparer.OrdinalIgnoreCase);
			dialogOptions["sync.fetchJobs"] = SelectedFetchJobs().ToString();
			SaveCheckBox(dialogOptions, "sync.forceSync", ForceSyncCheckBox);
			SaveCheckBox(dialogOptions, "sync.detach", DetachCheckBox);
			SaveCheckBox(dialogOptions, "sync.updateManifest", UpdateManifestCheckBox);
			SaveCheckBox(dialogOptions, "sync.localOnly", LocalOnlyCheckBox);
			SaveCheckBox(dialogOptions, "sync.networkOnly", NetworkOnlyCheckBox);
			SaveCheckBox(dialogOptions, "sync.failFast", FailFastCheckBox);
			SaveCheckBox(dialogOptions, "sync.allBranches", AllBranchesCheckBox);
			SaveCheckBox(dialogOptions, "sync.tags", TagsCheckBox);
			SaveCheckBox(dialogOptions, "sync.fetchSubmodules", FetchSubmodulesCheckBox);
			SaveCheckBox(dialogOptions, "sync.forceCheckout", ForceCheckoutCheckBox);
			SaveCheckBox(dialogOptions, "sync.forceFetch", ForceFetchCheckBox);
			SaveCheckBox(dialogOptions, "sync.forceRemoveDirty", ForceRemoveDirtyCheckBox);
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
				CheckoutJobs.ToString(),
				settings.StartBranch,
				settings.InitUrl,
				settings.InitManifest,
				settings.InitBranch,
				settings.InitGroup,
				dialogOptions);
			ForkPlusSettings.Default.Save();
		}

		private static void SaveCheckBox(Dictionary<string, string> dialogOptions, string key, CheckBox checkBox)
		{
			dialogOptions[key] = checkBox.IsChecked.GetValueOrDefault() ? "true" : "false";
		}

		private void InitializeCommandPreviewHandlers()
		{
			CheckoutJobsComboBox.SelectionChanged += delegate { RefreshCommandPreview(); };
			FetchJobsComboBox.SelectionChanged += delegate { RefreshCommandPreview(); };
			foreach (CheckBox checkBox in new CheckBox[]
			{
				ForceSyncCheckBox,
				DetachCheckBox,
				UpdateManifestCheckBox,
				LocalOnlyCheckBox,
				NetworkOnlyCheckBox,
				FailFastCheckBox,
				AllBranchesCheckBox,
				TagsCheckBox,
				FetchSubmodulesCheckBox,
				ForceCheckoutCheckBox,
				ForceFetchCheckBox,
				ForceRemoveDirtyCheckBox
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

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
