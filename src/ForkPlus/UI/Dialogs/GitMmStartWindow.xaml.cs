using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Collections.Generic;
using System.Linq;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class GitMmStartWindow : ForkPlusDialogWindow
	{
		private readonly GitMmSubrepoItem[] _subrepos;

		private readonly HashSet<string> _selectedSubrepoPaths = new HashSet<string>();

		// 阶段 3：承接 git mm start 多参数构建 + IsSubmitAllowed + 命令预览。
		// GitMmSubrepoItem→Name 投影由 View 完成（避免 VM 引用 ForkPlus.UI.UserControls 命名空间）。
		// RestoreDialogOptions/SaveDialogOptions/SubreposDropDownButton 留 View。
		private readonly GitMmStartWindowViewModel _viewModel;

		public string[] StartArgs { get; private set; }

		protected override bool IsSubmitAllowed
		{
			get
			{
				PushSelectionToViewModel();
				return _viewModel.IsSubmitAllowed && base.IsSubmitAllowed;
			}
		}

		private void PushSelectionToViewModel()
		{
			_viewModel.BranchName = BranchNameTextBox.Text;
			_viewModel.Jobs = SelectedJobs();
			_viewModel.GrepMode = SelectedComboBoxText(GrepModeComboBox) ?? "mixed";
			_viewModel.AllSubrepos = AllSubreposCheckBox.IsChecked.GetValueOrDefault();
			_viewModel.SelectedSubrepoNames = _subrepos
				.Where((GitMmSubrepoItem subrepo) => _selectedSubrepoPaths.Contains(subrepo.Path))
				.Select((GitMmSubrepoItem subrepo) => subrepo.Name)
				.ToArray();
			_viewModel.AllowTag = AllowTagCheckBox.IsChecked.GetValueOrDefault();
			_viewModel.AllowCommit = AllowCommitCheckBox.IsChecked.GetValueOrDefault();
			_viewModel.AllowNoTrack = AllowNoTrackCheckBox.IsChecked.GetValueOrDefault();
			_viewModel.Head = HeadCheckBox.IsChecked.GetValueOrDefault();
		}

		public GitMmStartWindow(IEnumerable<GitMmSubrepoItem> subrepos, GitMmSubrepoItem selectedSubrepo)
		{
			_viewModel = new GitMmStartWindowViewModel();
			InitializeComponent();
			_subrepos = subrepos?.ToArray() ?? new GitMmSubrepoItem[0];
			if (selectedSubrepo != null)
			{
				_selectedSubrepoPaths.Add(selectedSubrepo.Path);
			}
			base.DialogTitle = Translate("Start git mm");
			base.DialogDescription = Translate("Start development branch for git mm sub repositories");
			base.SubmitButtonTitle = Translate("Start");
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			RestoreDialogOptions();
			InitializeCommandPreviewHandlers();
			RefreshSubreposButton();
			UpdateSubmitButton();
			RefreshCommandPreview();
			base.Loaded += delegate
			{
				Dispatcher.Post(RefreshCommandPreview);
				BranchNameTextBox.Focus();
			};
		}

		protected override void OnSubmit()
		{
			PushSelectionToViewModel();
			StartArgs = _viewModel.CreateArgs();
			SaveDialogOptions();
			base.OnSubmit();
		}

		private int SelectedJobs()
		{
			if (JobsComboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out int jobs))
			{
				return System.Math.Max(1, System.Math.Min(10, jobs));
			}
			return 8;
		}

		private static string SelectedComboBoxText(ComboBox comboBox)
		{
			return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
		}

		private void RestoreDialogOptions()
		{
			ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
			BranchNameTextBox.Text = !string.IsNullOrWhiteSpace(settings.StartBranch) ? settings.StartBranch : "develop";
			SelectComboBoxItem(JobsComboBox, settings.GetDialogOption("start.jobs", "8"));
			SelectComboBoxItem(GrepModeComboBox, settings.GetDialogOption("start.grepMode", "mixed"));
			AllSubreposCheckBox.IsChecked = IsDialogOptionChecked(settings, "start.allSubrepos", defaultValue: true);
			AllowTagCheckBox.IsChecked = IsDialogOptionChecked(settings, "start.allowTag");
			AllowCommitCheckBox.IsChecked = IsDialogOptionChecked(settings, "start.allowCommit");
			AllowNoTrackCheckBox.IsChecked = IsDialogOptionChecked(settings, "start.allowNoTrack");
			HeadCheckBox.IsChecked = IsDialogOptionChecked(settings, "start.head");
		}

		private static void SelectComboBoxItem(ComboBox comboBox, string value)
		{
			foreach (ComboBoxItem item in comboBox.Items.OfType<ComboBoxItem>())
			{
				if (string.Equals(item.Content?.ToString(), value, System.StringComparison.OrdinalIgnoreCase))
				{
					comboBox.SelectedItem = item;
					return;
				}
			}
			if (comboBox.Items.Count > 0)
			{
				comboBox.SelectedIndex = 0;
			}
		}

		private static bool IsDialogOptionChecked(ForkPlusSettings.GitMmSettings settings, string key, bool defaultValue = false)
		{
			return string.Equals(settings.GetDialogOption(key, defaultValue ? "true" : "false"), "true", System.StringComparison.OrdinalIgnoreCase);
		}

		private void SubreposDropDownButton_Click(object sender, RoutedEventArgs e)
		{
			ContextMenu contextMenu = new ContextMenu();
			foreach (GitMmSubrepoItem subrepo in _subrepos)
			{
				MenuItem menuItem = new MenuItem
				{
					Header = new TextBlock
					{
						Text = subrepo.Name
					},
					IsCheckable = true,
					IsChecked = _selectedSubrepoPaths.Contains(subrepo.Path),
					StaysOpenOnClick = true
				};
				menuItem.Checked += delegate
				{
					_selectedSubrepoPaths.Add(subrepo.Path);
					RefreshSubreposButton();
					UpdateSubmitButton();
					RefreshCommandPreview();
				};
				menuItem.Unchecked += delegate
				{
					_selectedSubrepoPaths.Remove(subrepo.Path);
					RefreshSubreposButton();
					UpdateSubmitButton();
					RefreshCommandPreview();
				};
				contextMenu.Items.Add(menuItem);
			}
			SubreposDropDownButton.ContextMenu = contextMenu;
			contextMenu.PlacementTarget = SubreposDropDownButton;
			contextMenu.IsOpen = true;
		}

		private void AllSubreposCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			RefreshSubreposButton();
			UpdateSubmitButton();
			RefreshCommandPreview();
		}

		private void BranchNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
			RefreshCommandPreview();
		}

		private void RefreshSubreposButton()
		{
			bool allSubrepos = AllSubreposCheckBox.IsChecked.GetValueOrDefault();
			SubreposDropDownButton.IsEnabled = !allSubrepos;
			if (allSubrepos)
			{
				SubreposDropDownButton.Content = Translate("All sub repositories");
				return;
			}
			if (_selectedSubrepoPaths.Count == 0)
			{
				SubreposDropDownButton.Content = Translate("Select sub repositories");
				return;
			}
			SubreposDropDownButton.Content = string.Join(", ", _subrepos
				.Where((GitMmSubrepoItem subrepo) => _selectedSubrepoPaths.Contains(subrepo.Path))
				.Select((GitMmSubrepoItem subrepo) => subrepo.Name));
		}

		private void InitializeCommandPreviewHandlers()
		{
			JobsComboBox.SelectionChanged += delegate { RefreshCommandPreview(); };
			GrepModeComboBox.SelectionChanged += delegate { RefreshCommandPreview(); };
			AllowTagCheckBox.Checked += delegate { RefreshCommandPreview(); };
			AllowTagCheckBox.Unchecked += delegate { RefreshCommandPreview(); };
			AllowCommitCheckBox.Checked += delegate { RefreshCommandPreview(); };
			AllowCommitCheckBox.Unchecked += delegate { RefreshCommandPreview(); };
			AllowNoTrackCheckBox.Checked += delegate { RefreshCommandPreview(); };
			AllowNoTrackCheckBox.Unchecked += delegate { RefreshCommandPreview(); };
			HeadCheckBox.Checked += delegate { RefreshCommandPreview(); };
			HeadCheckBox.Unchecked += delegate { RefreshCommandPreview(); };
		}

		private void RefreshCommandPreview()
		{
			if (CommandPreviewTextBlock != null)
			{
				PushSelectionToViewModel();
				string cmd = _viewModel.CommandPreview;
				CommandPreviewTextBlock.Text = cmd;
				// 鼠标悬停显示完整命令文本（预览区可能因 MaxHeight 截断）
				CommandPreviewTextBlock.ToolTip = cmd;
			}
		}

		private void SaveDialogOptions()
		{
			ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
			Dictionary<string, string> dialogOptions = new Dictionary<string, string>(settings.DialogOptions, System.StringComparer.OrdinalIgnoreCase);
			dialogOptions["start.jobs"] = SelectedJobs().ToString();
			dialogOptions["start.grepMode"] = SelectedComboBoxText(GrepModeComboBox) ?? "mixed";
			SaveCheckBox(dialogOptions, "start.allSubrepos", AllSubreposCheckBox);
			SaveCheckBox(dialogOptions, "start.allowTag", AllowTagCheckBox);
			SaveCheckBox(dialogOptions, "start.allowCommit", AllowCommitCheckBox);
			SaveCheckBox(dialogOptions, "start.allowNoTrack", AllowNoTrackCheckBox);
			SaveCheckBox(dialogOptions, "start.head", HeadCheckBox);
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
				BranchNameTextBox.Text.Trim(),
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

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
