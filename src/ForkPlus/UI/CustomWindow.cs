using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Shell;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI
{
	[ContentProperty("Content")]
	[TemplatePart(Name = "PART_MinimizeButton", Type = typeof(Button))]
	[TemplatePart(Name = "PART_MaximizeButton", Type = typeof(Button))]
	[TemplatePart(Name = "PART_RestoreButton", Type = typeof(Button))]
	[TemplatePart(Name = "PART_CloseButton", Type = typeof(Button))]
	[TemplatePart(Name = "PART_WindowHeader", Type = typeof(FrameworkElement))]
	public class CustomWindow : Window
	{
		protected const string PartNameWindowHeader = "PART_WindowHeader";

		protected const string PartNameCloseButton = "PART_CloseButton";

		protected const string PartNameRestoreButton = "PART_RestoreButton";

		protected const string PartNameMinimizeButton = "PART_MinimizeButton";

		protected const string PartNameMaximizeButton = "PART_MaximizeButton";

		public static readonly DependencyProperty HeaderHeightProperty;

		public static readonly DependencyProperty ShowHeaderProperty;

		public static readonly DependencyProperty HideMinimizeMaximizeButtonsProperty;

		public static readonly DependencyProperty IsTitleVisibleProperty;

		public static readonly DependencyProperty WindowResizeBorderThicknessProperty;

		private FrameworkElement _templatePartWindowHeader;

		private Button _closeButton;

		private Button _minimizeButton;

		private Button _maximizeButton;

		private Button _restoreButton;

		private Thickness _tempWindowResizeBorderThickness;

		private Thickness _tempBorderThickness;

		// Phase 0.4：Core 引入了 ForkPlus.UI.WindowState 枚举。CustomWindow 继承自
		// System.Windows.Window，bare "WindowState" 在实例方法中会被 C# 简单名查找规则
		// 解析为继承的实例属性 this.WindowState（而非枚举类型），导致访问 .Maximized 等
		// 静态成员时触发 CS0176。所以这里用完全限定名 System.Windows.WindowState。
		private System.Windows.WindowState _tempWindowState;

		private bool _showHeader = true;

		private bool IsDesignMode => global::ForkPlus.DesignTimeHelper.IsInDesignMode();

		public double HeaderHeight
		{
			get
			{
				return (double)GetValue(HeaderHeightProperty);
			}
			private set
			{
				SetValue(HeaderHeightProperty, value);
			}
		}

		public bool ShowHeader
		{
			get
			{
				return (bool)GetValue(ShowHeaderProperty);
			}
			set
			{
				SetValue(ShowHeaderProperty, value);
			}
		}

		public bool HideMinimizeMaximizeButtons
		{
			get
			{
				return (bool)GetValue(HideMinimizeMaximizeButtonsProperty);
			}
			set
			{
				SetValue(HideMinimizeMaximizeButtonsProperty, value);
			}
		}

		public bool IsTitleVisible
		{
			get
			{
				return (bool)GetValue(IsTitleVisibleProperty);
			}
			set
			{
				SetValue(IsTitleVisibleProperty, value);
			}
		}

		public Thickness WindowResizeBorderThickness
		{
			get
			{
				return (Thickness)GetValue(WindowResizeBorderThicknessProperty);
			}
			private set
			{
				SetValue(WindowResizeBorderThicknessProperty, value);
			}
		}

		private static Thickness MaximizedWindowResizeBorderThickness
		{
			get
			{
				Thickness windowResizeBorderThickness = WindowLocationStateExtensions.WindowResizeBorderThickness;
				if (WindowLocationStateExtensions.AutoHideEnabled())
				{
					return new Thickness(0.0 - windowResizeBorderThickness.Left, 0.0 - windowResizeBorderThickness.Top, 0.0 - windowResizeBorderThickness.Right, 0.0 - windowResizeBorderThickness.Bottom);
				}
				return windowResizeBorderThickness;
			}
		}

		static CustomWindow()
		{
			HeaderHeightProperty = DependencyProperty.Register("HeaderHeight", typeof(double), typeof(CustomWindow), new PropertyMetadata(22.0));
			ShowHeaderProperty = DependencyProperty.Register("ShowHeader", typeof(bool), typeof(CustomWindow), new PropertyMetadata(true, OnShowHeaderChanged));
			HideMinimizeMaximizeButtonsProperty = DependencyProperty.Register("HideMinimizeMaximizeButtons", typeof(bool), typeof(CustomWindow), new PropertyMetadata(false));
			IsTitleVisibleProperty = DependencyProperty.Register("IsTitleVisible", typeof(bool), typeof(CustomWindow), new PropertyMetadata(false));
			WindowResizeBorderThicknessProperty = DependencyProperty.Register("WindowResizeBorderThickness", typeof(Thickness), typeof(CustomWindow), new PropertyMetadata(default(Thickness)));
			FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(CustomWindow), new FrameworkPropertyMetadata(typeof(CustomWindow)));
		}

		public CustomWindow()
		{
			SetResourceReference(FrameworkElement.StyleProperty, typeof(CustomWindow));
			if (IsDesignMode)
			{
				_tempWindowResizeBorderThickness = new Thickness(6.0);
				WindowResizeBorderThickness = _tempWindowResizeBorderThickness;
				return;
			}
			WindowChrome windowChrome = new WindowChrome
			{
				CornerRadius = default(CornerRadius),
				GlassFrameThickness = new Thickness(0.0, 0.0, 0.0, 1.0),
				UseAeroCaptionButtons = false
			};
			Binding binding = new Binding("HeaderHeight")
			{
				Source = this
			};
			BindingOperations.SetBinding(windowChrome, WindowChrome.CaptionHeightProperty, binding);
			WindowChrome.SetWindowChrome(this, windowChrome);
			_tempWindowResizeBorderThickness = WindowResizeBorderThickness;
			base.Loaded += Window_Loaded;
		}

		protected override void OnContentRendered(EventArgs e)
		{
			base.OnContentRendered(e);
			if (base.SizeToContent == SizeToContent.WidthAndHeight)
			{
				InvalidateMeasure();
			}
		}

		protected override void OnStateChanged(EventArgs e)
		{
			base.OnStateChanged(e);
			if (IsDesignMode)
			{
				return;
			}
			AdjustButtonsVisibilityToWindowState();
			if (base.WindowState == System.Windows.WindowState.Maximized)
			{
				WindowResizeBorderThickness = MaximizedWindowResizeBorderThickness;
				base.BorderThickness = default(Thickness);
			}
			else
			{
				WindowResizeBorderThickness = _tempWindowResizeBorderThickness;
				base.BorderThickness = _tempBorderThickness;
			}
		}

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();
			_templatePartWindowHeader = GetTemplateChild("PART_WindowHeader") as FrameworkElement;
			_closeButton = GetTemplateChild("PART_CloseButton") as Button;
			_minimizeButton = GetTemplateChild("PART_MinimizeButton") as Button;
			_maximizeButton = GetTemplateChild("PART_MaximizeButton") as Button;
			_restoreButton = GetTemplateChild("PART_RestoreButton") as Button;
			AdjustButtonsVisibilityToWindowState();
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			if (IsDesignMode)
			{
				return;
			}
			HwndSource.FromHwnd(new WindowInteropHelper(this).EnsureHandle()).AddHook(HwndSourceHook);
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			if (IsDesignMode)
			{
				return;
			}
			base.CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, delegate
			{
				base.WindowState = System.Windows.WindowState.Minimized;
			}));
			base.CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, delegate
			{
				base.WindowState = System.Windows.WindowState.Maximized;
			}));
			base.CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, delegate
			{
				base.WindowState = System.Windows.WindowState.Normal;
			}));
			base.CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, delegate
			{
				Close();
			}));
			_tempWindowState = base.WindowState;
			_tempBorderThickness = base.BorderThickness;
			if (base.WindowState == System.Windows.WindowState.Maximized)
			{
				base.BorderThickness = default(Thickness);
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
			switch (base.WindowState)
			{
			case System.Windows.WindowState.Normal:
				_maximizeButton?.Show();
				_restoreButton?.Collapse();
				break;
			case System.Windows.WindowState.Maximized:
				_maximizeButton?.Collapse();
				_restoreButton?.Show();
				break;
			}
			switch (base.ResizeMode)
			{
			case ResizeMode.NoResize:
				_minimizeButton?.Collapse();
				_maximizeButton?.Collapse();
				_restoreButton?.Collapse();
				break;
			case ResizeMode.CanMinimize:
				_maximizeButton?.Collapse();
				_restoreButton?.Collapse();
				break;
			}
		}

		private IntPtr HwndSourceHook(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
		{
			switch (msg)
			{
			case 71:
				WindowResizeBorderThickness = ((base.WindowState == System.Windows.WindowState.Maximized) ? MaximizedWindowResizeBorderThickness : _tempWindowResizeBorderThickness);
				break;
			case 36:
				WindowLocationStateExtensions.GetMinMaxInfo(hwnd, lparam);
				WindowResizeBorderThickness = ((base.WindowState == System.Windows.WindowState.Maximized) ? MaximizedWindowResizeBorderThickness : _tempWindowResizeBorderThickness);
				handled = true;
				break;
			case 132:
				try
				{
					lparam.ToInt32();
				}
				catch (OverflowException)
				{
					handled = true;
				}
				break;
			}
			return IntPtr.Zero;
		}

		private static void OnShowHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			((CustomWindow)d).SwitchShowHeader((bool)e.NewValue);
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
