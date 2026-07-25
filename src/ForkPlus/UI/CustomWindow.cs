using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using ForkPlus.UI.Helpers;
using Avalonia.Controls.Metadata;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.Styling;

namespace ForkPlus.UI
{
	/// <summary>
	/// 阶段 4 里程碑 4.2：CustomWindow WPF→Avalonia 迁移。
	/// WPF WindowChrome/HwndSource/Win32 消息钩子 → Avalonia ExtendClientAreaToDecorationsHint。
	/// DependencyProperty → StyledProperty&lt;T&gt;。OnSourceInitialized → 移除（Win32 专属）。
	/// </summary>
	// 阶段 5：WPF [ContentProperty("Content")] 移除——Avalonia 的 ContentAttribute 只能标注
	// 属性（不能标注类），且基类 Window/ContentControl 已在框架内将 Content 标为内容属性，
	// 子类无需重复声明。
	[TemplatePart(Name = "PART_MinimizeButton", Type = typeof(Button))]
	[TemplatePart(Name = "PART_MaximizeButton", Type = typeof(Button))]
	[TemplatePart(Name = "PART_RestoreButton", Type = typeof(Button))]
	[TemplatePart(Name = "PART_CloseButton", Type = typeof(Button))]
	[TemplatePart(Name = "PART_WindowHeader", Type = typeof(Control))]
	public class CustomWindow : Window
	{
		protected const string PartNameWindowHeader = "PART_WindowHeader";

		protected const string PartNameCloseButton = "PART_CloseButton";

		protected const string PartNameRestoreButton = "PART_RestoreButton";

		protected const string PartNameMinimizeButton = "PART_MinimizeButton";

		protected const string PartNameMaximizeButton = "PART_MaximizeButton";

		public static readonly StyledProperty<double> HeaderHeightProperty =
			AvaloniaProperty.Register<CustomWindow, double>(nameof(HeaderHeight), 22.0);

		public static readonly StyledProperty<bool> ShowHeaderProperty =
			AvaloniaProperty.Register<CustomWindow, bool>(nameof(ShowHeader), true);

		public static readonly StyledProperty<bool> HideMinimizeMaximizeButtonsProperty =
			AvaloniaProperty.Register<CustomWindow, bool>(nameof(HideMinimizeMaximizeButtons), false);

		public static readonly StyledProperty<bool> IsTitleVisibleProperty =
			AvaloniaProperty.Register<CustomWindow, bool>(nameof(IsTitleVisible), false);

		private Control _templatePartWindowHeader;

		private Button _closeButton;

		private Button _minimizeButton;

		private Button _maximizeButton;

		private Button _restoreButton;

		private bool _showHeader = true;

		// 阶段 5：DesignTimeHelper 提供 IsInDesignMode()（非 IsDesignMode()）。
	private bool IsDesignMode => global::ForkPlus.DesignTimeHelper.IsInDesignMode();

		public double HeaderHeight
		{
			get => GetValue(HeaderHeightProperty);
			private set => SetValue(HeaderHeightProperty, value);
		}

		public bool ShowHeader
		{
			get => GetValue(ShowHeaderProperty);
			set => SetValue(ShowHeaderProperty, value);
		}

		public bool HideMinimizeMaximizeButtons
		{
			get => GetValue(HideMinimizeMaximizeButtonsProperty);
			set => SetValue(HideMinimizeMaximizeButtonsProperty, value);
		}

		public bool IsTitleVisible
		{
			get => GetValue(IsTitleVisibleProperty);
			set => SetValue(IsTitleVisibleProperty, value);
		}

		/// <summary>
		/// WPF ResizeMode 兼容属性。Avalonia Window 仅有 bool CanResize，
		/// 此 setter 将 NoResize 映射为 CanResize=false，其他值映射为 CanResize=true。
		/// </summary>
		public ResizeMode ResizeMode
		{
			get
			{
				return CanResize ? ResizeMode.CanResize : ResizeMode.NoResize;
			}
			set
			{
				CanResize = (value != ResizeMode.NoResize);
			}
		}

		static CustomWindow()
		{
			// Avalonia: 通过 StyleKey 让控件查找对应 ControlTheme
			// （等价 WPF DefaultStyleKey OverrideMetadata）
		}

		public CustomWindow()
		{
			if (!IsDesignMode)
			{
				// Avalonia 自定义标题栏：扩展客户区到整个窗口
				ExtendClientAreaToDecorationsHint = true;
				// 阶段 5：ExtendClientAreaChromeHints 在 Avalonia.Platform 命名空间（非 Avalonia.Controls）。
				ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;
				ExtendClientAreaTitleBarHeightHint = -1;
			}
			base.Loaded += Window_Loaded;
			// 阶段 5：Avalonia 11 Window 无 WindowStateChanged 公开事件（仅 IWindowImpl 平台层回调）。
			// 改用 GetObservable 订阅 WindowState 属性变更（等价 WPF StateChanged）。
			this.GetObservable(WindowStateProperty).Subscribe(_ => Window_WindowStateChanged(this, EventArgs.Empty));
		}

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			_templatePartWindowHeader = e.NameScope.Get<Control>("PART_WindowHeader");
			_closeButton = e.NameScope.Get<Button>("PART_CloseButton");
			_minimizeButton = e.NameScope.Get<Button>("PART_MinimizeButton");
			_maximizeButton = e.NameScope.Get<Button>("PART_MaximizeButton");
			_restoreButton = e.NameScope.Get<Button>("PART_RestoreButton");

			// 绑定标题栏按钮点击
			if (_closeButton != null)
			{
				_closeButton.Click += (s, ev) => Close();
			}
			if (_minimizeButton != null)
			{
				_minimizeButton.Click += (s, ev) => WindowState = WindowState.Minimized;
			}
			if (_maximizeButton != null)
			{
				_maximizeButton.Click += (s, ev) => WindowState = WindowState.Maximized;
			}
			if (_restoreButton != null)
			{
				_restoreButton.Click += (s, ev) => WindowState = WindowState.Normal;
			}

			AdjustButtonsVisibilityToWindowState();
		}

		private void Window_WindowStateChanged(object sender, EventArgs e)
		{
			if (IsDesignMode)
			{
				return;
			}
			AdjustButtonsVisibilityToWindowState();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			if (IsDesignMode)
			{
				return;
			}
			SwitchShowHeader(_showHeader);
		}

		private void AdjustButtonsVisibilityToWindowState()
		{
			if (_minimizeButton == null && _maximizeButton == null && _restoreButton == null)
			{
				return;
			}
			if (HideMinimizeMaximizeButtons)
			{
				_minimizeButton?.Collapse();
				_maximizeButton?.Collapse();
				_restoreButton?.Collapse();
				return;
			}
			switch (WindowState)
			{
			case WindowState.Normal:
				_maximizeButton?.Show();
				_restoreButton?.Collapse();
				break;
			case WindowState.Maximized:
				_maximizeButton?.Collapse();
				_restoreButton?.Show();
				break;
			}
			if (!CanResize)
			{
				_minimizeButton?.Collapse();
				_maximizeButton?.Collapse();
				_restoreButton?.Collapse();
			}
		}

		/// <summary>
		/// ShowHeader 属性变更回调（Avalonia 没有 OnPropertyChanged 虚方法重写 StyledProperty，
		/// 通过 override OnPropertyChanged 统一处理）。
		/// </summary>
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == ShowHeaderProperty)
			{
				SwitchShowHeader(change.NewValue is bool b && b);
			}
		}

		private void SwitchShowHeader(bool showHeader)
		{
			if (_templatePartWindowHeader == null)
			{
				_showHeader = showHeader;
				return;
			}
			if (showHeader)
			{
				_templatePartWindowHeader.Show();
				return;
			}
			_templatePartWindowHeader.Collapse();
			HeaderHeight = 0.0;
		}
	}
}
