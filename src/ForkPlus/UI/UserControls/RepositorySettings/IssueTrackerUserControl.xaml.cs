// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls
// - using System.Windows.Markup → 移除
// - using System.Windows.Media → using Avalonia.Media（Brushes.Green/Red）
// - SelectionChangedEventArgs/TextChangedEventArgs → Avalonia.Controls 同名类型
// - Brushes.Green/Red → Avalonia.Media.Brushes.Green/Red（API 兼容）
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls.RepositorySettings
{
	public partial class IssueTrackerUserControl : UserControl
	{
		private ForkPlusDialogWindow _parentWindow;

		private GitModule _gitModule;

		private bool _saveRequired;

		private bool _updateInProgress;

		private ObservableCollection<BugtrackerRuleViewModel> _bugtrackers;

		private BugtrackerRuleViewModel SelectedBugtrackerRule => BugTrackerRulesListBox.SelectedItem as BugtrackerRuleViewModel;

		public IssueTrackerUserControl()
		{
			InitializeComponent();
		}

		public void Initialize(ForkPlusDialogWindow parentWindow, GitModule gitModule)
		{
			_parentWindow = parentWindow;
			_gitModule = gitModule;
			if (!ForkPlusSettings.Default.ShowBugtrackerLinks)
			{
				string title = Translate("Issue Tracker Integration is disabled");
				string message = Translate("Enable Issue Tracker Integration in Fork preferences");
				ContentContainer.ShowFallback(title, message);
				return;
			}
			IsEnabledCheckbox.IsChecked = gitModule.Settings.ShowBugtrackerLinks;
			RefreshContentFallback();
			_bugtrackers = LoadBugtrackers(gitModule);
			BugTrackerRulesListBox.ItemsSource = _bugtrackers;
			BugtrackerRuleViewModel bugtrackerRule = ((_bugtrackers.Count > 0) ? _bugtrackers[0] : null);
			UpdateSelection(bugtrackerRule);
		}

		public void Save()
		{
			if (_saveRequired)
			{
				BugtrackerLinkDefinition[] bugtrackers = _bugtrackers.ToArray().CompactMap((BugtrackerRuleViewModel x) => BugtrackerLinkDefinition.Create(x.Name, x.Level, x.RegexString, x.UrlString));
				new SetBugtrackerRulesGitCommand().Execute(_gitModule, bugtrackers);
				_saveRequired = false;
			}
		}

		private void IsEnabledCheckbox_Changed(object sender, RoutedEventArgs e)
		{
			_gitModule.Settings.ShowBugtrackerLinks = IsEnabledCheckbox.IsChecked.GetValueOrDefault();
			_gitModule.Settings.Save();
			RefreshContentFallback();
		}

		private void BugTrackerRulesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (SelectedBugtrackerRule == null)
			{
				ItemFallbackUserControl.Show();
				return;
			}
			_updateInProgress = true;
			ItemFallbackUserControl.Hide();
			NameTextBox.Text = SelectedBugtrackerRule.Name;
			LocalRadioButton.IsChecked = SelectedBugtrackerRule.Level == Level.Local;
			SharedRadioButton.IsChecked = SelectedBugtrackerRule.Level == Level.Shared;
			RegexTextBox.Text = SelectedBugtrackerRule.RegexString;
			UrlTextBox.Text = SelectedBugtrackerRule.UrlString;
			SampleMessageTextBox.Text = SelectedBugtrackerRule.SampleMessage;
			Level level = ((!SharedRadioButton.IsChecked.Value) ? Level.Local : Level.Shared);
			RuleLocationTextBlock.Text = GetBugtrackerRuleLocation(level);
			RefreshRegexStatus();
			RefreshSamplePreviewTextBlock();
			_updateInProgress = false;
		}

		private void NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!_updateInProgress && SelectedBugtrackerRule != null)
			{
				_saveRequired = true;
				SelectedBugtrackerRule.Name = NameTextBox.Text;
			}
		}

		private void RegexTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!_updateInProgress && SelectedBugtrackerRule != null)
			{
				_saveRequired = true;
				SelectedBugtrackerRule.RegexString = RegexTextBox.Text;
				RefreshRegexStatus();
				RefreshSamplePreviewTextBlock();
			}
		}

		private void LevelRadioButton_Changed(object sender, RoutedEventArgs e)
		{
			if (!_updateInProgress && SelectedBugtrackerRule != null)
			{
				_saveRequired = true;
				Level level = ((!SharedRadioButton.IsChecked.Value) ? Level.Local : Level.Shared);
				SelectedBugtrackerRule.Level = level;
				RuleLocationTextBlock.Text = GetBugtrackerRuleLocation(level);
			}
		}

		private void UrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!_updateInProgress && SelectedBugtrackerRule != null)
			{
				_saveRequired = true;
				SelectedBugtrackerRule.UrlString = UrlTextBox.Text;
				RefreshSamplePreviewTextBlock();
			}
		}

		private void SampleMessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!_updateInProgress && SelectedBugtrackerRule != null)
			{
				SelectedBugtrackerRule.SampleMessage = SampleMessageTextBox.Text;
				RefreshSamplePreviewTextBlock();
			}
		}

		private void NewRuleMenuItem_Click(object sender, RoutedEventArgs e)
		{
			_saveRequired = true;
			BugtrackerRuleViewModel newRule = BugtrackerRuleViewModel.NewRule;
			_bugtrackers.Add(newRule);
			UpdateSelection(newRule);
		}

		private void SampleGithubRuleMenuItem_Click(object sender, RoutedEventArgs e)
		{
			_saveRequired = true;
			BugtrackerRuleViewModel sampleGitHubRule = BugtrackerRuleViewModel.SampleGitHubRule;
			_bugtrackers.Add(sampleGitHubRule);
			UpdateSelection(sampleGitHubRule);
		}

		private void SampleJiraRuleMenuItem_Click(object sender, RoutedEventArgs e)
		{
			_saveRequired = true;
			BugtrackerRuleViewModel sampleJiraRule = BugtrackerRuleViewModel.SampleJiraRule;
			_bugtrackers.Add(sampleJiraRule);
			UpdateSelection(sampleJiraRule);
		}

		private void SampleJiraMultiprojectRuleMenuItem_Click(object sender, RoutedEventArgs e)
		{
			_saveRequired = true;
			BugtrackerRuleViewModel sampleJiraMultiprojectRule = BugtrackerRuleViewModel.SampleJiraMultiprojectRule;
			_bugtrackers.Add(sampleJiraMultiprojectRule);
			UpdateSelection(sampleJiraMultiprojectRule);
		}

		private void RemoveRuleButton_Click(object sender, RoutedEventArgs e)
		{
			if (BugTrackerRulesListBox.SelectedItem is BugtrackerRuleViewModel item && new MessageBoxWindow(Translate("Do you want to remove the selected issue tracker rule?"), Translate("You can't undo this action"), Translate("Remove"), Translate("Cancel"), showCancelButton: true, 580.0)
			{
				Owner = _parentWindow,
				WindowStartupLocation = WindowStartupLocation.CenterOwner
			}.ShowDialog().GetValueOrDefault())
			{
				_saveRequired = true;
				int num = _bugtrackers.IndexOf(item) - 1;
				_bugtrackers.Remove(item);
				BugtrackerRuleViewModel bugtrackerRule = ((_bugtrackers.Count > 0) ? _bugtrackers[0] : null);
				if (num != -1)
				{
					bugtrackerRule = _bugtrackers[num];
				}
				UpdateSelection(bugtrackerRule);
			}
		}

		private void RefreshRegexStatus()
		{
			if (IsRegexStringValid(RegexTextBox.Text))
			{
				RegexStatusTextBlock.Text = Translate("valid");
				RegexStatusTextBlock.Foreground = Brushes.Green;
			}
			else
			{
				RegexStatusTextBlock.Text = Translate("invalid");
				RegexStatusTextBlock.Foreground = Brushes.Red;
			}
		}

		private void RefreshSamplePreviewTextBlock()
		{
			SamplePreviewTextBlock.Text = SelectedBugtrackerRule.SampleMessage;
			if (IsRegexStringValid(SelectedBugtrackerRule.RegexString))
			{
				BugtrackerLinkDefinition bugtrackerLinkDefinition = BugtrackerLinkDefinition.Create(SelectedBugtrackerRule.Name, SelectedBugtrackerRule.Level, SelectedBugtrackerRule.RegexString, SelectedBugtrackerRule.UrlString);
				SamplePreviewTextBlock.ApplySearchAndButrackerHighlighting(null, new BugtrackerLinkDefinition[1] { bugtrackerLinkDefinition });
			}
		}

		private static bool IsRegexStringValid(string regexString)
		{
			if (string.IsNullOrEmpty(regexString))
			{
				return false;
			}
			try
			{
				new Regex(regexString);
			}
			catch
			{
				return false;
			}
			return true;
		}

		private void RefreshContentFallback()
		{
			if (IsEnabledCheckbox.IsChecked.Value)
			{
				ContentContainer.ShowContent();
				return;
			}
			string message = string.Format(Translate("Issue tracker integration for '{0}' is disabled"), _gitModule.RepositoryName);
			ContentContainer.ShowFallback(string.Empty, message);
		}

		private void UpdateSelection(BugtrackerRuleViewModel bugtrackerRule)
		{
			if (_bugtrackers.Count == 0)
			{
				ItemFallbackUserControl.Show();
				return;
			}
			ItemFallbackUserControl.Collapse();
			BugTrackerRulesListBox.SelectedItem = bugtrackerRule;
			BugTrackerRulesListBox.Focus();
		}

		private string GetBugtrackerRuleLocation(Level level)
		{
			return level switch
			{
				Level.Shared => ".issuetracker", 
				Level.Local => ".git/issuetracker", 
				Level.System => "system", 
				_ => throw new Exception("Cannot reach there"), 
			};
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

		private static ObservableCollection<BugtrackerRuleViewModel> LoadBugtrackers(GitModule gitModule)
		{
			ObservableCollection<BugtrackerRuleViewModel> observableCollection = new ObservableCollection<BugtrackerRuleViewModel>();
			BugtrackerLinkDefinition[] array = new GetBugtrackerRulesGitCommand().Execute(gitModule);
			foreach (BugtrackerLinkDefinition bugtrackerRule in array)
			{
				observableCollection.Add(new BugtrackerRuleViewModel(bugtrackerRule));
			}
			return observableCollection;
		}

	}
}
