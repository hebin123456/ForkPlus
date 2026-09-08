using System;
using ForkPlus.UI.WpfCompat;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Controls.Shapes;

namespace ForkPlus.UI.Controls
{
	public class ClosableTabItem : TabItem
	{
		// 关键：让隐式 ControlTheme `{x:Type controls:ClosableTabItem}` 能命中该控件
		//（否则会回落到基类 TabItem 模板，导致缺少：仓库名/颜色标记/关闭按钮/右键菜单）。
		protected override Type StyleKeyOverride => typeof(ClosableTabItem);

		private const string CloseButton = "PART_Close";

		private const string TabHeader = "PART_Header";

		private const string TitleTextBlockName = "PART_Title";

		private const string RepositoryManagerTabHeader = "Repository Manager";

		public static readonly SolidColorBrush IsDirtyDefaultBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E8E91"));

		public static readonly global::Avalonia.StyledProperty<SolidColorBrush> TagBrushProperty =
    global::Avalonia.AvaloniaProperty.Register<ClosableTabItem, SolidColorBrush>("TagBrush");

		public static readonly global::Avalonia.StyledProperty<bool> IsDirtyProperty =
    global::Avalonia.AvaloniaProperty.Register<ClosableTabItem, bool>("IsDirty", false);

		private Point _dragStartPoint;

		// 若模板/主题未生效（回落到默认 TabItem 模板），用运行时 header chrome 兜底：
		// 右键菜单/关闭按钮/颜色标记不会缺失。
		private bool _useFallbackHeaderChrome;
		private CenteredDockPanel _fallbackHeader;
		private Ellipse _fallbackEllipse;
		private EditableTextBlock _fallbackTitle;
		private Button _fallbackClose;
		private string _titleText = string.Empty;

		public TabItemMode Mode { get; private set; }

		[Null]
		public RepositoryManagerUserControl RepositoryManagerUserControl { get; private set; }

		[Null]
		public RepositoryUserControl RepositoryUserControl { get; private set; }

		[Null]
		public GitMmUserControl GitMmUserControl { get; private set; }

		public SolidColorBrush TagBrush
		{
			get
			{
				return (SolidColorBrush)GetValue(TagBrushProperty);
			}
			set
			{
				SetValue(TagBrushProperty, value);
			}
		}

		public bool IsDirty
		{
			get
			{
				return (bool)GetValue(IsDirtyProperty);
			}
			set
			{
				SetValue(IsDirtyProperty, value);
			}
		}

		private EditableTextBlock TitleTextBlock => _fallbackTitle ?? this.GetTemplateChild("PART_Title") as EditableTextBlock;

		protected override void OnPropertyChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == TagBrushProperty || change.Property == IsDirtyProperty)
			{
				SyncHeaderChromeFromState();
			}
		}

		public ClosableTabItem()
		{
			// 强制应用自定义 ControlTheme（避免回落到默认 TabItem 模板）。
			ControlTheme tabTheme = null;
			if (Application.Current?.TryFindResource("ClosableTabItemTheme", out var byName) == true)
			{
				tabTheme = byName as ControlTheme;
			}
			if (tabTheme == null && Application.Current?.TryFindResource(typeof(ClosableTabItem), out var byType) == true)
			{
				tabTheme = byType as ControlTheme;
			}
			if (tabTheme != null)
			{
				base.Theme = tabTheme;
			}

			base.PointerPressed += TabItem_PreviewMouseDown;
			base.PointerMoved += TabItem_PreviewMouseMove;
			// Migration note：WPF UIElement.Drop += handler（实例 CLR 事件）在 Avalonia 12 的 TabItem 上
			// 不存在（CS0117）；等价写法是 AddHandler(DragDrop.DropEvent, handler)（Interactive 路由
			// 事件订阅，默认 Direct|Bubble，与 WPF Drop 冒泡行为一致）。
			this.AddHandler(global::Avalonia.Input.DragDrop.DropEvent, TabItem_Drop);
			WeakEventManager<NotificationCenter, EventArgs<RepositoryUserControl>>.AddHandler(NotificationCenter.Current, "RepositoryUserControlTitleChanged", RepositoryUserControlTitleChanged);
			WeakEventManager<NotificationCenter, EventArgs<RepositoryUserControl>>.AddHandler(NotificationCenter.Current, "RepositoryUserControlColorChanged", RepositoryUserControlColorChanged);
			WeakEventManager<NotificationCenter, EventArgs<RepositoryUserControl>>.AddHandler(NotificationCenter.Current, "RepositoryUserControlIsDirtyChanged", RepositoryUserControlIsDirtyChanged);
			WeakEventManager<NotificationCenter, EventArgs<RepositoryManager.Repository>>.AddHandler(NotificationCenter.Current, "RepositoryColorChanged", RepositoryColorChanged);
		}

		public void Close()
		{
			GetOwnerTabControl()?.RemoveTab(this);
		}

		protected override void OnApplyTemplate(global::Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			if (this.GetTemplateChild("PART_Close") is Button button)
			{
				button.Click -= CloseButton_Click;
				button.Click += CloseButton_Click;
				button.RemoveHandler(InputElement.PointerPressedEvent, CloseButton_PointerPressed);
				button.AddHandler(InputElement.PointerPressedEvent, CloseButton_PointerPressed, global::Avalonia.Interactivity.RoutingStrategies.Tunnel | global::Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
				button.RemoveHandler(InputElement.PointerReleasedEvent, CloseButton_PointerReleased);
				button.AddHandler(InputElement.PointerReleasedEvent, CloseButton_PointerReleased, global::Avalonia.Interactivity.RoutingStrategies.Tunnel | global::Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
			}
			if (!(this.GetTemplateChild("PART_Header") is CenteredDockPanel centeredDockPanel))
			{
				_useFallbackHeaderChrome = true;
				EnsureFallbackHeaderChrome();
				return;
			}
			_useFallbackHeaderChrome = false;
			centeredDockPanel.PointerPressed += delegate(object s, global::Avalonia.Input.PointerPressedEventArgs e)
			{
				// Migration note：WPF e.MiddleButton == MouseButtonState.Pressed（查鼠标中键状态）在
				// Avalonia 的 PointerPressedEventArgs 上不存在（CS1061）；等价物是当前指针点位的
				// PointerPointProperties.IsMiddleButtonPressed。
				if (e.GetCurrentPoint(null).Properties.IsMiddleButtonPressed)
				{
					Close();
				}
			};
			RefreshHeaderChrome();
			SyncHeaderChromeFromState();
		}

		private void CloseButton_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
		{
			e.Handled = true;
			Close();
		}

		private void CloseButton_PointerPressed(object sender, global::Avalonia.Input.PointerPressedEventArgs e)
		{
			if (!e.GetCurrentPoint(sender as Control).Properties.IsLeftButtonPressed)
			{
				return;
			}
			e.Handled = true;
			Close();
		}

		private void CloseButton_PointerReleased(object sender, global::Avalonia.Input.PointerReleasedEventArgs e)
		{
			if (e.InitialPressMouseButton != MouseButton.Left)
			{
				return;
			}
			e.Handled = true;
			Close();
		}

		private void EnsureFallbackHeaderChrome()
		{
			if (_fallbackHeader == null)
			{
				_fallbackHeader = new CenteredDockPanel
				{
					Background = Brushes.Transparent,
					Name = "PART_Header",
				};

				_fallbackEllipse = new Ellipse
				{
					Width = 8,
					Height = 8,
					Margin = new Thickness(3, 0, 3, -2),
					HorizontalAlignment = HorizontalAlignment.Left,
					VerticalAlignment = VerticalAlignment.Center,
					StrokeThickness = 2,
				};
				DockPanel.SetDock(_fallbackEllipse, Dock.Left);
				_fallbackHeader.Children.Add(_fallbackEllipse);

				_fallbackClose = new Button
				{
					Width = 16,
					Height = 16,
					HorizontalAlignment = HorizontalAlignment.Right,
					VerticalAlignment = VerticalAlignment.Center,
				};
				if (Application.Current?.TryFindResource("CloseButtonStyle", out var closeButtonTheme) == true && closeButtonTheme is ControlTheme controlTheme)
				{
					_fallbackClose.Theme = controlTheme;
				}
				_fallbackClose.Click += delegate { Close(); };
				_fallbackClose.AddHandler(InputElement.PointerPressedEvent, CloseButton_PointerPressed, global::Avalonia.Interactivity.RoutingStrategies.Tunnel | global::Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
				_fallbackClose.AddHandler(InputElement.PointerReleasedEvent, CloseButton_PointerReleased, global::Avalonia.Interactivity.RoutingStrategies.Tunnel | global::Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
				DockPanel.SetDock(_fallbackClose, Dock.Right);
				_fallbackHeader.Children.Add(_fallbackClose);

				_fallbackTitle = new EditableTextBlock
				{
					Height = 22,
					Margin = new Thickness(0, 1, 0, 1),
					Padding = new Thickness(0, 2, 0, 2),
					HorizontalAlignment = HorizontalAlignment.Left,
					Name = "PART_Title",
				};
				_fallbackHeader.Children.Add(_fallbackTitle);

				_fallbackHeader.PointerPressed += delegate (object s, global::Avalonia.Input.PointerPressedEventArgs e)
				{
					if (e.GetCurrentPoint(null).Properties.IsMiddleButtonPressed)
					{
						Close();
					}
				};
			}

			_useFallbackHeaderChrome = true;
			UpdateFallbackHeaderFromState();
			base.Header = _fallbackHeader;
			RefreshHeaderChrome();
		}

		private void UpdateFallbackHeaderFromState()
		{
			if (_fallbackTitle != null)
			{
				_fallbackTitle.Value = _titleText;
			}
			if (_fallbackEllipse != null)
			{
				bool show = Mode == TabItemMode.Repository || Mode == TabItemMode.GitMm || IsDirty || TagBrush != null;
				_fallbackEllipse.IsVisible = show;
				SolidColorBrush b = TagBrush;
				if (IsDirty && b == null)
				{
					b = IsDirtyDefaultBrush;
				}
				IBrush fillBrush = b;
				_fallbackEllipse.Stroke = b;
				_fallbackEllipse.Fill = fillBrush ?? Brushes.Transparent;
			}
		}

		public void ActivateRepositoryManagerMode()
		{
			RepositoryUserControl = null;
			GitMmUserControl = null;
			RepositoryManagerUserControl = new RepositoryManagerUserControl();
			Mode = TabItemMode.RepositoryManager;
			VisualTreeAttachmentHelper.TrySetContent(this, RepositoryManagerUserControl, GetType().Name + ".Content");
			RefreshTitle();
			RefreshHeaderChrome();
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
			RefreshTitle();
			RefreshHeaderChrome();
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
			RefreshTitle();
			RefreshHeaderChrome();
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

		private void TabItem_PreviewMouseDown(object sender, global::Avalonia.Input.PointerPressedEventArgs e)
		{
			_dragStartPoint = e.GetPosition(null);
			if (e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
			{
				e.Handled = true;
				Close();
				return;
			}
			if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
			{
				IsSelected = true;
			}
		}

		private void TabItem_PreviewMouseMove(object sender, global::Avalonia.Input.PointerEventArgs e)
		{
			// Migration note：WPF Mouse.PrimaryDevice.LeftButton（全局查询鼠标左键状态）在 Avalonia 无
			// 全局鼠标状态 API（CS0117）；拖动语义等价物 = 当前 PointerMoved 事件指针点位的
			// IsLeftButtonPressed（按下并移动才会走到这里，行为一致）。
			if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed && CursorReachedDropDistance(e.GetPosition(null)) && !(e.Source is Button) && e.Source is ClosableTabItem closableTabItem)
			{
				global::ForkPlus.UI.WpfCompat.DragDropLauncher.DoDragDrop(closableTabItem, new WeakReference<ClosableTabItem>(closableTabItem), (global::Avalonia.Input.DragDropEffects)7);
			}
		}

		private void TabItem_Drop(object sender, DragEventArgs e)
		{
			// Migration note：WPF DataObject.GetData(Type) 以类型全名作隐式格式名；Avalonia 侧的
			// WpfDataObject 只有 GetData(string)（CS1503），且 DragDropLauncher.DoDragDrop 把自定义
			// 对象统一存为 "ForkPlusItem" 格式（WpfCompat.Batch2.cs ToTransfer 默认分支），
			// 故按该格式名读取，读取结果仍以类型模式匹配校验，语义等价。
			if (e.WpfData().GetData("ForkPlusItem") is WeakReference<ClosableTabItem> weakReference && weakReference.TryGetTarget(out var target) && e.Source is ClosableTabItem closableTabItem)
			{
				ClosableTabControl closableTabControl = closableTabItem.GetOwnerTabControl();
				if (closableTabControl != null && closableTabItem != target)
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
				SetTitleText(RepositoryUserControl.RepositoryTitle);
			}
			else if (Mode == TabItemMode.GitMm)
			{
				SetTitleText(GitMmUserControl?.WorkspaceTitle ?? "git mm");
			}
			else
			{
				SetTitleText(PreferencesLocalization.Translate("Repository Manager", ForkPlusSettings.Default.UiLanguage));
			}
			RefreshHeaderChrome();
		}

		private void SetTitleText(string title)
		{
			title = title ?? string.Empty;
			_titleText = title;
			EnsureFallbackHeaderChrome();
			if (_fallbackTitle != null)
			{
				_fallbackTitle.Value = title;
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
			if (Mode == TabItemMode.Repository && RepositoryUserControl?.GitModule != null && e.Value.Path == PathHelper.Normalize(RepositoryUserControl.GitModule.Path))
			{
				RepositoryManager.Repository? repository = RepositoryManager.Instance.Repositories.FirstItemStruct((RepositoryManager.Repository x) => x.Path == e.Value.Path);
				TagBrush = RepositoryColorsUserControl.GetBrush(repository?.Color ?? RepositoryColor.None);
				return;
			}
			if (Mode == TabItemMode.GitMm && GitMmUserControl != null && e.Value.Path == PathHelper.Normalize(GitMmUserControl.WorkspacePath))
			{
				RepositoryManager.Repository? repository = RepositoryManager.Instance.Repositories.FirstItemStruct((RepositoryManager.Repository x) => x.Path == e.Value.Path);
				TagBrush = RepositoryColorsUserControl.GetBrush(repository?.Color ?? RepositoryColor.None);
			}
		}

		private void RefreshHeaderChrome()
		{
			ContextMenu contextMenu = GetContextMenu();
			global::ForkPlus.UI.MenuExtensions.AttachCloseOnLeafItemClick(contextMenu);
			global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AttachAutoDismiss(contextMenu, _fallbackHeader ?? (Control)this);
			base.ContextMenu = contextMenu;

			string toolTip = GetToolTip();
			global::Avalonia.Controls.ToolTip.SetTip(this, toolTip);

			if (_useFallbackHeaderChrome)
			{
				if (_fallbackHeader != null)
				{
					_fallbackHeader.ContextMenu = contextMenu;
					global::Avalonia.Controls.ToolTip.SetTip(_fallbackHeader, toolTip);
					UpdateFallbackHeaderFromState();
				}
				return;
			}

			if (this.GetTemplateChild("PART_Header") is CenteredDockPanel centeredDockPanel)
			{
				centeredDockPanel.ContextMenu = contextMenu;
				global::Avalonia.Controls.ToolTip.SetTip(centeredDockPanel, toolTip);
			}
			SyncHeaderChromeFromState();
		}

		private void SyncHeaderChromeFromState()
		{
			if (_useFallbackHeaderChrome)
			{
				UpdateFallbackHeaderFromState();
				return;
			}

			if (this.GetTemplateChild("PART_Title") is EditableTextBlock titleTextBlock)
			{
				titleTextBlock.Value = _titleText;
			}
			if (this.GetTemplateChild("PART_Color") is Ellipse ellipse)
			{
				SolidColorBrush brush = TagBrush;
				if (IsDirty && brush == null)
				{
					brush = IsDirtyDefaultBrush;
				}
				ellipse.IsVisible = IsDirty || brush != null;
				ellipse.Stroke = brush;
				IBrush fillBrush = brush;
				ellipse.Fill = fillBrush ?? Brushes.Transparent;
			}
		}

		private ClosableTabControl GetOwnerTabControl()
		{
			return ItemsControl.ItemsControlFromItemContainer(this) as ClosableTabControl
				?? base.Parent as ClosableTabControl
				?? global::Avalonia.VisualTree.VisualExtensions.GetVisualAncestors(this).OfType<ClosableTabControl>().FirstOrDefault();
		}

		private ContextMenu GetContextMenu()
		{
			ContextMenu contextMenu = new ContextMenu();
			MenuItem menuItem = new MenuItem();
			menuItem.Header = PreferencesLocalization.MenuHeader("Close All");
			menuItem.Click += delegate
			{
				GetOwnerTabControl()?.RemoveAllTabs();
			};
			contextMenu.Items.Add(menuItem);
			MenuItem menuItem2 = new MenuItem();
			menuItem2.Header = PreferencesLocalization.MenuHeader("Close All But This");
			menuItem2.Click += delegate
			{
				GetOwnerTabControl()?.RemoveAllTabs(this);
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
			// 若该仓是某个 git mm 工作区的子仓，提供“打开 git mm 仓”快捷入口
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
			return global::ForkPlus.UI.WpfCompat.StyleCompat.WithStyle(new MenuItem
			{
				Header = new RepositoryColorsUserControl(repository)			},global::ForkPlus.UI.Theme.CustomContentMenuItemStyle
);
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
