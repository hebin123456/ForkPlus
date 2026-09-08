using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup;
using ForkPlus.Settings;
using ForkPlus.UI.Helpers;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

namespace ForkPlus.UI.Dialogs
{
	public partial class ConfigureWorkspacesWindow : ForkPlusDialogWindow
	{
		private readonly ObservableCollection<WorkspaceViewModel> _workspaceViewModels;

		public ConfigureWorkspacesWindow()
		{
			// “工作区配置”界面不需要左上角 ForkPlus Logo；该 Logo 会在 Column=0 宽度为 0 时仍绘制并覆盖内容。
			base.ShowLogo = false;

			Workspace[] all = ForkPlusSettings.Default.Workspaces.All;
			_workspaceViewModels = new ObservableCollection<WorkspaceViewModel>(all.Map((Workspace x) => new WorkspaceViewModel(x)));
			InitializeComponent();
			base.DialogTitle = PreferencesLocalization.Current("Workspaces");
			base.DialogDescription = PreferencesLocalization.Current("Use '/' as path separator to create folders");
			base.ShowSubmitButton = false;
			base.CancelButtonTitle = PreferencesLocalization.Current("Close");
			WorkspacesListBox.ItemsSource = _workspaceViewModels;
			WorkspacesListBox.SelectedIndex = 0;
			WorkspacesListBox.ContextRequested += (_, e) => e.Handled = true;
			WorkspacesListBox.AddHandler(InputElement.PointerPressedEvent, new EventHandler<PointerPressedEventArgs>(WorkspacesListBox_ContextMenuPointerPressed), RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
			UpdateDeleteButtonState();
			ShowWorkspaceInTitleCheckBox.IsChecked = ForkPlusSettings.Default.Workspaces.ShowInTitle;
		}

		protected override void OnClosing(global::Avalonia.Controls.WindowClosingEventArgs e)
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

		private void WorkspacesListBox_ContextMenuOpening(object sender, global::Avalonia.Input.ContextRequestedEventArgs e)
		{
			Point point;
			if (!SetWorkspaceContextMenu(e.Source, e.TryGetPosition(WorkspacesListBox, out point) ? point : (Point?)null))
			{
				e.Handled = true;
				WorkspacesListBox.ContextMenu?.Close();
			}
		}

		private void WorkspacesListBox_ContextMenuPointerPressed(object sender, PointerPressedEventArgs e)
		{
			if (!e.GetCurrentPoint(WorkspacesListBox).Properties.IsRightButtonPressed)
			{
				return;
			}

			e.Handled = true;
			WorkspacesListBox.ContextMenu?.Close();
			if (!SetWorkspaceContextMenu(e.Source, e.GetPosition(WorkspacesListBox)))
			{
				WorkspacesListBox.ContextMenu?.Close();
				return;
			}
			global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AttachAutoDismiss(WorkspacesListBox.ContextMenu, WorkspacesListBox);
			WorkspacesListBox.ContextMenu.Open();
		}

		private bool SetWorkspaceContextMenu(object source, Point? position)
		{
			ListBoxItem container = null;
			for (global::Avalonia.Visual current = source as global::Avalonia.Visual; current != null; current = global::Avalonia.VisualTree.VisualExtensions.GetVisualParent(current))
			{
				if (current is ListBoxItem listBoxItem)
				{
					container = listBoxItem;
					break;
				}
			}
			if (container == null && position.HasValue)
			{
				container = global::ForkPlus.UI.ItemsControlExtensions.GetContainerAtPoint<ListBoxItem>(WorkspacesListBox, position.Value);
			}
			if (container is not { DataContext: WorkspaceViewModel dataContext })
			{
				return false;
			}

			WorkspacesListBox.SelectedItem = dataContext;
			WorkspacesListBox.ContextMenu.Items.Clear();
			WorkspacesListBox.ContextMenu.SetItems(GetContextMenu(dataContext));
			return WorkspacesListBox.ContextMenu.Items.Count > 0;
		}

		private void WorkspacesListBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.F2 && (sender as ListBox)?.ContainerFromElement(e.Source as global::Avalonia.Visual) /* Migration note：WPF 双参静态方法 → 扩展方法实例调用 */ is ListBoxItem { DataContext: WorkspaceViewModel dataContext })
			{
				BeginEditWorkspace(dataContext);
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
				BeginEditWorkspace(workspaceViewModel);
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
			BeginEditWorkspace(workspaceViewModel);
		}

		private void BeginEditWorkspace(WorkspaceViewModel workspaceViewModel)
		{
			if (workspaceViewModel == null)
			{
				return;
			}
			SelectAndFocusWorkspace(workspaceViewModel);
			workspaceViewModel.IsInEditMode = true;
			Avalonia.Threading.Dispatcher.UIThread.Post(delegate
			{
				foreach (TextBox textBox in global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(WorkspacesListBox).OfType<TextBox>())
				{
					if (textBox.DataContext == workspaceViewModel)
					{
						textBox.Text = workspaceViewModel.Name;
						textBox.Focus();
						textBox.SelectAll();
						break;
					}
				}
			}, Avalonia.Threading.DispatcherPriority.Background);
		}

		private void WorkspaceNameTextBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (sender is not TextBox textBox || textBox.DataContext is not WorkspaceViewModel workspaceViewModel)
			{
				return;
			}
			if (e.Key == Key.Escape)
			{
				CommitWorkspaceNameEdit(workspaceViewModel, textBox.Text, save: false);
				e.Handled = true;
			}
		}

		private void WorkspaceNameTextBox_KeyUp(object sender, KeyEventArgs e)
		{
			if (sender is not TextBox textBox || textBox.DataContext is not WorkspaceViewModel workspaceViewModel)
			{
				return;
			}
			if (e.Key == Key.Return || e.Key == Key.Enter)
			{
				CommitWorkspaceNameEdit(workspaceViewModel, textBox.Text, save: true);
				e.Handled = true;
			}
		}

		private void WorkspaceNameTextBox_LostFocus(object sender, RoutedEventArgs e)
		{
			if (sender is TextBox textBox && textBox.DataContext is WorkspaceViewModel workspaceViewModel && workspaceViewModel.IsInEditMode)
			{
				CommitWorkspaceNameEdit(workspaceViewModel, textBox.Text, save: true);
			}
		}

		private void CommitWorkspaceNameEdit(WorkspaceViewModel workspaceViewModel, string text, bool save)
		{
			if (save)
			{
				workspaceViewModel.DisplayName = text;
			}
			workspaceViewModel.IsInEditMode = false;
			WorkspacesListBox.Focus();
		}

		private void RemoveWorkspace(WorkspaceViewModel workspace)
		{
			if (new MessageBoxWindow("Do you want to delete the workspace '" + workspace.Name + "'?", "You can't undo this action", "Delete", "Cancel", showCancelButton: true, 500.0)
				.SetOwnerAndCenter(this).ShowDialog().GetValueOrDefault()) // Migration note：WPF { Owner=.., WindowStartupLocation=CenterOwner } → 链式扩展。
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
