using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class GitMmStartWindow : ForkPlusDialogWindow
	{
		private readonly GitMmSubrepoItem[] _subrepos;

		private readonly HashSet<string> _selectedSubrepoPaths = new HashSet<string>();

		public string[] StartArgs { get; private set; }

		protected override bool IsSubmitAllowed
		{
			get
			{
				if (string.IsNullOrWhiteSpace(BranchNameTextBox.Text))
				{
					return false;
				}
				if (!AllSubreposCheckBox.IsChecked.GetValueOrDefault() && _selectedSubrepoPaths.Count == 0)
				{
					return false;
				}
				return base.IsSubmitAllowed;
			}
		}

		public GitMmStartWindow(IEnumerable<GitMmSubrepoItem> subrepos, GitMmSubrepoItem selectedSubrepo)
		{
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
				Dispatcher.BeginInvoke(new System.Action(RefreshCommandPreview), System.Windows.Threading.DispatcherPriority.Loaded);
				BranchNameTextBox.Focus();
			};
		}

		protected override void OnSubmit()
		{
			StartArgs = CreateArgs();
			SaveDialogOptions();
			base.OnSubmit();
		}

		private string[] CreateArgs()
		{
			List<string> args = new List<string> { "start", BranchNameTextBox.Text.Trim() };
			args.Add("-j");
			args.Add(SelectedJobs().ToString());
			string grepMode = SelectedComboBoxText(GrepModeComboBox);
			if (!string.IsNullOrWhiteSpace(grepMode) && grepMode != "mixed")
			{
				args.Add("-g");
				args.Add(grepMode);
			}
			if (AllSubreposCheckBox.IsChecked.GetValueOrDefault())
			{
				args.Add("--all");
			}
			else
			{
				args.AddRange(_subrepos
					.Where((GitMmSubrepoItem subrepo) => _selectedSubrepoPaths.Contains(subrepo.Path))
					.Select((GitMmSubrepoItem subrepo) => subrepo.Name));
			}
			if (AllowTagCheckBox.IsChecked.GetValueOrDefault())
			{
				args.Add("--allow-tag");
			}
			if (AllowCommitCheckBox.IsChecked.GetValueOrDefault())
			{
				args.Add("--allow-commit");
			}
			if (AllowNoTrackCheckBox.IsChecked.GetValueOrDefault())
			{
				args.Add("--allow-no-track");
			}
			if (HeadCheckBox.IsChecked.GetValueOrDefault())
			{
				args.Add("--head");
			}
			return args.ToArray();
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
				CommandPreviewTextBlock.Text = GitMmCommandPreviewHelper.Format(CreateArgs());
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
