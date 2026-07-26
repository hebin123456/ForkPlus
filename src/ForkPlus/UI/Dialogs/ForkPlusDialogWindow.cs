using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using ForkPlus.Git.Commands;
using ForkPlus.Services;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.UserControls;
using Theme = ForkPlus.UI.Theme;

namespace ForkPlus.UI.Dialogs
{
	public class ForkPlusDialogWindow : CustomWindow
	{
		// 阶段 6 修复：assets/ 目录下文件名全小写（forkplusicon.png），Avalonia 资源加载器大小写敏感，
		// 原 ForkPlusIcon.png 会在 AddForkPlusLogo → AssetLoader.Open 抛 FileNotFoundException，
		// 导致 ConfigureGitInstanceWindow 等 ShowLogo=true 的对话框 Loaded 即崩溃（"选 git 路径后闪退"根因）。
		private static readonly Uri ForkPlusLogo = new Uri("avares://ForkPlus/assets/forkplusicon.png");

		public static readonly Uri WarningIcon = new Uri("avares://ForkPlus/assets/warning.png");

		public static readonly Uri ErrorIcon = new Uri("avares://ForkPlus/assets/error.png");

		public static readonly Uri SuccessIcon = new Uri("avares://ForkPlus/assets/checkmarkstroked.png");

		private Image _warningIcon;

		private bool _showWarningIcon;

		private bool _dialogChromeInitialized;

		private string _pendingDialogTitle;

		private string _pendingDialogDescription;

		private string _pendingSubmitButtonTitle;

		private string _pendingCancelButtonTitle;

		private bool? _pendingShowSubmitButton;

		private bool? _pendingShowCancelButton;

		private TextBlock _commandPreviewLabel;

	private TextBlock _commandPreviewTextBlock;

	// 预览文本外层 ScrollViewer：限制 MaxHeight 防止长命令撑高窗口挤掉确认按钮
	private ScrollViewer _commandPreviewScrollViewer;

	private Button _commandPreviewCopyButton;

	private bool _commandPreviewInitialized;

	// 阶段 6 修复 NRE：子类构造时 TitleTextBlock/DescriptionTextBlock 尚未创建（在 Loaded →
	// AddDialogHeader 才赋值），直接访问 base.TitleTextBlock.XXX 会抛 NullReferenceException。
	// 这些 pending 字段缓存子类构造器中对 TextBlock 样式的设置，由 AddDialogHeader 在创建
	// TextBlock 后统一应用。同样适用于已初始化后再调用的情况（直接套用）。
	// 注意：Avalonia 中 TextTrimming 是 class（引用类型），用 null 表示未设置；
	// TextWrapping 是 enum（值类型），用 Nullable<TextWrapping> 表示未设置。
	private TextTrimming _pendingTitleTextTrimming;
	private TextWrapping? _pendingTitleTextWrapping;
	private double? _pendingTitleMaxHeight;
	private double? _pendingTitleFontSize;
	private IBrush _pendingTitleForeground;
	private TextTrimming _pendingDescriptionTextTrimming;
	private TextWrapping? _pendingDescriptionTextWrapping;
	private double? _pendingDescriptionMaxHeight;
	private Action<TextBlock> _pendingDescriptionConfigure;

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

	/// <summary>
	/// 阶段 6 修复 NRE：配置标题 TextBlock 样式。子类构造器中调用，若 TitleTextBlock 已创建则
	/// 立即套用，否则缓存为 pending，由 AddDialogHeader 在创建 TextBlock 后统一应用。
	/// </summary>
	protected void ConfigureTitleTextBlock(TextTrimming textTrimming = null, TextWrapping? textWrapping = null, double? maxHeight = null, double? fontSize = null, IBrush foreground = null)
	{
		if (textTrimming != null) _pendingTitleTextTrimming = textTrimming;
		if (textWrapping.HasValue) _pendingTitleTextWrapping = textWrapping;
		if (maxHeight.HasValue) _pendingTitleMaxHeight = maxHeight;
		if (fontSize.HasValue) _pendingTitleFontSize = fontSize;
		if (foreground != null) _pendingTitleForeground = foreground;
		if (TitleTextBlock != null) ApplyPendingTitleStyles(TitleTextBlock);
	}

	/// <summary>
	/// 阶段 6 修复 NRE：配置描述 TextBlock 样式。子类构造器中调用，若 DescriptionTextBlock 已创建则
	/// 立即套用，否则缓存为 pending，由 AddDialogHeader 在创建 TextBlock 后统一应用。
	/// </summary>
	protected void ConfigureDescriptionTextBlock(TextTrimming textTrimming = null, TextWrapping? textWrapping = null, double? maxHeight = null)
	{
		if (textTrimming != null) _pendingDescriptionTextTrimming = textTrimming;
		if (textWrapping.HasValue) _pendingDescriptionTextWrapping = textWrapping;
		if (maxHeight.HasValue) _pendingDescriptionMaxHeight = maxHeight;
		if (DescriptionTextBlock != null) ApplyPendingDescriptionStyles(DescriptionTextBlock);
	}

	/// <summary>
	/// 阶段 6 修复 NRE：注册 DescriptionTextBlock 的自定义配置回调（用于 Inlines 等无法用简单属性
	/// 表达的复杂设置）。回调在 AddDialogHeader 创建 DescriptionTextBlock 后调用。
	/// </summary>
	protected void ConfigureDescriptionTextBlock(Action<TextBlock> configure)
	{
		_pendingDescriptionConfigure = configure;
		if (DescriptionTextBlock != null) configure(DescriptionTextBlock);
	}

	private void ApplyPendingTitleStyles(TextBlock textBlock)
	{
		if (textBlock == null) return;
		if (_pendingTitleTextTrimming != null) textBlock.TextTrimming = _pendingTitleTextTrimming;
		if (_pendingTitleTextWrapping.HasValue) textBlock.TextWrapping = _pendingTitleTextWrapping.Value;
		if (_pendingTitleMaxHeight.HasValue) textBlock.MaxHeight = _pendingTitleMaxHeight.Value;
		if (_pendingTitleFontSize.HasValue) textBlock.FontSize = _pendingTitleFontSize.Value;
		if (_pendingTitleForeground != null) textBlock.Foreground = _pendingTitleForeground;
	}

	private void ApplyPendingDescriptionStyles(TextBlock textBlock)
	{
		if (textBlock == null) return;
		if (_pendingDescriptionTextTrimming != null) textBlock.TextTrimming = _pendingDescriptionTextTrimming;
		if (_pendingDescriptionTextWrapping.HasValue) textBlock.TextWrapping = _pendingDescriptionTextWrapping.Value;
		if (_pendingDescriptionMaxHeight.HasValue) textBlock.MaxHeight = _pendingDescriptionMaxHeight.Value;
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
					DescriptionTextBlock.Text = value;
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
				return Footer.SubmitButton.IsVisible;
			}
			set
			{
				_pendingShowSubmitButton = value;
				if (Footer != null)
				{
					Footer.SubmitButton.IsVisible = !(!value);
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
				return Footer.CancelButton.IsVisible;
			}
			set
			{
				_pendingShowCancelButton = value;
				if (Footer != null)
				{
					Footer.CancelButton.IsVisible = !(!value);
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

		private IEnumerable<Control> EditableControls => FindVisualChildren<Control>(this);

		private bool IsDesignMode => global::ForkPlus.DesignTimeHelper.IsInDesignMode();

		public ForkPlusDialogWindow(bool preventMainWindowRefresh = true)
		{
			if (!IsDesignMode)
			{
				MainWindow instance = MainWindow.Instance;
				if (instance != null)
				{
					// base.Owner = instance;  // 阶段5：Avalonia Window.Owner 只读，已注释
					if (preventMainWindowRefresh)
					{
						instance.PreventRefreshAfterChildDialogClose(GetType().Name);
					}
				}
				base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			}
			base.ShowInTaskbar = false;
			base.CanResize = false;
			base.Loaded += ForkPlusDialogWindow_Loaded;
			if (!IsDesignMode)
			{
				NotificationCenter.Current.ApplicationThemeChanged += ApplicationThemeChanged;
			}
		}

		public void SetStatus(ForkPlusDialogStatus status, string message)
		{
			IsOperationInProgress = status == ForkPlusDialogStatus.InProgress;
			if (status == ForkPlusDialogStatus.None)
			{
				ClearStatus();
				return;
			}
			string localizedMessage = PreferencesLocalization.Translate(message, ForkPlusSettings.Default.UiLanguage);
			Footer.StatusMessageTextBlock.Text = localizedMessage;
			ToolTip.SetTip(Footer.StatusMessageTextBlock, localizedMessage);
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
				Footer.StatusImage.Source = new Bitmap(AssetLoader.Open(SuccessIcon));
				break;
			case ForkPlusDialogStatus.Warning:
				Footer.StatusImage.Source = new Bitmap(AssetLoader.Open(WarningIcon));
				break;
			case ForkPlusDialogStatus.Error:
				Footer.StatusImage.Source = new Bitmap(AssetLoader.Open(ErrorIcon));
				break;
			}
		}

		public void ClearStatus()
		{
			Footer.StatusImage.IsVisible = false;
			Footer.StatusMessageTextBlock.IsVisible = false;
			Footer.BusyIndicator.IsVisible = false;
		}

		public void DisableEditableControls()
		{
			foreach (Control editableControl in EditableControls)
			{
				editableControl.Disable();
			}
			UpdateSubmitButton();
		}

		public void EnableEditableControls()
		{
			foreach (Control editableControl in EditableControls)
			{
				editableControl.Enable();
			}
			UpdateSubmitButton();
		}

		private void ForkPlusDialogWindow_Loaded(object sender, RoutedEventArgs e)
		{
			// 阶段 6 诊断：记录 Loaded 各阶段日志，定位"窗口出现就崩"在 Loaded 中的具体步骤。
			Log.Info($"ForkPlusDialogWindow_Loaded: start (type={GetType().Name})");
			try
			{
				InitializeDialogChrome();
				Log.Info("ForkPlusDialogWindow_Loaded: InitializeDialogChrome done");
				if (IsDesignMode)
				{
					return;
				}
				if (ApplyAutomaticLocalization)
				{
					PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
					Log.Info("ForkPlusDialogWindow_Loaded: ApplyAutomaticLocalization done");
				}
				(base.Content as Grid)?.Focus();
				Log.Info("ForkPlusDialogWindow_Loaded: done");
			}
			catch (Exception ex)
			{
				Log.Error($"ForkPlusDialogWindow_Loaded 抛异常: {ex.GetType().FullName}: {ex.Message}{(ex.StackTrace != null ? Environment.NewLine + ex.StackTrace : "")}");
				throw;
			}
		}

		private void InitializeDialogChrome()
		{
			if (_dialogChromeInitialized)
			{
				return;
			}
			// 阶段 6 诊断"选 git 路径窗口出现就崩"：记录 InitializeDialogChrome 各阶段进度。
			Log.Info($"InitializeDialogChrome: start (type={GetType().Name}, ShowHeader={ShowHeader}, ShowLogo={ShowLogo}, ShowFooter={ShowFooter})");
			Grid obj = base.Content as Grid;
			if (obj == null)
			{
				Log.Error($"InitializeDialogChrome: base.Content is not a Grid (actual={base.Content?.GetType().FullName ?? "<null>"})");
				return;
			}
			_dialogChromeInitialized = true;
			try
			{
				RefreshWindowSize();
				Log.Info("InitializeDialogChrome: RefreshWindowSize done");
				obj.Margin = new Thickness(20.0, 0.0, 20.0, 20.0);
				obj.Background = ForkPlus.UI.Theme.ForkPlusDialogBackgroundBrush;
				if (ShowHeader)
				{
					AddDialogHeader();
					Log.Info("InitializeDialogChrome: AddDialogHeader done");
				}
				if (ShowLogo)
				{
					AddForkPlusLogo();
					Log.Info("InitializeDialogChrome: AddForkPlusLogo done");
				}
				if (ShowFooter)
				{
					AddCommandPreview();
					Log.Info("InitializeDialogChrome: AddCommandPreview done");
					AddFooter();
					Log.Info("InitializeDialogChrome: AddFooter done");
					UpdateSubmitButton();
					Log.Info("InitializeDialogChrome: UpdateSubmitButton done");
				}
				Log.Info("InitializeDialogChrome: done");
			}
			catch (Exception ex)
			{
				Log.Error($"InitializeDialogChrome 抛异常: {ex.GetType().FullName}: {ex.Message}{(ex.StackTrace != null ? Environment.NewLine + ex.StackTrace : "")}");
				throw;
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
			TextBlock textBlock = new TextBlock
			{
				FontWeight = FontWeights.Medium,
				FontSize = 15.0,
				Text = "[Dialog Title]"
			};
			TextBlock textBlock2 = new TextBlock
			{
				TextWrapping = TextWrapping.Wrap,
				FontSize = 13.0,
				Margin = new Thickness(0.0, 2.0, 0.0, 0.0),
				Foreground = ForkPlus.UI.Theme.FindBrush("ForkPlusDialogDescriptionForeground"),
				Text = "[Dialog Description]"
			};
			StackPanel stackPanel = new StackPanel();
			stackPanel.SetValue(Grid.RowProperty, 0);
			stackPanel.SetValue(Grid.ColumnProperty, 1);
			stackPanel.Children.Add(textBlock);
			stackPanel.Children.Add(textBlock2);
			obj.Children.Add(stackPanel);
			TitleTextBlock = textBlock;
			DescriptionTextBlock = textBlock2;
			if (_pendingDialogTitle != null)
			{
				DialogTitle = _pendingDialogTitle;
			}
			if (_pendingDialogDescription != null)
			{
				DialogDescription = _pendingDialogDescription;
			}
			// 阶段 6：应用子类构造器中通过 ConfigureTitleTextBlock/ConfigureDescriptionTextBlock
			// 缓存的 pending 样式（TextTrimming/TextWrapping/MaxHeight/FontSize/Foreground/Inlines）。
			ApplyPendingTitleStyles(TitleTextBlock);
			ApplyPendingDescriptionStyles(DescriptionTextBlock);
			_pendingDescriptionConfigure?.Invoke(DescriptionTextBlock);
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
			ToolTip.SetTip(_commandPreviewTextBlock, null);
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
			ToolTip.SetTip(_commandPreviewTextBlock, text);
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
			VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
			HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
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
			Foreground = ForkPlus.UI.Theme.FindBrush("SecondaryLabelBrush"),
			Margin = new Thickness(8.0, 4.0, 0.0, 0.0),
			IsVisible = false
		};
		// 限制命令预览最大高度：长命令换行多时不再无限撑高窗口把确认按钮挤出可视区。
		// 超出部分在 ScrollViewer 内滚动查看；同时悬停 ToolTip 显示完整命令文本。
		ScrollViewer previewScrollViewer = new ScrollViewer
		{
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
			MaxHeight = 120.0,
			Margin = new Thickness(0.0, 0.0, 0.0, 0.0),
			IsVisible = false
		};
		previewScrollViewer.SetValue(Grid.ColumnProperty, 1);
		previewScrollViewer.Content = _commandPreviewTextBlock;
		_commandPreviewScrollViewer = previewScrollViewer;
		previewGrid.Children.Add(previewScrollViewer);
		// 复制按钮：点击复制预览命令到剪贴板，ToolTip 国际化
		_commandPreviewCopyButton = new Button
		{
			VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
			HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
			Margin = new Thickness(4.0, 2.0, 0.0, 0.0),
			Padding = new Thickness(2.0),
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Cursor = new Cursor(StandardCursorType.Hand),
			IsVisible = false
		};
		ToolTip.SetTip(_commandPreviewCopyButton, PreferencesLocalization.Current("Copy to clipboard"));
		_commandPreviewCopyButton.SetValue(Grid.ColumnProperty, 2);
		// Avalonia 中 DrawingImage/GeometryDrawing API 与 WPF 不同，简化为用 TextBlock 显示 emoji 复制图标
		_commandPreviewCopyButton.Content = new TextBlock { Text = "📋", FontSize = 12 };
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
		foreach (Control child in grid.Children)
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
		}

		private void AddForkPlusLogo()
		{
			Grid obj = base.Content as Grid;
			if (obj == null)
			{
				return;
			}
			Image image = new Image
			{
				Source = new Bitmap(AssetLoader.Open(ForkPlusLogo)),
				Width = 64.0,
				Height = 64.0,
				HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
				VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
			};
			image.SetValue(Grid.RowSpanProperty, 2);
			obj.Children.Add(image);
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
					Source = new Bitmap(AssetLoader.Open(WarningIcon)),
					Width = 24.0,
					Height = 24.0,
					HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
					VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
					Margin = new Thickness(38.0, 38.0, 0.0, 0.0)
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
				Close(false);
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
				Close(true);
			}
		}

		protected void UpdateSubmitButton()
		{
			if (Footer?.SubmitButton != null)
			{
				Footer.SubmitButton.IsEnabled = IsSubmitAllowed;
			}
		}

		private static IEnumerable<T> FindVisualChildren<T>(Control depObj) where T : class
		{
			return depObj?.GetVisualDescendants().OfType<T>() ?? Enumerable.Empty<T>();
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
				obj.Background = ForkPlus.UI.Theme.ForkPlusDialogBackgroundBrush;
			}
		}
	}
}
