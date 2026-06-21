using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Parsing;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.Settings;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.UserControls.Preferences
{
	public partial class GeneralUserControl : UserControl
	{
		private static readonly double CodeEditorMinFontSize = 10.0;

		private static readonly double CodeEditorMaxFontSize = 40.0;

		private ForkPlusDialogWindow _parentWindow;

		private bool _initialized;

		public GeneralUserControl()
		{
			InitializeComponent();
		}

		public void Initialize(ForkPlusDialogWindow parentWindow)
		{
			_parentWindow = parentWindow;
			RefreshSourceDirs();
			ShowDiffChangeMarksCheckBox.IsChecked = ForkPlusSettings.Default.DiffShowChangeMarks;
			CodeEditorFontSizeTextBox.Text = ForkPlusSettings.Default.CodeEditorFontSize.ToString();
			InitializeDiffCodeEditor();
			switch (ForkPlusSettings.Default.RevisionSortOrder)
			{
			case RevisionSortOrder.Date:
				DateSortOrderRadioButton.IsChecked = true;
				break;
			case RevisionSortOrder.Topo:
				TopologicalSortOrderRadioButton.IsChecked = true;
				break;
			}
			DiffCodeEditor.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
			FetchRemotesAutomaticallyCheckBox.IsChecked = ForkPlusSettings.Default.FetchRemotesAutomatically;
			FetchAllTagsCheckBox.IsChecked = ForkPlusSettings.Default.FetchAllTags;
			UpdateRepoStatusAutomaticallyCheckBox.IsChecked = ForkPlusSettings.Default.AutomaticStatusUpdateInterval > 0;
			UpdateSubmodulesAutomaticallyCheckBox.IsChecked = ForkPlusSettings.Default.UpdateSubmodulesOnCheckout;
			DisableSyntaxHighlightingCheckBox.IsChecked = ForkPlusSettings.Default.DisableSyntaxHighlighting;
			SpaceCharacterComboBox.ItemsSource = Consts.Git.References.SpaceCharacterReplacements;
			SpaceCharacterComboBox.SelectedValue = ForkPlusSettings.Default.ReferenceSpaceCharacterReplacement;
			PushAutomaticallyOnCommitCheckBox.IsChecked = ForkPlusSettings.Default.PushAutomaticallyOnCommit;
			CompactBranchLabelsCheckBox.IsChecked = ForkPlusSettings.Default.CompactBranchLabels;
			RefreshLanguageComboBoxItems();
			SelectUiLanguage(ForkPlusSettings.Default.UiLanguage);
			_initialized = true;
		}

		private void RefreshLanguageComboBoxItems()
		{
			LanguageComboBox.Items.Clear();
			foreach (PreferencesLocalization.LanguageOption language in PreferencesLocalization.GetLanguages())
			{
				LanguageComboBox.Items.Add(new ComboBoxItem
				{
					Content = language.DisplayName,
					Tag = language.Code
				});
			}
		}

		private void SelectUiLanguage(string language)
		{
			foreach (ComboBoxItem item in LanguageComboBox.Items)
			{
				if ((item.Tag as string) == language)
				{
					LanguageComboBox.SelectedItem = item;
					return;
				}
			}
			LanguageComboBox.SelectedIndex = 0;
		}

		private void RefreshSourceDirs()
		{
			SrcDirsListBox.ItemsSource = RepositoryManager.Instance.SourceDirs.ToSortedArray((string x, string y) => NumericIgnoreCaseStringComparer.Comparer.Compare(x, y)).Map((string x) => new SrcDirViewModel(x));
			RemoveSrcDirButton.Visibility = ((RepositoryManager.Instance.SourceDirs.Length <= 1) ? Visibility.Collapsed : Visibility.Visible);
		}

		private void InitializeDiffCodeEditor()
		{
			string input = "diff --git forkSrcPrefix/file1.swift forkDstPrefix/file2.swift\nindex a57cc90ef2fe70c4da807e0b8369503b45572774..ded5ed9cf2fe70c4da807e0b8369503b45572774 100644\n--- {Consts.Git.Diff.SrcPrefix}file1.swift\n+++ {Consts.Git.Diff.DstPrefix}file2.swift\n@@ -1,3 +1,3 @@\n public func elementsEqual<OtherSequence>(\n _ other: OtherSequence,\n-isEquivalent isEquivalent: (${GElement}, ${GElement}) throws -> Bool\n+isEquivalent: (${GElement}, ${GElement}) throws -> Bool\n";
			GitCommandResult<Patch> gitCommandResult = new BiturboPatchParser().Parse(input);
			DiffCodeEditor.VisualPatch = VisualPatch.CreateVisualPatch(gitCommandResult.Result.Diffs.FirstItem(), entireFile: false, DiffLocation.Revision);
		}

		private void EditSrcDirButton_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Button { DataContext: SrcDirViewModel dataContext })
			{
				EditSrcDir(dataContext.Path);
			}
		}

		private void AddSrcDirButton_Click(object sender, RoutedEventArgs e)
		{
			AddSrcDir();
		}

		private void RemoveSrcDirButton_Click(object sender, RoutedEventArgs e)
		{
			if (SrcDirsListBox.SelectedItem is SrcDirViewModel srcDirViewModel)
			{
				RemoveSrcDir(srcDirViewModel.Path);
			}
		}

		private void SrcDirsListBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
		{
			if (ItemsControl.ContainerFromElement(sender as ListBox, e.OriginalSource as DependencyObject) is ListBoxItem { DataContext: SrcDirViewModel dataContext })
			{
				SrcDirsListBox.ContextMenu.Items.Clear();
				SrcDirsListBox.ContextMenu.SetItems(CreateSrcDirContextMenu(dataContext));
			}
			else
			{
				e.Handled = true;
				SrcDirsListBox.ContextMenu.IsOpen = false;
			}
		}

		private IEnumerable<Control> CreateSrcDirContextMenu(SrcDirViewModel srcDirViewModel)
		{
			MenuItem addMenuItem = new MenuItem();
			addMenuItem.Header = PreferencesLocalization.MenuHeader("Add New...");
			addMenuItem.Click += delegate
			{
				AddSrcDir();
			};
			yield return addMenuItem;

			MenuItem deleteMenuItem = new MenuItem();
			deleteMenuItem.Header = PreferencesLocalization.MenuHeader("Delete...");
			deleteMenuItem.Click += delegate
			{
				RemoveSrcDir(srcDirViewModel.Path);
			};
			deleteMenuItem.IsEnabled = RepositoryManager.Instance.SourceDirs.Length > 1;
			yield return deleteMenuItem;
		}

		private void RevisionSortOrder_Changed(object sender, RoutedEventArgs e)
		{
			if (sender == DateSortOrderRadioButton)
			{
				ForkPlusSettings.Default.RevisionSortOrder = RevisionSortOrder.Date;
			}
			else if (sender == TopologicalSortOrderRadioButton)
			{
				ForkPlusSettings.Default.RevisionSortOrder = RevisionSortOrder.Topo;
			}
		}

		private void FetchRemotesAutomaticallyCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (_initialized)
			{
				ForkPlusSettings.Default.FetchRemotesAutomatically = FetchRemotesAutomaticallyCheckBox.IsChecked.GetValueOrDefault(true);
			}
		}

		private void FetchAllTagsCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (_initialized)
			{
				ForkPlusSettings.Default.FetchAllTags = FetchAllTagsCheckBox.IsChecked.GetValueOrDefault();
			}
		}

		private void UpdateRepoStatusAutomaticallyCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (_initialized)
			{
				int num = (UpdateRepoStatusAutomaticallyCheckBox.IsChecked.GetValueOrDefault(true) ? 60 : 0);
				ForkPlusSettings.Default.AutomaticStatusUpdateInterval = num;
				NotificationCenter.Current.RaiseUpdateRepoStatusAutomaticallyChanged(this, num);
			}
		}

		private void UpdateSubmodulesAutomaticallyCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (_initialized)
			{
				ForkPlusSettings.Default.UpdateSubmodulesOnCheckout = UpdateSubmodulesAutomaticallyCheckBox.IsChecked.GetValueOrDefault(true);
			}
		}

		private void SpaceCharacterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_initialized && SpaceCharacterComboBox.SelectedValue is string referenceSpaceCharacterReplacement)
			{
				ForkPlusSettings.Default.ReferenceSpaceCharacterReplacement = referenceSpaceCharacterReplacement;
			}
		}

		private void CodeEditorFontSizeTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (_initialized)
			{
				if (!double.TryParse(CodeEditorFontSizeTextBox.Text, out var result))
				{
					result = 13.0;
				}
				if (result < CodeEditorMinFontSize)
				{
					result = CodeEditorMinFontSize;
				}
				else if (result > CodeEditorMaxFontSize)
				{
					result = CodeEditorMaxFontSize;
				}
				ForkPlusSettings.Default.CodeEditorFontSize = result;
				NotificationCenter.Current.RaiseCodeEditorFontSizeChanged(this, result);
			}
		}

		private void ShowDiffChangeMarksCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (_initialized)
			{
				bool valueOrDefault = ShowDiffChangeMarksCheckBox.IsChecked.GetValueOrDefault();
				ForkPlusSettings.Default.DiffShowChangeMarks = valueOrDefault;
				NotificationCenter.Current.RaiseDiffShowChangeMarksChanged(this, valueOrDefault);
			}
		}

		private void PushAutomaticallyOnCommitCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (_initialized)
			{
				bool valueOrDefault = PushAutomaticallyOnCommitCheckBox.IsChecked.GetValueOrDefault();
				ForkPlusSettings.Default.PushAutomaticallyOnCommit = valueOrDefault;
				NotificationCenter.Current.RaisePushAutomaticallyOnCommitChanged(this, valueOrDefault);
			}
		}

		private void CompactBranchLabelsCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (_initialized)
			{
				bool valueOrDefault = CompactBranchLabelsCheckBox.IsChecked.GetValueOrDefault(true);
				ForkPlusSettings.Default.CompactBranchLabels = valueOrDefault;
				NotificationCenter.Current.RaiseCompactBranchLabelsChanged(this, valueOrDefault);
			}
		}

		private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_initialized && LanguageComboBox.SelectedItem is ComboBoxItem { Tag: string language })
			{
				ForkPlusSettings.Default.UiLanguage = language;
				if (_parentWindow is PreferencesWindow preferencesWindow)
				{
					preferencesWindow.ApplyLocalization();
				}
				MainWindow.Instance?.ApplyLocalization();
			}
		}

		private void DisableSyntaxHighlightingCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (_initialized)
			{
				bool valueOrDefault = DisableSyntaxHighlightingCheckBox.IsChecked.GetValueOrDefault();
				ForkPlusSettings.Default.DisableSyntaxHighlighting = valueOrDefault;
				NotificationCenter.Current.RaiseDisableSyntaxHighlightingChanged(this, valueOrDefault);
			}
		}

		private void AddSrcDir()
		{
			string initialDirectory = Environment.ExpandEnvironmentVariables("%userprofile%");
			if (OpenDialog.SelectDirectory(_parentWindow, "Select location", initialDirectory, out var directoryPath))
			{
				if (!directoryPath.EndsWith("\\"))
				{
					directoryPath += "\\";
				}
				List<string> list = new List<string>(RepositoryManager.Instance.SourceDirs);
				list.Add(directoryPath);
				RepositoryManager.Instance.SetSourceDirs(list.ToArray());
				RepositoryManager.Instance.Save();
				RefreshSourceDirs();
			}
		}

		private void EditSrcDir(string path)
		{
			if (OpenDialog.SelectDirectory(_parentWindow, "Select location", path, out var directoryPath))
			{
				if (!directoryPath.EndsWith("\\"))
				{
					directoryPath += "\\";
				}
				List<string> list = new List<string>(RepositoryManager.Instance.SourceDirs);
				list.Remove(path);
				list.Add(directoryPath);
				RepositoryManager.Instance.SetSourceDirs(list.ToArray());
				RepositoryManager.Instance.Save();
				RefreshSourceDirs();
			}
		}

		private void RemoveSrcDir(string path)
		{
			if (new MessageBoxWindow("Do you want to remove the selected source directory?", "Fork will not look for repositories in this folder automatically", "Remove", "Cancel", showCancelButton: true, 550.0)
			{
				Owner = _parentWindow,
				WindowStartupLocation = WindowStartupLocation.CenterOwner
			}.ShowDialog().GetValueOrDefault())
			{
				List<string> list = new List<string>(RepositoryManager.Instance.SourceDirs);
				list.Remove(path);
				RepositoryManager.Instance.SetSourceDirs(list.ToArray());
				RefreshSourceDirs();
				string prefix = path;
				if (!prefix.EndsWith("\\"))
				{
					prefix += "\\";
				}
				string[] repositoriesToDelete = RepositoryManager.Instance.Repositories.Filter((RepositoryManager.Repository x) => x.Path.StartsWith(prefix)).Map((RepositoryManager.Repository x) => x.Path);
				RepositoryManager.Instance.DeleteRepositories(repositoriesToDelete, addToIgnore: false);
				RepositoryManager.Instance.Save();
			}
		}

	}
}
