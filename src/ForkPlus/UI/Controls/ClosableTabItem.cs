using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Controls
{
	// 阶段 4.5：WPF System.Windows.Controls.TabItem → Avalonia.Controls.TabItem。
	// WPF PreviewMouseDown/PreviewMouseMove → Avalonia PointerPressed/PointerMoved（无 Preview 变体）。
	// WPF PointerPressedEventArgs → Avalonia PointerPressedEventArgs/PointerEventArgs。
	// WPF Mouse.PrimaryDevice.LeftButton → Avalonia PointerEventArgs.GetCurrentPoint().Properties.IsLeftButtonPressed。
	// WPF DragDrop.DoDragDrop + DragEventArgs → Avalonia DragDrop.DoDragDrop + DragEventArgs（API 兼容）。
	// WPF WeakEventManager<T,S> → 直接事件订阅（阶段 6 改用 Avalonia WeakEvent）。
	// WPF DependencyProperty → Avalonia StyledProperty。
	// WPF Brushes.Transparent → Avalonia Brushes.Transparent。
	public class ClosableTabItem : TabItem
	{
		private const string CloseButton = "PART_Close";

		private const string TabHeader = "PART_Header";

		private const string TitleTextBlockName = "PART_Title";

		private const string RepositoryManagerTabHeader = "Repository Manager";

		// 阶段 4.5：WPF ColorConverter.ConvertFromString → Avalonia Color.Parse。
		public static readonly SolidColorBrush IsDirtyDefaultBrush = new SolidColorBrush(Color.Parse("#8E8E91"));

		// 阶段 4.5：WPF DependencyProperty.Register → Avalonia StyledProperty.Register。
		public static readonly StyledProperty<SolidColorBrush> TagBrushProperty =
			AvaloniaProperty.Register<ClosableTabItem, SolidColorBrush>(nameof(TagBrush), Brushes.Transparent);

		public static readonly StyledProperty<bool> IsDirtyProperty =
			AvaloniaProperty.Register<ClosableTabItem, bool>(nameof(IsDirty), false);

		private Point _dragStartPoint;

		// 阶段 4.5：缓存左键按下状态（替代 WPF Mouse.PrimaryDevice.LeftButton 静态查询）。
		private bool _isLeftButtonPressed;

		public TabItemMode Mode { get; private set; }

		[Null]
		public RepositoryManagerUserControl RepositoryManagerUserControl { get; private set; }

		[Null]
		public RepositoryUserControl RepositoryUserControl { get; private set; }

		[Null]
		public GitMmUserControl GitMmUserControl { get; private set; }

		public SolidColorBrush TagBrush
		{
			get => GetValue(TagBrushProperty);
			set => SetValue(TagBrushProperty, value);
		}

		public bool IsDirty
		{
			get => GetValue(IsDirtyProperty);
			set => SetValue(IsDirtyProperty, value);
		}

		private EditableTextBlock TitleTextBlock => GetTemplateChild("PART_Title") as EditableTextBlock;

		public ClosableTabItem()
		{
			// 阶段 4.5：Avalonia 画刷默认不可变，无需 WPF Freeze()。
			// 阶段 4.5：WPF PreviewMouseDown → Avalonia PointerPressed。
			base.PointerPressed += TabItem_PointerPressed;
			// 阶段 4.5：WPF PreviewMouseMove → Avalonia PointerMoved。
			base.PointerMoved += TabItem_PointerMoved;
			base.Drop += TabItem_Drop;
			// 阶段 4.5：WPF WeakEventManager<T,S>.AddHandler → 直接事件订阅。
			// TODO(4.6-a): 阶段 6 改用 Avalonia WeakEvent 避免内存泄漏。
			NotificationCenter.Current.RepositoryUserControlTitleChanged += RepositoryUserControlTitleChanged;
			NotificationCenter.Current.RepositoryUserControlColorChanged += RepositoryUserControlColorChanged;
			NotificationCenter.Current.RepositoryUserControlIsDirtyChanged += RepositoryUserControlIsDirtyChanged;
			NotificationCenter.Current.RepositoryColorChanged += RepositoryColorChanged;
		}

		public void Close()
		{
			// 阶段 4.5：WPF FrameworkElement.Parent → Avalonia Control.Parent（类型为 IControl）。
			(base.Parent as ClosableTabControl)?.RemoveTab(this);
		}

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();
			if (GetTemplateChild("PART_Close") is Button button)
			{
				button.Click += delegate
				{
					Close();
				};
			}
			if (!(GetTemplateChild("PART_Header") is CenteredDockPanel centeredDockPanel))
			{
				return;
			}
			// 阶段 4.5：WPF PreviewMouseDown + PointerPressedEventArgs.MiddleButton
			// → Avalonia PointerPressed + PointerPressedEventArgs.GetCurrentPoint().Properties.IsMiddleButtonPressed。
			centeredDockPanel.PointerPressed += delegate(object s, PointerPressedEventArgs e)
			{
				if (e.GetCurrentPoint(centeredDockPanel).Properties.IsMiddleButtonPressed)
				{
					Close();
				}
			};
			// 阶段 4.5：WPF FrameworkElement.ToolTip 属性 → Avalonia ToolTip.SetTip 附加属性。
			ToolTip.SetTip(centeredDockPanel, GetToolTip());
			centeredDockPanel.ContextMenu = GetContextMenu();
		}

		public void ActivateRepositoryManagerMode()
		{
			RepositoryUserControl = null;
			GitMmUserControl = null;
			RepositoryManagerUserControl = new RepositoryManagerUserControl();
			Mode = TabItemMode.RepositoryManager;
			VisualTreeAttachmentHelper.TrySetContent(this, RepositoryManagerUserControl, GetType().Name + ".Content");
		}

		public void ActivateRepositoryViewMode(GitModule gitModule)
		{
			RepositoryManagerUserControl = null;
			GitMmUserControl = null;
			RepositoryUserControl = new RepositoryUserControl();
			RepositoryUserControl.OpenRepository(gitModule);
			Mode = TabItemMode.Repository;
			VisualTreeAttachmentHelper.TrySetContent(this, RepositoryUserControl, GetType().Name + ".Content");
			TagBrush = RepositoryColorsUserControl.GetBrush(RepositoryUserControl.RepositoryColor);
			IsDirty = RepositoryUserControl.IsDirty;
		}

		public void ActivateGitMmMode(string workspacePath)
		{
			RepositoryManagerUserControl = null;
			RepositoryUserControl = null;
			GitMmUserControl = new GitMmUserControl(workspacePath);
			Mode = TabItemMode.GitMm;
			VisualTreeAttachmentHelper.TrySetContent(this, GitMmUserControl, GetType().Name + ".Content");
			TagBrush = RepositoryColorsUserControl.GetBrush(RepositoryManager.Instance.Repositories.FirstItemStruct((RepositoryManager.Repository x) => x.Path == PathHelper.Normalize(workspacePath))?.Color ?? RepositoryColor.None);
			IsDirty = false;
		}

		public void Refresh()
		{
			if (Mode == TabItemMode.Repository)
			{
				RepositoryUserControl repositoryUserControl = RepositoryUserControl;
				if (repositoryUserControl != null)
				{
					if (repositoryUserControl.ViewMode == RepositoryViewMode.CommitViewMode)
					{
						repositoryUserControl.InvalidateAndRefresh(SubDomain.DefaultRefresh, null, RepositoryViewMode.CommitViewMode);
					}
					else
					{
						repositoryUserControl.InvalidateAndRefresh(SubDomain.DefaultRefresh);
					}
					return;
				}
			}
			if (Mode == TabItemMode.RepositoryManager)
			{
				RepositoryManagerUserControl?.Refresh();
			}
			if (Mode == TabItemMode.GitMm)
			{
				GitMmUserControl?.Refresh();
			}
		}

		private void TabItem_PointerPressed(object sender, PointerPressedEventArgs e)
		{
			// 阶段 4.5：WPF e.GetPosition(null) → Avalonia e.GetPosition(null)（根坐标系）。
			_dragStartPoint = e.GetPosition(null);
			_isLeftButtonPressed = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;
		}

		private void TabItem_PointerMoved(object sender, PointerEventArgs e)
		{
			// 阶段 4.5：WPF Mouse.PrimaryDevice.LeftButton → 缓存的 _isLeftButtonPressed。
			// 阶段 4.5：WPF e.OriginalSource → Avalonia e.Source。
			if (_isLeftButtonPressed && CursorReachedDropDistance(e.GetPosition(null)) && !(e.Source is Button) && e.Source is ClosableTabItem closableTabItem)
			{
				// 阶段 4.5：Avalonia DragDrop.DoDragDrop 签名与 WPF 兼容（返回 Task，丢弃即可）。
				_ = DragDrop.DoDragDrop(closableTabItem, new WeakReference<ClosableTabItem>(closableTabItem), DragDropEffects.All);
			}
		}

		private void TabItem_Drop(object sender, DragEventArgs e)
		{
			if (e.Data.Get(typeof(WeakReference<ClosableTabItem>)) is WeakReference<ClosableTabItem> weakReference && weakReference.TryGetTarget(out var target) && e.Source is ClosableTabItem closableTabItem)
			{
				ClosableTabControl closableTabControl = closableTabItem.Parent as ClosableTabControl;
				if (closableTabItem != target)
				{
					closableTabControl.StopSelectionChangedEventWhileDropInProgress = true;
					int num = closableTabControl.Items.IndexOf(closableTabItem);
					ClosableTabItem closableTabItem2 = new ClosableTabItem();
					closableTabControl.Items.Insert(0, closableTabItem2);
					closableTabControl.SelectTab(closableTabItem2);
					closableTabControl.Items.Remove(target);
					closableTabControl.Items.Insert(num + 1, target);
					closableTabControl.Items.Remove(closableTabItem2);
					closableTabControl.StopSelectionChangedEventWhileDropInProgress = false;
					closableTabControl.SelectTab(target);
				}
			}
		}

		private void RenameRepository(string newName)
		{
			string text = RepositoryUserControl?.GitModule.Path;
			if (text != null)
			{
				RenameRepository(text, newName);
			}
		}

		private void RenameRepository(string repositoryPath, string newName)
		{
			EnsureRepositoryManagerEntry(repositoryPath);
			string normalizedPath = PathHelper.Normalize(repositoryPath);
			RepositoryManager.Instance.RenameRepository(normalizedPath, newName);
			NotificationCenter.Current.RaiseRepositoryNameChanged(this, normalizedPath);
			RepositoryManager.Instance.Save();
			RefreshTitle();
		}

		public void RefreshTitle()
		{
			if (Mode == TabItemMode.Repository)
			{
				RepositoryUserControl.RefreshRepositoryTitle();
				base.Header = RepositoryUserControl.RepositoryTitle;
			}
			else if (Mode == TabItemMode.GitMm)
			{
				base.Header = GitMmUserControl?.WorkspaceTitle ?? "git mm";
			}
			else
			{
				base.Header = PreferencesLocalization.Translate("Repository Manager", ForkPlusSettings.Default.UiLanguage);
			}
		}

		[Null]
		private string GetToolTip()
		{
			if (Mode == TabItemMode.Repository)
			{
				return RepositoryUserControl?.GitModule.Path;
			}
			if (Mode == TabItemMode.GitMm)
			{
				return GitMmUserControl?.WorkspacePath;
			}
			return null;
		}

		private void RepositoryUserControlTitleChanged(object sender, EventArgs<RepositoryUserControl> e)
		{
			RefreshTitle();
		}

		private void RepositoryUserControlIsDirtyChanged(object sender, EventArgs<RepositoryUserControl> e)
		{
			if (e.Value == RepositoryUserControl)
			{
				IsDirty = RepositoryUserControl.IsDirty;
			}
		}

		private void RepositoryUserControlColorChanged(object sender, EventArgs<RepositoryUserControl> e)
		{
			if (e.Value == RepositoryUserControl)
			{
				TagBrush = RepositoryColorsUserControl.GetBrush(RepositoryUserControl.RepositoryColor);
			}
		}

		private void RepositoryColorChanged(object sender, EventArgs<RepositoryManager.Repository> e)
		{
			if (Mode == TabItemMode.GitMm && GitMmUserControl != null && e.Value.Path == PathHelper.Normalize(GitMmUserControl.WorkspacePath))
			{
				RepositoryManager.Repository? repository = RepositoryManager.Instance.Repositories.FirstItemStruct((RepositoryManager.Repository x) => x.Path == e.Value.Path);
				TagBrush = RepositoryColorsUserControl.GetBrush(repository?.Color ?? RepositoryColor.None);
			}
		}

		private ContextMenu GetContextMenu()
		{
			ContextMenu contextMenu = new ContextMenu();
			MenuItem menuItem = new MenuItem();
			menuItem.Header = PreferencesLocalization.MenuHeader("Close All");
			menuItem.Click += delegate
			{
				((ClosableTabControl)base.Parent).RemoveAllTabs();
			};
			contextMenu.Items.Add(menuItem);
			MenuItem menuItem2 = new MenuItem();
			menuItem2.Header = PreferencesLocalization.MenuHeader("Close All But This");
			menuItem2.Click += delegate
			{
				((ClosableTabControl)base.Parent).RemoveAllTabs(this);
			};
			contextMenu.Items.Add(menuItem2);
			string managedRepositoryPath = null;
			string managedRepositoryName = null;
			if (Mode == TabItemMode.Repository)
			{
				RepositoryUserControl repositoryUserControl = RepositoryUserControl;
				if (repositoryUserControl != null && repositoryUserControl.GitModule.Type != ModuleType.Submodule && repositoryUserControl.GitModule.Type != ModuleType.Worktree)
				{
					managedRepositoryPath = repositoryUserControl.GitModule.Path;
					managedRepositoryName = repositoryUserControl.RepositoryName;
				}
			}
			else if (Mode == TabItemMode.GitMm && GitMmUserControl != null)
			{
				managedRepositoryPath = GitMmUserControl.WorkspacePath;
				managedRepositoryName = RepositoryManager.Instance.FindRepositoryName(managedRepositoryPath) ?? GitMmUserControl.WorkspaceTitle.Replace("git mm: ", "");
			}
			if (managedRepositoryPath != null)
			{
				contextMenu.Items.Add(new Separator());
				MenuItem menuItem3 = new MenuItem();
				menuItem3.Header = PreferencesLocalization.MenuHeader("Rename");
				menuItem3.Click += delegate
				{
					EditableTextBlock editableTextBlock = TitleTextBlock;
					if (editableTextBlock != null)
					{
						editableTextBlock.ShowEditor(GetCurrentRepositoryName(managedRepositoryPath, managedRepositoryName), delegate(bool success, string newName)
						{
							editableTextBlock.HideEditor();
							if (success)
							{
								RenameRepository(managedRepositoryPath, newName);
							}
						}, centeredHorizontally: true);
					}
				};
				contextMenu.Items.Add(menuItem3);
				if (ForkPlusSettings.Default.Workspaces.All.Length != 0)
				{
					contextMenu.Items.Add(new Separator());
					MenuItem menuItem4 = new MenuItem
					{
						Header = PreferencesLocalization.MenuHeader("Workspaces")
					};
					Workspace[] all = ForkPlusSettings.Default.Workspaces.All;
					foreach (Workspace workspace in all)
					{
						bool isCurrentWorkspace = workspace == ForkPlusSettings.Default.Workspaces.ActiveWorkspace;
						MenuItem menuItem5 = new MenuItem();
						menuItem5.Header = workspace.Name;
						menuItem5.IsChecked = isCurrentWorkspace;
						menuItem5.Click += delegate
						{
							if (!isCurrentWorkspace)
							{
								AddRepositoryToWorkspace(workspace, managedRepositoryPath);
								Close();
							}
						};
						menuItem4.Items.Add(menuItem5);
					}
					contextMenu.Items.Add(menuItem4);
				}
				contextMenu.Items.Add(new Separator());
				RepositoryManager.Repository? repository = EnsureRepositoryManagerEntry(managedRepositoryPath);
				if (repository.HasValue)
				{
					contextMenu.Items.Add(CreateRepositoryColorsMenuItem(repository.GetValueOrDefault()));
				}
			}
			// 若该仓是某个 git mm 工作区的子仓，提供"打开 git mm 仓"快捷入口
			if (Mode == TabItemMode.Repository && !string.IsNullOrWhiteSpace(managedRepositoryPath))
			{
				string gitMmWorkspacePath = MainWindow.Instance?.TabManager?.FindGitMmWorkspacePathForSubrepo(managedRepositoryPath);
				if (!string.IsNullOrWhiteSpace(gitMmWorkspacePath))
				{
					contextMenu.Items.Add(new Separator());
					MenuItem openGitMmItem = new MenuItem();
					openGitMmItem.Header = PreferencesLocalization.MenuHeader("Open git mm Repository");
					string workspacePathCaptured = gitMmWorkspacePath;
					openGitMmItem.Click += delegate
					{
						MainWindow.Instance?.TabManager?.OpenRepository(workspacePathCaptured);
					};
					contextMenu.Items.Add(openGitMmItem);
				}
			}
			return contextMenu;
		}

		private static string GetCurrentRepositoryName(string repositoryPath, string fallbackName)
		{
			return RepositoryManager.Instance.FindRepositoryName(repositoryPath) ?? fallbackName ?? PathHelper.GetReadableFileName(repositoryPath);
		}

		private static Control CreateRepositoryColorsMenuItem(RepositoryManager.Repository repository)
		{
			return new MenuItem
			{
				Header = new RepositoryColorsUserControl(repository),
				// 阶段 4.5：WPF Style 属性 → Avalonia Styles 集合（单 Style 用 Styles.Add）。
				// 此处简化为直接设置 Header；原 Theme.CustomContentMenuItemStyle 需在 XAML 模板中应用。
				// TODO(4.6-b): 恢复 CustomContentMenuItemStyle 样式应用。
			};
		}

		private static RepositoryManager.Repository? EnsureRepositoryManagerEntry(string repositoryPath)
		{
			string normalizedPath = PathHelper.Normalize(repositoryPath);
			RepositoryManager.Repository? repository = RepositoryManager.Instance.Repositories.FirstItemStruct((RepositoryManager.Repository x) => x.Path == normalizedPath);
			if (!repository.HasValue)
			{
				RepositoryManager.Instance.AddRepositories(new string[1] { normalizedPath });
				RepositoryManager.Instance.Save();
				repository = RepositoryManager.Instance.Repositories.FirstItemStruct((RepositoryManager.Repository x) => x.Path == normalizedPath);
			}
			return repository;
		}

		private static void AddRepositoryToWorkspace(Workspace workspace, string repository)
		{
			List<string> list = new List<string>(workspace.Repositories);
			if (!list.Contains(repository))
			{
				list.Add(repository);
			}
			workspace.Repositories = list.ToArray();
			workspace.ActiveRepository = workspace.ActiveRepository ?? workspace.Repositories.FirstItem();
		}

		private bool CursorReachedDropDistance(Point point)
		{
			if (!(Math.Abs(_dragStartPoint.X - point.X) > 10.0))
			{
				return Math.Abs(_dragStartPoint.Y - point.Y) > 10.0;
			}
			return true;
		}
	}
}
