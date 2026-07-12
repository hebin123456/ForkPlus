using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.CustomCommands;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.UserControls.Preferences
{
	public partial class CustomCommandsUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		private static readonly string UrlCommandDefaultUrl = "https://hebin.me";

		private Window _parentWindow;

		[Null]
		private GitModule _gitModule;

		private bool _localMode;

		private ObservableCollection<CustomCommandViewModel> _customCommandViewModels;

		public CustomCommandsUserControl()
		{
			InitializeComponent();
			ApplyLocalization();
			AddCustomCommandDropDownButton.ContextMenu.Opened += delegate
			{
				ApplyLocalization();
			};
		}

		public void InitializeLocal(Window parentWindow, GitModule gitModule, RepositoryData repositoryData)
		{
			_localMode = true;
			_gitModule = gitModule;
			_parentWindow = parentWindow;
			_customCommandViewModels = LoadLocalCustomCommands(repositoryData);
			CustomCommandsListBox.ItemsSource = _customCommandViewModels;
			CustomCommandViewModel customCommandViewModel = _customCommandViewModels.FirstOrDefault();
			if (customCommandViewModel != null)
			{
				SelectAndFocusCustomCommand(customCommandViewModel);
			}
			else
			{
				FallbackUserControl.Show();
			}
			LocationRadioButtonContainer.Show();
			ApplyLocalization();
		}

		public void InitializeGlobal(Window parentWindow)
		{
			_localMode = false;
			_parentWindow = parentWindow;
			_customCommandViewModels = LoadGlobalCustomCommands();
			CustomCommandsListBox.ItemsSource = _customCommandViewModels;
			CustomCommandViewModel customCommandViewModel = _customCommandViewModels.FirstOrDefault();
			if (customCommandViewModel != null)
			{
				SelectAndFocusCustomCommand(customCommandViewModel);
			}
			else
			{
				FallbackUserControl.Show();
			}
			LocationRadioButtonContainer.Collapse();
			ApplyLocalization();
		}

		public void ApplyLocalization()
		{
			RevisionCustomCommandMenuItem.Header = PreferencesLocalization.Translate("Add Commit Custom Command", ForkPlusSettings.Default.UiLanguage);
			RepositoryCustomCommandMenuItem.Header = PreferencesLocalization.Translate("Add Repository Custom Command", ForkPlusSettings.Default.UiLanguage);
			FileCustomCommandMenuItem.Header = PreferencesLocalization.Translate("Add File Custom Command", ForkPlusSettings.Default.UiLanguage);
			ReferenceCustomCommandMenuItem.Header = PreferencesLocalization.Translate("Add Branch Custom Command", ForkPlusSettings.Default.UiLanguage);
			SubmoduleCustomCommandMenuItem.Header = PreferencesLocalization.Translate("Add Submodule Custom Command", ForkPlusSettings.Default.UiLanguage);
		}

		public void Save()
		{
			CustomCommand[] array = _customCommandViewModels.Map((CustomCommandViewModel x) => x.CustomCommand);
			if (_localMode)
			{
				CustomCommandManager.Current.SetLocalCustomCommands(_gitModule, array);
			}
			else
			{
				CustomCommandManager.Current.SetGlobalCustomCommands(array);
			}
		}

		private void CustomCommandsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!(CustomCommandsListBox.SelectedItem is CustomCommandViewModel customCommandViewModel))
			{
				FallbackUserControl.FallbackMessage = null;
				FallbackUserControl.Show();
			}
			else if (customCommandViewModel.Version > 2)
			{
				FallbackUserControl.FallbackMessage = PreferencesLocalization.Current("This custom  command was created in a newer version of Fork.\n Please check for updates in order to use it.");
				FallbackUserControl.Show();
			}
			else
			{
				FallbackUserControl.Collapse();
				RefreshControls(customCommandViewModel);
			}
		}

		private void UIMode_Changed(object sender, RoutedEventArgs e)
		{
			CustomCommandViewModel customCommandViewModel = CustomCommandsListBox.SelectedItem as CustomCommandViewModel;
			if (UiRadioButton.IsChecked.GetValueOrDefault())
			{
				customCommandViewModel.ActionType = ActionType.UI;
				UiActionDetailsContainer.Show();
				ProcessActionTypeTextBlock.Collapse();
				ProcessActionButton.Collapse();
			}
			else if (ProcessRadioButton.IsChecked.GetValueOrDefault())
			{
				customCommandViewModel.ActionType = ActionType.Action;
				ProcessActionTypeTextBlock.Show();
				ProcessActionButton.Show();
				UiActionDetailsContainer.Collapse();
			}
		}

		private void RepositoryCustomCommandMenuItem_Click(object sender, RoutedEventArgs e)
		{
			string name = CreateCustomCommandName();
			CustomCommandViewModel customCommandViewModel = new CustomCommandViewModel(CustomCommandTarget.Repository, name, new UrlCustomCommandAction(UrlCommandDefaultUrl));
			_customCommandViewModels.Add(customCommandViewModel);
			SelectAndFocusCustomCommand(customCommandViewModel);
		}

		private void RevisionCustomCommandMenuItem_Click(object sender, RoutedEventArgs e)
		{
			string name = CreateCustomCommandName();
			CustomCommandViewModel customCommandViewModel = new CustomCommandViewModel(CustomCommandTarget.Revision, name, new ShCustomCommandAction("git show ${sha}", showOutput: true, waitForExit: true));
			_customCommandViewModels.Add(customCommandViewModel);
			SelectAndFocusCustomCommand(customCommandViewModel);
		}

		private void FileCustomCommandMenuItem_Click(object sender, RoutedEventArgs e)
		{
			string name = CreateCustomCommandName();
			CustomCommandViewModel customCommandViewModel = new CustomCommandViewModel(CustomCommandTarget.RepositoryFile, name, new ShCustomCommandAction("git diff ${file}", showOutput: true, waitForExit: true));
			_customCommandViewModels.Add(customCommandViewModel);
			SelectAndFocusCustomCommand(customCommandViewModel);
		}

		private void ReferenceCustomCommandMenuItem_Click(object sender, RoutedEventArgs e)
		{
			string name = CreateCustomCommandName();
			CustomCommandViewModel customCommandViewModel = new CustomCommandViewModel(CustomCommandTarget.Reference, name, new ShCustomCommandAction("git diff HEAD ${ref}", showOutput: true, waitForExit: true), new CustomCommandRefTarget[2]
			{
				CustomCommandRefTarget.LocalBranch,
				CustomCommandRefTarget.RemoteBranch
			});
			_customCommandViewModels.Add(customCommandViewModel);
			SelectAndFocusCustomCommand(customCommandViewModel);
		}

		private void SubmoduleCustomCommandMenuItem_Click(object sender, RoutedEventArgs e)
		{
			string name = CreateCustomCommandName();
			CustomCommandViewModel customCommandViewModel = new CustomCommandViewModel(CustomCommandTarget.Submodule, name, new ShCustomCommandAction("git submodule update --remote -- ${submodule}", showOutput: true, waitForExit: true));
			_customCommandViewModels.Add(customCommandViewModel);
			SelectAndFocusCustomCommand(customCommandViewModel);
		}

		private void RemoveCustomCommandButton_Click(object sender, RoutedEventArgs e)
		{
			if (CustomCommandsListBox.SelectedItem is CustomCommandViewModel item && new MessageBoxWindow("Do you want to remove the selected custom command?", "You can't undo this action", "Remove", "Cancel", showCancelButton: true, 550.0)
			{
				Owner = _parentWindow,
				WindowStartupLocation = WindowStartupLocation.CenterOwner
			}.ShowDialog().GetValueOrDefault())
			{
				int num = _customCommandViewModels.IndexOf(item) - 1;
				_customCommandViewModels.Remove(item);
				CustomCommandViewModel customCommand = ((num != -1) ? _customCommandViewModels[num] : _customCommandViewModels.FirstOrDefault());
				SelectAndFocusCustomCommand(customCommand);
			}
		}

		private void TargetsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			CustomCommandViewModel customCommandViewModel = CustomCommandsListBox.SelectedItem as CustomCommandViewModel;
			object selectedItem = TargetsComboBox.SelectedItem;
			if (selectedItem == CommitComboBoxItem)
			{
				customCommandViewModel.Target = CustomCommandTarget.Revision;
				RefreshReferenceTargetsContainer(CustomCommandTarget.Revision);
			}
			else if (selectedItem == RepositoryComboBoxItem)
			{
				customCommandViewModel.Target = CustomCommandTarget.Repository;
				RefreshReferenceTargetsContainer(CustomCommandTarget.Repository);
			}
			else if (selectedItem == FileComboBoxItem)
			{
				customCommandViewModel.Target = CustomCommandTarget.RepositoryFile;
				RefreshReferenceTargetsContainer(CustomCommandTarget.RepositoryFile);
			}
			else if (selectedItem == BranchComboBoxItem)
			{
				customCommandViewModel.Target = CustomCommandTarget.Reference;
				RefreshReferenceTargetsContainer(CustomCommandTarget.Reference);
			}
			else
			{
				if (selectedItem != SubmoduleComboBoxItem)
				{
					throw new InvalidOperationException();
				}
				customCommandViewModel.Target = CustomCommandTarget.Submodule;
				RefreshReferenceTargetsContainer(CustomCommandTarget.Submodule);
			}
			UpdateDescription(customCommandViewModel);
		}

		private void UiControlsButton_Click(object sender, RoutedEventArgs e)
		{
			CustomCommandViewModel customCommandViewModel = CustomCommandsListBox.SelectedItem as CustomCommandViewModel;
			EditCustomCommandUIControlsWindow editCustomCommandUIControlsWindow = new EditCustomCommandUIControlsWindow(customCommandViewModel.UIViewModel.Controls);
			editCustomCommandUIControlsWindow.Owner = _parentWindow;
			if (editCustomCommandUIControlsWindow.ShowDialog().GetValueOrDefault())
			{
				customCommandViewModel.UIViewModel.Controls = editCustomCommandUIControlsWindow.OutControls;
			}
		}

		private void ProcessActionButton_Click(object sender, RoutedEventArgs e)
		{
			CustomCommandViewModel customCommandViewModel = CustomCommandsListBox.SelectedItem as CustomCommandViewModel;
			CustomCommandAction customCommandAction = EditAction(customCommandViewModel.CustomCommand, customCommandViewModel.ActionViewModel.Action, showCancel: false);
			if (customCommandAction != null)
			{
				customCommandViewModel.ActionViewModel.Action = customCommandAction;
				customCommandViewModel.RefreshDetails();
			}
		}

		private void UiActionButton1_Click(object sender, RoutedEventArgs e)
		{
			CustomCommandViewModel customCommandViewModel = CustomCommandsListBox.SelectedItem as CustomCommandViewModel;
			CustomCommandAction customCommandAction = EditAction(customCommandViewModel.CustomCommand, customCommandViewModel.UIViewModel.Button1ActionViewModel.Action, showCancel: true);
			if (customCommandAction != null)
			{
				customCommandViewModel.UIViewModel.Button1ActionViewModel.Action = customCommandAction;
				customCommandViewModel.RefreshDetails();
			}
		}

		private void UiActionButton2_Click(object sender, RoutedEventArgs e)
		{
			CustomCommandViewModel customCommandViewModel = CustomCommandsListBox.SelectedItem as CustomCommandViewModel;
			CustomCommandAction customCommandAction = EditAction(customCommandViewModel.CustomCommand, customCommandViewModel.UIViewModel.Button2ActionViewModel.Action, showCancel: true);
			if (customCommandAction != null)
			{
				customCommandViewModel.UIViewModel.Button2ActionViewModel.Action = customCommandAction;
				customCommandViewModel.RefreshDetails();
			}
		}

		[Null]
		private CustomCommandAction EditAction(CustomCommand customCommand, CustomCommandAction action, bool showCancel)
		{
			EditCustomActionWindow editCustomActionWindow = new EditCustomActionWindow(customCommand, action, showCancel);
			editCustomActionWindow.Owner = _parentWindow;
			if (editCustomActionWindow.ShowDialog().GetValueOrDefault())
			{
				return editCustomActionWindow.OutAction;
			}
			return null;
		}

		private void LocationRadioButton_Changed(object sender, RoutedEventArgs e)
		{
			if (_localMode)
			{
				CustomCommandViewModel customCommandViewModel = CustomCommandsListBox.SelectedItem as CustomCommandViewModel;
				customCommandViewModel.Shared = SharedRadioButton.IsChecked.GetValueOrDefault();
				RefreshLocationControls(customCommandViewModel);
			}
		}

		private void OSComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			CustomCommandViewModel customCommandViewModel = CustomCommandsListBox.SelectedItem as CustomCommandViewModel;
			object selectedItem = OSComboBox.SelectedItem;
			if (selectedItem == AnyComboBoxItem)
			{
				customCommandViewModel.OS = CustomCommandOS.Any;
				return;
			}
			if (selectedItem == WindowsComboBoxItem)
			{
				customCommandViewModel.OS = CustomCommandOS.Windows;
				return;
			}
			if (selectedItem == MacComboBoxItem)
			{
				customCommandViewModel.OS = CustomCommandOS.Mac;
				return;
			}
			throw new InvalidOperationException();
		}

		private ObservableCollection<CustomCommandViewModel> LoadLocalCustomCommands(RepositoryData repositoryData)
		{
			ObservableCollection<CustomCommandViewModel> observableCollection = new ObservableCollection<CustomCommandViewModel>();
			CustomCommand[] localCustomCommands = CustomCommandManager.Current.GetLocalCustomCommands(repositoryData);
			foreach (CustomCommand command in localCustomCommands)
			{
				observableCollection.Add(new CustomCommandViewModel(command));
			}
			return observableCollection;
		}

		private ObservableCollection<CustomCommandViewModel> LoadGlobalCustomCommands()
		{
			ObservableCollection<CustomCommandViewModel> observableCollection = new ObservableCollection<CustomCommandViewModel>();
			CustomCommand[] globalCustomCommands = CustomCommandManager.Current.GetGlobalCustomCommands();
			foreach (CustomCommand command in globalCustomCommands)
			{
				observableCollection.Add(new CustomCommandViewModel(command));
			}
			return observableCollection;
		}

		private string CreateCustomCommandName()
		{
			string name = "Custom Command";
			int num = _customCommandViewModels.Count((CustomCommandViewModel x) => x.Name.ToLower().StartsWith(name.ToLower()));
			if (num > 0)
			{
				name += $"{num}";
			}
			return name;
		}

		private void RefreshControls(CustomCommandViewModel customCommand)
		{
			if (_localMode)
			{
				RefreshLocationControls(customCommand);
			}
			RefreshTargetCombobox(customCommand.Target);
			RefreshReferenceTargetsContainer(customCommand.Target);
			UiRadioButton.IsChecked = customCommand.ActionType == ActionType.UI;
			ProcessRadioButton.IsChecked = customCommand.ActionType == ActionType.Action;
		}

		private void RefreshTargetCombobox(CustomCommandTarget target)
		{
			switch (target)
			{
			case CustomCommandTarget.Revision:
				TargetsComboBox.SelectedItem = CommitComboBoxItem;
				break;
			case CustomCommandTarget.Repository:
				TargetsComboBox.SelectedItem = RepositoryComboBoxItem;
				break;
			case CustomCommandTarget.RepositoryFile:
				TargetsComboBox.SelectedItem = FileComboBoxItem;
				break;
			case CustomCommandTarget.Reference:
				TargetsComboBox.SelectedItem = BranchComboBoxItem;
				break;
			case CustomCommandTarget.Submodule:
				TargetsComboBox.SelectedItem = SubmoduleComboBoxItem;
				break;
			}
		}

		private void RefreshReferenceTargetsContainer(CustomCommandTarget target)
		{
			if (target == CustomCommandTarget.Reference)
			{
				ReferenceTargetsContainer.Show();
			}
			else
			{
				ReferenceTargetsContainer.Collapse();
			}
		}

		private void RefreshLocationControls(CustomCommandViewModel customCommand)
		{
			LocalRadioButton.IsChecked = !customCommand.Shared;
			SharedRadioButton.IsChecked = customCommand.Shared;
			if (customCommand.Shared)
			{
				ShareIcon.Show();
				LocationTextBlock.Text = ".fork/custom-commands.json";
				OSTextBlock.Show();
				OSComboBox.Show();
				RefreshOSComboBox(customCommand.OS);
			}
			else
			{
				ShareIcon.Hide();
				LocationTextBlock.Text = ".git/fork/custom-commands.json";
				OSTextBlock.Hide();
				OSComboBox.Hide();
				RefreshOSComboBox(CustomCommandOS.Any);
			}
		}

		private void RefreshOSComboBox(CustomCommandOS osType)
		{
			switch (osType)
			{
			case CustomCommandOS.Any:
				OSComboBox.SelectedItem = AnyComboBoxItem;
				break;
			case CustomCommandOS.Windows:
				OSComboBox.SelectedItem = WindowsComboBoxItem;
				break;
			case CustomCommandOS.Mac:
				OSComboBox.SelectedItem = MacComboBoxItem;
				break;
			}
		}

		private void UpdateDescription(CustomCommandViewModel customCommand)
		{
			DescriptionTextBlock.Inlines.Clear();
			DescriptionTextBlock.Inlines.Add(new Run(PreferencesLocalization.Translate("Available variables:", ForkPlusSettings.Default.UiLanguage))
			{
				FontSize = 13.0,
				FontWeight = FontWeights.Medium
			});
			DescriptionTextBlock.Inlines.Add(Environment.NewLine);
			DescriptionTextBlock.Inlines.Add(Environment.NewLine);
			DescriptionTextBlock.Inlines.AddRange(customCommand.VariablesInlines);
		}

		private void SelectAndFocusCustomCommand(CustomCommandViewModel customCommand)
		{
			CustomCommandsListBox.SelectedItem = customCommand;
			CustomCommandsListBox.Focus();
		}

	}
}
