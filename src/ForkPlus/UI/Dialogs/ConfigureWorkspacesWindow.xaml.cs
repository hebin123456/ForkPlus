using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using System.ComponentModel;

namespace ForkPlus.UI.Dialogs
{
	public partial class ConfigureWorkspacesWindow : ForkPlusDialogWindow
	{
		private readonly ObservableCollection<WorkspaceViewModel> _workspaceViewModels;

		public ConfigureWorkspacesWindow()
		{
			Workspace[] all = ForkPlusSettings.Default.Workspaces.All;
			_workspaceViewModels = new ObservableCollection<WorkspaceViewModel>(all.Map((Workspace x) => new WorkspaceViewModel(x)));
			InitializeComponent();
			base.DialogTitle = PreferencesLocalization.Current("Workspaces");
			base.DialogDescription = PreferencesLocalization.Current("Use '/' as path separator to create folders");
			base.ShowSubmitButton = false;
			base.CancelButtonTitle = PreferencesLocalization.Current("Close");
			WorkspacesListBox.ItemsSource = _workspaceViewModels;
			WorkspacesListBox.SelectedIndex = 0;
			UpdateDeleteButtonState();
			ShowWorkspaceInTitleCheckBox.IsChecked = ForkPlusSettings.Default.Workspaces.ShowInTitle;
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			if (_workspaceViewModels.Count > 1)
			{
				Workspace[] array = _workspaceViewModels.Map((WorkspaceViewModel x) => x.CreateWorkspace());
				Workspace activeWorkspace = IReadOnlyListExtensions.FirstItem(array, (Workspace x) => x.Name == ForkPlusSettings.Default.Workspaces.ActiveWorkspace.Name) ?? array.FirstItem();
				bool valueOrDefault = ShowWorkspaceInTitleCheckBox.IsChecked.GetValueOrDefault();
				ForkPlusSettings.Default.Workspaces.Update(array, activeWorkspace, valueOrDefault);
				ForkPlusSettings.Default.Save();
				MainWindow.Instance.TabManager.RestoreSession();
				MainWindow.Instance.Toolbar.RefreshWorkspacesButton();
				MainWindow.Instance.RefreshTitle();
			}
			base.OnClosing(e);
		}

		private void WorkspacesListBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
		{
			if (ItemsControl.ContainerFromElement(sender as ListBox, e.OriginalSource as DependencyObject) is ListBoxItem { DataContext: WorkspaceViewModel dataContext })
			{
				WorkspacesListBox.ContextMenu.Items.Clear();
				WorkspacesListBox.ContextMenu.SetItems(GetContextMenu(dataContext));
			}
			else
			{
				e.Handled = true;
				WorkspacesListBox.ContextMenu.IsOpen = false;
			}
		}

		private void WorkspacesListBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.F2 && ItemsControl.ContainerFromElement(sender as ListBox, e.OriginalSource as DependencyObject) is ListBoxItem { DataContext: WorkspaceViewModel dataContext })
			{
				dataContext.IsInEditMode = true;
			}
		}

		private void AddWorkspaceButton_Click(object sender, RoutedEventArgs e)
		{
			AddNewWorkspace();
		}

		private void RemoveWorkspaceButton_Click(object sender, RoutedEventArgs e)
		{
			if (WorkspacesListBox.SelectedItem is WorkspaceViewModel workspace)
			{
				RemoveWorkspace(workspace);
			}
		}

		private IEnumerable<Control> GetContextMenu(WorkspaceViewModel workspaceViewModel)
		{
			MenuItem addMenuItem = new MenuItem();
			addMenuItem.Header = PreferencesLocalization.MenuHeader("Add New Workspace");
			addMenuItem.Click += delegate
			{
				AddNewWorkspace();
			};
			yield return addMenuItem;

			yield return new Separator();

			MenuItem renameMenuItem = new MenuItem();
			renameMenuItem.Header = PreferencesLocalization.MenuHeader("Rename");
			renameMenuItem.Click += delegate
			{
				workspaceViewModel.IsInEditMode = true;
			};
			yield return renameMenuItem;

			MenuItem deleteMenuItem = new MenuItem();
			deleteMenuItem.Header = PreferencesLocalization.MenuHeader("Delete...");
			deleteMenuItem.Click += delegate
			{
				RemoveWorkspace(workspaceViewModel);
			};
			deleteMenuItem.IsEnabled = _workspaceViewModels.Count > 2;
			yield return deleteMenuItem;
		}

		private void AddNewWorkspace()
		{
			WorkspaceViewModel workspaceViewModel = new WorkspaceViewModel();
			_workspaceViewModels.Add(workspaceViewModel);
			UpdateDeleteButtonState();
			SelectAndFocusWorkspace(workspaceViewModel);
			workspaceViewModel.IsInEditMode = true;
		}

		private void RemoveWorkspace(WorkspaceViewModel workspace)
		{
			if (new MessageBoxWindow("Do you want to delete the workspace '" + workspace.Name + "'?", "You can't undo this action", "Delete", "Cancel", showCancelButton: true, 500.0)
			{
				Owner = this,
				WindowStartupLocation = WindowStartupLocation.CenterOwner
			}.ShowDialog().GetValueOrDefault())
			{
				int num = _workspaceViewModels.IndexOf(workspace);
				_workspaceViewModels.Remove(workspace);
				UpdateDeleteButtonState();
				WorkspaceViewModel workspaceViewModel = ((num < _workspaceViewModels.Count) ? _workspaceViewModels[num] : _workspaceViewModels.FirstOrDefault());
				SelectAndFocusWorkspace(workspaceViewModel);
			}
		}

		private void UpdateDeleteButtonState()
		{
			RemoveWorkspaceButton.IsEnabled = _workspaceViewModels.Count > 2;
		}

		private void SelectAndFocusWorkspace(WorkspaceViewModel workspaceViewModel)
		{
			WorkspacesListBox.SelectedItem = workspaceViewModel;
			WorkspacesListBox.Focus();
		}

		private void ShowWorkspaceInTitleCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			ForkPlusSettings.Default.Workspaces.ShowInTitle = ShowWorkspaceInTitleCheckBox.IsChecked.GetValueOrDefault();
		}

	}
}
