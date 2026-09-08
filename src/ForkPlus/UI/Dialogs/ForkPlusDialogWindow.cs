using System;
using ForkPlus.UI.WpfCompat;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ForkPlus.Git.Commands;
using ForkPlus.Services;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.UserControls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ForkPlus.UI.Dialogs
{
	public class ForkPlusDialogWindow : CustomWindow
	{
		private static readonly Uri ForkPlusLogo = new Uri("avares://ForkPlus/Assets/ForkPlusIcon.png");

		public static readonly Uri WarningIcon = new Uri("avares://ForkPlus/Assets/Warning.png");

		public static readonly Uri ErrorIcon = new Uri("avares://ForkPlus/Assets/Error.png");

		public static readonly Uri SuccessIcon = new Uri("avares://ForkPlus/Assets/CheckMarkStroked.png");

		private Image _logoImage;

		private Image _warningIcon;

		private bool _showWarningIcon;

		private bool _dialogChromeInitialized;

		private string _pendingDialogTitle;

		private string _pendingDialogDescription;

		private string _pendingSubmitButtonTitle;

		private string _pendingCancelButtonTitle;

		private bool? _pendingShowSubmitButton;

		private bool? _pendingShowCancelButton;

		// Migration note：构造期 SetStatus/ClearStatus 的 pending 缓冲。Avalonia 下 Footer 经
		// Dispatcher.Post 延迟到 chrome 初始化才创建（见 OnContentChanged），而 WPF 里
		// Style 触发器在 ctor 内即生效——子类构造函数里的 SetStatus（如
		// ConfigureSshKeysWindow.Refresh()）直接 NRE。Footer 就绪后回放。
		private ForkPlusDialogStatus? _pendingStatus;

		private string _pendingStatusMessage;

		private TextBlock _commandPreviewLabel;

	private TextBlock _commandPreviewTextBlock;

	// 预览文本外层 ScrollViewer：限制 MaxHeight 防止长命令撑高窗口挤掉确认按钮
	private ScrollViewer _commandPreviewScrollViewer;

	private Button _commandPreviewCopyButton;

	private bool _commandPreviewInitialized;

		public bool IsOperationInProgress { get; private set; }

		protected new bool ShowHeader { get; set; } = true;


		protected bool ShowLogo { get; set; } = true;


		protected bool ShowFooter { get; set; } = true;


		public bool ShowWarningIcon
		{
			get
			{
				return _showWarningIcon;
			}
			set
			{
				if (_showWarningIcon != value)
				{
					_showWarningIcon = value;
					if (_showWarningIcon)
					{
						AddWarningIcon();
					}
					else
					{
						RemoveWarningIcon();
					}
				}
			}
		}

		protected ForkPlusDialogFooter Footer { get; private set; }

		protected TextBlock TitleTextBlock { get; private set; }

	protected TextBlock DescriptionTextBlock { get; private set; }

	// Migration note：WPF 中 chrome（TitleTextBlock/DescriptionTextBlock）在构造期已同步初始化，
	// 子类构造函数可直接访问其属性；Avalonia 12 的 InitializeDialogChrome 延迟到
	// Initialized/UIThread.Post 才执行，构造期访问为 null（WelcomeWindow 构造 NRE 实证）。
	// 定制经 pending 机制在 AddDialogHeader 创建控件后应用。
	private Action<TextBlock> _pendingTitleCustomization;

	private Action<TextBlock> _pendingDescriptionCustomization;

	/// <summary>
	/// 定制标题 TextBlock（FontSize/Foreground/TextWrapping 等）。chrome 未初始化时
	/// 挂起待 AddDialogHeader 执行；已初始化则立即应用。构造期安全的等价写法。
	/// </summary>
	protected void CustomizeTitleTextBlock(Action<TextBlock> customization)
	{
		if (TitleTextBlock != null)
		{
			customization(TitleTextBlock);
		}
		else
		{
			_pendingTitleCustomization += customization;
		}
	}

	/// <summary>定制描述 TextBlock，语义同 CustomizeTitleTextBlock。</summary>
	protected void CustomizeDescriptionTextBlock(Action<TextBlock> customization)
	{
		if (DescriptionTextBlock != null)
		{
			customization(DescriptionTextBlock);
		}
		else
		{
			_pendingDescriptionCustomization += customization;
		}
	}

		public GitCommandResult GitResult { get; protected set; }

		protected string DialogTitle
		{
			get
			{
				return TitleTextBlock?.Text ?? _pendingDialogTitle;
			}
			set
			{
				_pendingDialogTitle = value;
				if (TitleTextBlock != null)
				{
					TitleTextBlock.Text = value;
				}
				base.Title = value;
			}
		}

		protected override void OnPropertyChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			// 有些对话框只设置 Window.Title（XAML 或本地化代码），不走 DialogTitle；
			// 为避免标题栏显示 "[Dialog Title]" 占位符，这里保持 Header 标题与 Window.Title 同步。
			if (change.Property == TitleProperty && TitleTextBlock != null)
			{
				string title = base.Title;
				if (!string.IsNullOrWhiteSpace(title))
				{
					TitleTextBlock.Text = title;
				}
			}
		}

		protected string DialogDescription
		{
			get
			{
				return DescriptionTextBlock?.Text ?? _pendingDialogDescription;
			}
			set
			{
				_pendingDialogDescription = value;
				if (DescriptionTextBlock != null)
				{
					string text = value ?? string.Empty;
					DescriptionTextBlock.Text = text;
					// 没有描述时不显示占位符，直接隐藏描述行，避免出现 "[Dialog Description]".
					DescriptionTextBlock.IsVisible = !string.IsNullOrWhiteSpace(text);
				}
			}
		}

		protected bool ShowSubmitButton
		{
			get
			{
				if (Footer == null)
				{
					return _pendingShowSubmitButton.GetValueOrDefault(true);
				}
				return Footer.SubmitButton.IsVisible == true;
			}
			set
			{
				_pendingShowSubmitButton = value;
				if (Footer != null)
				{
					Footer.SubmitButton.IsVisible = ((!value) ? false : true);
				}
			}
		}

		protected string SubmitButtonTitle
		{
			get
			{
				return (Footer?.SubmitButton.Content as string) ?? _pendingSubmitButtonTitle;
			}
			set
			{
				_pendingSubmitButtonTitle = value;
				if (Footer != null)
				{
					Footer.SubmitButton.Content = value;
				}
			}
		}

		protected bool ShowCancelButton
		{
			get
			{
				if (Footer == null)
				{
					return _pendingShowCancelButton.GetValueOrDefault(true);
				}
				return Footer.CancelButton.IsVisible == true;
			}
			set
			{
				_pendingShowCancelButton = value;
				if (Footer != null)
				{
					Footer.CancelButton.IsVisible = ((!value) ? false : true);
				}
			}
		}

		protected string CancelButtonTitle
		{
			get
			{
				return (Footer?.CancelButton.Content as string) ?? _pendingCancelButtonTitle;
			}
			set
			{
				_pendingCancelButtonTitle = value;
				if (Footer != null)
				{
					Footer.CancelButton.Content = value;
				}
			}
		}

		protected virtual bool IsSubmitAllowed => !IsOperationInProgress;

		protected virtual bool ApplyAutomaticLocalization => true;

		// Migration note：WPF ComponentDispatcher.IsThreadModal（Win32 消息循环模态标记）→
                // WindowDialogCompat.IsShownAsDialog（本窗口是否经 ShowDialog shim 打开）。
                private bool IsWindowModal => this.IsShownAsDialog();

		private IEnumerable<global::Avalonia.Input.InputElement> EditableControls => FindVisualChildren<Control>(this);

		private bool IsDesignMode => global::ForkPlus.DesignTimeHelper.IsInDesignMode();

		/// <summary>
		/// Migration note：WPF Window.OnActivated 虚方法。Avalonia 无此虚方法，
		/// 此处在构造时订阅 Activated 事件转发到本虚方法，子类重写签名保持 WPF 形态。
		/// </summary>
		protected virtual void OnActivated(EventArgs e)
		{
		}

		public ForkPlusDialogWindow(bool preventMainWindowRefresh = true)
		{
			// Migration note：WPF Window.OverridesDefaultStyle（无默认模板）在 Avalonia 无对应概念，移除。
			Activated += delegate
			{
				OnActivated(EventArgs.Empty);
			};
			if (!IsDesignMode)
			{
				MainWindow instance = MainWindow.Instance;
				if (instance != null)
				{
					this.SetOwnerCompat(instance);
					if (preventMainWindowRefresh)
					{
						instance.PreventRefreshAfterChildDialogClose(GetType().Name);
					}
				}
				base.WindowStartupLocation = global::Avalonia.Controls.WindowStartupLocation.CenterOwner;
			}
			base.ShowInTaskbar = false;
			ResizeMode = ResizeMode.NoResize;
			base.Initialized += ForkPlusDialogWindow_Initialized;
			base.Loaded += ForkPlusDialogWindow_Loaded;
			// Migration note：WPF OnContentChanged override → Avalonia ContentProperty 变更订阅转发（见 OnContentChanged）。
			PropertyChanged += delegate(object s, global::Avalonia.AvaloniaPropertyChangedEventArgs e)
			{
				if (e.Property == global::Avalonia.Controls.ContentControl.ContentProperty)
				{
					OnContentChanged(e.OldValue, e.NewValue);
				}
			};
			// Migration note：WPF `Style = TryFindResource(...) as Style`；资源实为 ControlTheme，
			// 经 StyleCompat.SetStyle 挂 Theme（base 不能作参数，等价 this）。
			global::ForkPlus.UI.WpfCompat.StyleCompat.SetStyle(this, Application.Current?.TryFindResource("ForkPlusDialogWindowStyle"));
			if (!IsDesignMode)
			{
				WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
			}
		}

		public void SetStatus(ForkPlusDialogStatus status, string message)
		{
			IsOperationInProgress = status == ForkPlusDialogStatus.InProgress;
			// Migration note：Footer 未创建（chrome 延迟初始化）时先缓存，AddFooter 完成后回放（WPF ctor 期 Style 即生效）。
			if (Footer == null)
			{
				_pendingStatus = status;
				_pendingStatusMessage = message;
				return;
			}
			if (status == ForkPlusDialogStatus.None)
			{
				ClearStatus();
				return;
			}
			string localizedMessage = PreferencesLocalization.Translate(message, ForkPlusSettings.Default.UiLanguage);
			Footer.StatusMessageTextBlock.Text = localizedMessage;
			global::Avalonia.Controls.ToolTip.SetTip(Footer.StatusMessageTextBlock,localizedMessage);
			Footer.StatusMessageTextBlock.IsVisible = true;
			if (status == ForkPlusDialogStatus.InProgress)
			{
				Footer.StatusImage.IsVisible = false;
				Footer.BusyIndicator.IsVisible = true;
				return;
			}
			Footer.BusyIndicator.IsVisible = false;
			Footer.StatusImage.IsVisible = true;
			switch (status)
			{
			case ForkPlusDialogStatus.Success:
				Footer.StatusImage.Source = new global::Avalonia.Media.Imaging.Bitmap(global::Avalonia.Platform.AssetLoader.Open(SuccessIcon));
				break;
			case ForkPlusDialogStatus.Warning:
				Footer.StatusImage.Source = new global::Avalonia.Media.Imaging.Bitmap(global::Avalonia.Platform.AssetLoader.Open(WarningIcon));
				break;
			case ForkPlusDialogStatus.Error:
				Footer.StatusImage.Source = new global::Avalonia.Media.Imaging.Bitmap(global::Avalonia.Platform.AssetLoader.Open(ErrorIcon));
				break;
			}
		}

		public void ClearStatus()
		{
			// Migration note：Footer 未创建时清掉 pending 即可（None 状态无需回放）。
			if (Footer == null)
			{
				_pendingStatus = null;
				_pendingStatusMessage = null;
				return;
			}
			Footer.StatusImage.IsVisible = false;
			Footer.StatusMessageTextBlock.IsVisible = false;
			Footer.BusyIndicator.IsVisible = false;
		}

		public void DisableEditableControls()
		{
			foreach (global::Avalonia.Input.InputElement editableControl in EditableControls)
			{
				editableControl.Disable();
			}
			UpdateSubmitButton();
		}

		public void EnableEditableControls()
		{
			foreach (global::Avalonia.Input.InputElement editableControl in EditableControls)
			{
				editableControl.Enable();
			}
			UpdateSubmitButton();
		}

		private void ForkPlusDialogWindow_Loaded(object sender, RoutedEventArgs e)
		{
			if (IsDesignMode)
			{
				return;
			}
			if (ApplyAutomaticLocalization)
			{
				PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			}
			(base.Content as Grid)?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
		}

		private void ForkPlusDialogWindow_Initialized(object sender, EventArgs e)
		{
			InitializeDialogChrome();
		}

		// Migration note：WPF ContentControl.OnContentChanged override 在 Avalonia 无对应虚方法，
		// 由构造函数订阅 PropertyChanged（ContentProperty）转发，保持子类重写形态。
		protected void OnContentChanged(object oldContent, object newContent)
		{
			if (!IsInitialized)
			{
				return;
			}
			// Migration note：WPF 中 Content 在 BAML 解析末尾（x:Name 字段已赋值后）才设置；
			// Avalonia XamlIl 在 populate 中途即设 Content，此时子类 x:Name 字段（如
			// ConfigureGitInstanceWindow.GitPathTextBox）尚未赋值——直接初始化 chrome 会让
			// IsSubmitAllowed 虚属性 NRE。推迟到下一帧（populate/构造函数完成后）再执行，
			// _dialogChromeInitialized 幂等 + _pending* 字段机制保证语义与 WPF 一致。
			global::Avalonia.Threading.Dispatcher.UIThread.Post(delegate
			{
				InitializeDialogChrome();
			});
		}

		private void InitializeDialogChrome()
		{
			if (_dialogChromeInitialized)
			{
				return;
			}
			Grid obj = base.Content as Grid;
			if (obj == null)
			{
				return;
			}
			_dialogChromeInitialized = true;
			RefreshWindowSize();
			obj.Margin = new Thickness(20.0, 0.0, 20.0, 20.0);
			obj.Background = global::ForkPlus.UI.Theme.ForkPlusDialogBackgroundBrush;
			RenderOptionsShim.SetClearTypeHint(obj, ClearTypeHint.Enabled);
			if (ShowHeader)
			{
				AddDialogHeader();
			}
			if (ShowLogo)
			{
				AddForkPlusLogo();
			}
			if (ShowFooter)
			{
				AddCommandPreview();
				AddFooter();
				UpdateSubmitButton();
			}
		}

		private void RefreshWindowSize()
		{
			double num = (double)ForkPlusSettings.Default.LayoutScaling * 0.01;
			base.Height *= num;
			base.Width *= num;
		}

		private void AddDialogHeader()
		{
			Grid obj = base.Content as Grid;
			if (obj == null)
			{
				return;
			}
			string dialogDescription = _pendingDialogDescription ?? string.Empty;
			TextBlock textBlock = new TextBlock
			{
				FontWeight = FontWeights.Medium,
				FontSize = 15.0,
				Text = (string.IsNullOrWhiteSpace(base.Title) ? "[Dialog Title]" : base.Title)
			};
			TextBlock textBlock2 = new TextBlock
			{
				TextWrapping = TextWrapping.Wrap,
				FontSize = 13.0,
				Margin = new Thickness(0.0, 2.0, 0.0, 0.0),
				Foreground = (Application.Current.TryFindResource("ForkPlusDialogDescriptionForeground") as Brush),
				Text = dialogDescription,
				IsVisible = !string.IsNullOrWhiteSpace(dialogDescription)
			};
			StackPanel stackPanel = new StackPanel();
			stackPanel.SetValue(Grid.RowProperty, 0);
			stackPanel.SetValue(Grid.ColumnProperty, 1);
			stackPanel.Children.Add(textBlock);
			stackPanel.Children.Add(textBlock2);
			obj.Children.Add(stackPanel);
			TitleTextBlock = textBlock;
		DescriptionTextBlock = textBlock2;
		_pendingTitleCustomization?.Invoke(textBlock);
		_pendingTitleCustomization = null;
		_pendingDescriptionCustomization?.Invoke(textBlock2);
		_pendingDescriptionCustomization = null;
			if (_pendingDialogTitle != null)
			{
				DialogTitle = _pendingDialogTitle;
			}
			if (_pendingDialogDescription != null)
			{
				DialogDescription = _pendingDialogDescription;
			}
		}

		/// <summary>
	/// 子类重写以提供命令预览文本。返回 null 或空字符串则不显示预览区域。
	/// </summary>
	protected virtual string GetCommandPreview()
	{
		return null;
	}

	/// <summary>
	/// 刷新命令预览区域。子类在控件事件（TextChanged/SelectionChanged/Checked 等）中调用。
	/// </summary>
	protected void RefreshCommandPreview()
	{
		if (!_commandPreviewInitialized || _commandPreviewTextBlock == null)
		{
			return;
		}
		string text = GetCommandPreview();
		if (string.IsNullOrWhiteSpace(text))
		{
			_commandPreviewLabel.IsVisible = false;
			_commandPreviewTextBlock.IsVisible = false;
			_commandPreviewTextBlock.Text = "";
			// 鼠标悬停显示完整命令文本（预览区可能因 MaxHeight 截断）
			global::Avalonia.Controls.ToolTip.SetTip(_commandPreviewTextBlock,null);
			if (_commandPreviewScrollViewer != null)
			{
				_commandPreviewScrollViewer.IsVisible = false;
			}
			if (_commandPreviewCopyButton != null)
			{
				_commandPreviewCopyButton.IsVisible = false;
			}
		}
		else
		{
			_commandPreviewLabel.IsVisible = true;
			_commandPreviewTextBlock.IsVisible = true;
			_commandPreviewTextBlock.Text = text;
			// 鼠标悬停显示完整命令文本（预览区可能因 MaxHeight 截断）
			global::Avalonia.Controls.ToolTip.SetTip(_commandPreviewTextBlock,text);
			if (_commandPreviewScrollViewer != null)
			{
				_commandPreviewScrollViewer.IsVisible = true;
			}
			if (_commandPreviewCopyButton != null)
			{
				_commandPreviewCopyButton.IsVisible = true;
			}
		}
	}

	private void AddCommandPreview()
	{
		if (_commandPreviewInitialized)
		{
			return;
		}
		Grid grid = base.Content as Grid;
		if (grid == null)
		{
			return;
		}
		_commandPreviewInitialized = true;
		// 在 footer 行之前插入新行用于命令预览
		int previewRow = grid.RowDefinitions.Count;
		RowDefinition rowDefinition = new RowDefinition
		{
			Height = GridLength.Auto
		};
		grid.RowDefinitions.Add(rowDefinition);
		// 命令预览放在内容列（Column 1），与上方内容区使用一致的两列布局
		// （Auto 标签列 + * 输入列），使预览标签和文本与对话框内容对齐。
		// 此前 label 放在 Column 0（80px logo 列）导致与内容标签错位。
		Grid previewGrid = new Grid
		{
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
		};
		previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		previewGrid.SetValue(Grid.RowProperty, previewRow);
		previewGrid.SetValue(Grid.ColumnProperty, 1);
		_commandPreviewLabel = new TextBlock
		{
			Text = PreferencesLocalization.Current("Git Command Preview"),
			FontSize = 13.0,
			FontWeight = FontWeights.Medium,
			VerticalAlignment = VerticalAlignment.Top,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 4.0, 8.0, 0.0),
			IsVisible = false
		};
		_commandPreviewLabel.SetValue(Grid.ColumnProperty, 0);
		previewGrid.Children.Add(_commandPreviewLabel);
		_commandPreviewTextBlock = new TextBlock
		{
			FontFamily = new FontFamily("Consolas"),
			FontSize = 12.0,
			TextWrapping = TextWrapping.Wrap,
			Foreground = (Application.Current.TryFindResource("SecondaryLabelBrush") as Brush),
			Margin = new Thickness(8.0, 4.0, 0.0, 0.0),
			IsVisible = false
		};
		// 限制命令预览最大高度：长命令换行多时不再无限撑高窗口把确认按钮挤出可视区。
		// 超出部分在 ScrollViewer 内滚动查看；同时悬停 ToolTip 显示完整命令文本。
		ScrollViewer previewScrollViewer = new ScrollViewer
		{
			VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
			MaxHeight = 120.0,
			Margin = new Thickness(0.0, 0.0, 0.0, 0.0),
			IsVisible = false
		};
		previewScrollViewer.SetValue(Grid.ColumnProperty, 1);
		previewScrollViewer.Content = _commandPreviewTextBlock;
		_commandPreviewScrollViewer = previewScrollViewer;
		previewGrid.Children.Add(previewScrollViewer);
		// 复制按钮：点击复制预览命令到剪贴板，ToolTip 国际化
		_commandPreviewCopyButton = global::ForkPlus.UI.WpfCompat.ToolTipCompat.WithTip(new Button
		{
			VerticalAlignment = VerticalAlignment.Top,			HorizontalAlignment = HorizontalAlignment.Left,			Margin = new Thickness(4.0, 2.0, 0.0, 0.0),			Padding = new Thickness(2.0),			Background = Brushes.Transparent,			BorderThickness = new Thickness(0.0),			Cursor = Cursors.Hand,			IsVisible = false
		},PreferencesLocalization.Current("Copy to clipboard"));
		_commandPreviewCopyButton.SetValue(Grid.ColumnProperty, 2);
		// 用矢量 Path 绘制复制图标（两个重叠的圆角矩形），无需新增图片资源
		_commandPreviewCopyButton.Content = new Image
		{
			Source = new DrawingImage(new GeometryDrawing
			{
				Geometry = Geometry.Parse("M4,2 L12,2 L12,14 L4,14 Z M6,4 L6,12 L10,12 L10,4 Z M2,4 L2,16 L14,16 L14,14 L13,14 L13,15 L3,15 L3,5 L4,5 L4,4 Z"),
				Brush = (Application.Current.TryFindResource("SecondaryLabelBrush") as global::Avalonia.Media.IBrush) ?? (global::Avalonia.Media.IBrush)Brushes.Gray
			}),
			Width = 14.0,
			Height = 14.0
		};
		_commandPreviewCopyButton.Click += delegate
		{
			if (_commandPreviewTextBlock != null && !string.IsNullOrWhiteSpace(_commandPreviewTextBlock.Text))
			{
				ServiceLocator.Clipboard.SetText(_commandPreviewTextBlock.Text);
			}
		};
		previewGrid.Children.Add(_commandPreviewCopyButton);
		grid.Children.Add(previewGrid);
		// 初始刷新
		RefreshCommandPreview();
	}

	private void AddFooter()
		{
			Grid grid = base.Content as Grid;
			if (grid == null)
			{
				return;
			}
			ForkPlusDialogFooter forkDialogFooter = new ForkPlusDialogFooter();
		if (grid.RowDefinitions.Count <= 0)
		{
			grid.RowDefinitions.Add(new RowDefinition());
		}
		// 若最后一行已被命令预览占用（AddCommandPreview 先于 AddFooter 执行），则新增一行放 footer
		int footerRow = grid.RowDefinitions.Count - 1;
		bool lastRowOccupied = false;
		foreach (global::Avalonia.Input.InputElement child in grid.Children)
		{
			int row = (int)child.GetValue(Grid.RowProperty);
			if (row == footerRow)
			{
				lastRowOccupied = true;
				break;
			}
		}
		if (lastRowOccupied)
		{
			grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			footerRow = grid.RowDefinitions.Count - 1;
		}
		forkDialogFooter.SetValue(Grid.RowProperty, footerRow);
			forkDialogFooter.SetValue(Grid.ColumnProperty, 0);
			forkDialogFooter.SetValue(Grid.ColumnSpanProperty, 2);
			grid.Children.Add(forkDialogFooter);
			forkDialogFooter.Cancel += delegate
			{
				OnCancel();
			};
			forkDialogFooter.Submit += delegate
			{
				OnSubmit();
			};
			Footer = forkDialogFooter;
			// Migration note（2026-09-03，"弹窗取消按钮固定是 Cancel 没有国际化"修复）：
			// Footer XAML 的 Cancel 按钮文本硬编码 "Cancel"（WPF 原版同样如此），未显式设置
			// CancelButtonTitle 的弹窗（CloneWindow/CreateBranchWindow/PullWindow 等）在任何语言
			// 下都显示英文 "Cancel"。此处对齐用户预期：弹窗未显式设置时默认翻译。
			// 已显式设置（Close/Later/Exit 等）的不受影响——构造函数赋值先入 pending，
			// 此处判空后回放覆盖（见下方 _pendingCancelButtonTitle 回放）。
			if (_pendingCancelButtonTitle == null)
			{
				_pendingCancelButtonTitle = PreferencesLocalization.Translate("Cancel", ForkPlusSettings.Default.UiLanguage);
			}
			if (_pendingSubmitButtonTitle != null)
			{
				SubmitButtonTitle = _pendingSubmitButtonTitle;
			}
			if (_pendingCancelButtonTitle != null)
			{
				CancelButtonTitle = _pendingCancelButtonTitle;
			}
			if (_pendingShowSubmitButton.HasValue)
			{
				ShowSubmitButton = _pendingShowSubmitButton.Value;
			}
			if (_pendingShowCancelButton.HasValue)
			{
				ShowCancelButton = _pendingShowCancelButton.Value;
			}
			// Migration note：回放构造期缓存的 SetStatus（如 ConfigureSshKeysWindow ctor 里的 Refresh）。
			if (_pendingStatus.HasValue)
			{
				ForkPlusDialogStatus pendingStatus = _pendingStatus.Value;
				string pendingMessage = _pendingStatusMessage;
				_pendingStatus = null;
				_pendingStatusMessage = null;
				SetStatus(pendingStatus, pendingMessage);
			}
		}

		private void AddForkPlusLogo()
		{
			Grid obj = base.Content as Grid;
			if (obj == null)
			{
				return;
			}
			if (_logoImage != null)
			{
				return;
			}
			_logoImage = new Image
			{
				Source = new global::Avalonia.Media.Imaging.Bitmap(global::Avalonia.Platform.AssetLoader.Open(ForkPlusLogo)),
				Width = 64.0,
				Height = 64.0,
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Top,
				// 作为“底图”放在最底层：警告/错误叠加标记需要压在其上面显示。
				ZIndex = 0
			};
			_logoImage.SetValue(Grid.RowSpanProperty, 2);
			obj.Children.Add(_logoImage);
		}

		private void AddWarningIcon()
		{
			if (_warningIcon == null)
			{
				Grid obj = base.Content as Grid;
				if (obj == null)
				{
					return;
				}
				_warningIcon = new Image
				{
					Source = new global::Avalonia.Media.Imaging.Bitmap(global::Avalonia.Platform.AssetLoader.Open(WarningIcon)),
					Width = 24.0,
					Height = 24.0,
					HorizontalAlignment = HorizontalAlignment.Left,
					VerticalAlignment = VerticalAlignment.Top,
					Margin = new Thickness(38.0, 38.0, 0.0, 0.0),
					// 必须压在 ForkPlusLogo 之上（用户反馈：黄色感叹号被图标遮挡）。
					ZIndex = 10
				};
				_warningIcon.SetValue(Grid.RowSpanProperty, 2);
				obj.Children.Add(_warningIcon);
			}
		}

		private void RemoveWarningIcon()
		{
			if (_warningIcon != null)
			{
				(base.Content as Grid)?.Children.Remove(_warningIcon);
				_warningIcon = null;
			}
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (ShowFooter && ShowCancelButton && e.Key == Key.Escape)
			{
				OnCancel();
				e.Handled = true;
			}
			else
			{
				base.OnKeyDown(e);
			}
		}

		protected virtual void OnCancel()
		{
			if (base.IsVisible)
			{
				if (IsWindowModal)
				{
					Close(false);
				}
				else
				{
					Close();
				}
			}
		}

		protected void Close(GitCommandResult gitResult)
		{
			GitResult = gitResult;
			CloseWithOk();
		}

		protected virtual void OnSubmit()
		{
			CloseWithOk();
		}

		protected void CloseWithOk()
		{
			if (base.IsVisible)
			{
				if (IsWindowModal)
				{
					Close(true);
				}
				else
				{
					Close();
				}
			}
		}

		protected void UpdateSubmitButton()
		{
			if (Footer?.SubmitButton != null)
			{
				Footer.SubmitButton.IsEnabled = IsSubmitAllowed;
			}
		}

		private static IEnumerable<T> FindVisualChildren<T>(global::Avalonia.AvaloniaObject depObj) where T : global::Avalonia.AvaloniaObject
		{
			if (depObj == null)
			{
				yield break;
			}
			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
			{
				global::Avalonia.AvaloniaObject child = VisualTreeHelper.GetChild(depObj, i);
				if (child is T typedChild)
				{
					yield return typedChild;
				}
				foreach (T childOfChild in FindVisualChildren<T>(child))
				{
					yield return childOfChild;
				}
			}
		}

		private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
		{
			RefreshBrushes();
			InvalidateVisual();
		}

		private void RefreshBrushes()
		{
			Grid obj = base.Content as Grid;
			if (obj != null)
			{
				obj.Background = global::ForkPlus.UI.Theme.ForkPlusDialogBackgroundBrush;
			}
		}
	}
}

