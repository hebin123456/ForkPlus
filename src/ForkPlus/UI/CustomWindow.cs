using System;
using ForkPlus.UI.WpfCompat;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Markup;
using ForkPlus.UI.Helpers;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace ForkPlus.UI
{
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "PART_MinimizeButton", Type = typeof(Button))]
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "PART_MaximizeButton", Type = typeof(Button))]
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "PART_RestoreButton", Type = typeof(Button))]
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "PART_CloseButton", Type = typeof(Button))]
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "PART_WindowHeader", Type = typeof(global::Avalonia.Controls.Control))]
	public class CustomWindow : Window
	{
		private const double DragRegionHeightWhenHeaderHidden = 32.0;

		// Migration note（根因修复）：Avalonia 的 implicit ControlTheme 查找用 StyleKey（Window 基类
		// StyleKeyOverride=typeof(Window)），而 Window.axaml 的 ControlTheme key 是 {x:Type ui:CustomWindow}，
		// 二者不匹配导致隐式主题永不应用、模板退化为 ContentControl 默认 FuncControlTemplate
		// （PART_MainMenu 等模板部件全部找不到 → ForkWindow_Loaded NRE）。
		// override StyleKeyOverride 为 CustomWindow 后，CustomWindow 及其所有子类
		// （MainWindow/ReflogWindow...）均能命中 {x:Type ui:CustomWindow} ControlTheme。
		// 显式 Theme="{DynamicResource MainWindowStyle}" 优先级高于隐式主题，BasedOn 链不受影响。
		protected override global::System.Type StyleKeyOverride => typeof(CustomWindow);

		protected const string PartNameWindowHeader = "PART_WindowHeader";

		protected const string PartNameCloseButton = "PART_CloseButton";

		protected const string PartNameRestoreButton = "PART_RestoreButton";

		protected const string PartNameMinimizeButton = "PART_MinimizeButton";

		protected const string PartNameMaximizeButton = "PART_MaximizeButton";

		// Migration note：字段类型从 AvaloniaProperty 收紧为 StyledProperty<T>（XAML 编译器要求 typed property，
		// 否则 TemplateBinding ui:CustomWindow.Xxx 报 "doesn't inherit from AvaloniaProperty<T>"）。
		public static readonly global::Avalonia.StyledProperty<double> HeaderHeightProperty;

		public static readonly global::Avalonia.StyledProperty<bool> ShowHeaderProperty;

		public static readonly global::Avalonia.StyledProperty<bool> HideMinimizeMaximizeButtonsProperty;

		public static readonly global::Avalonia.StyledProperty<bool> IsTitleVisibleProperty;

		public static readonly global::Avalonia.StyledProperty<global::Avalonia.Thickness> WindowResizeBorderThicknessProperty;

		private global::Avalonia.Controls.Control _templatePartWindowHeader;

		private Button _closeButton;

		private Button _minimizeButton;

		private Button _maximizeButton;

		private Button _restoreButton;

		private Thickness _tempWindowResizeBorderThickness;

		private Thickness _tempBorderThickness;

		private global::Avalonia.Controls.WindowState _tempWindowState;

		private bool _showHeader = true;

		// 问题H：标题栏拖拽期间活跃的贴顶吸附跟踪器（null = 未在跟踪）。
		// 原生模态移动循环独占输入且阻塞 Dispatcher，不可能并发第二个拖拽。
		private TitleBarDragSnapTracker _activeDragSnapTracker;

		private bool IsDesignMode => global::ForkPlus.DesignTimeHelper.IsInDesignMode();

		// ===== WPF 兼容成员（迁移期）=====
		// WPF Window.LocationChanged 事件 / OnLocationChanged 虚方法：Avalonia 对应 Window.PositionChanged。
		public event EventHandler LocationChanged;

		private bool _locationChangedHooked;

		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			global::ForkPlus.UI.WpfCompat.Keyboard.InstallGlobalKeyTracking(this);
			if (!_locationChangedHooked)
			{
				_locationChangedHooked = true;
				PositionChanged += delegate
				{
					OnLocationChanged(EventArgs.Empty);
				};
			}

			// 批量修复：弹窗/子窗口默认居中到 owner 所在屏幕（或主窗口所在屏幕）。
			// 许多窗口仍以 WPF 风格调用 ShowDialog()（无 owner/StartupLocation），Avalonia 下会出现不居中。
			TryCenterOnOpened();
		}

		private void TryCenterOnOpened()
		{
			// 主窗口不自动重定位
			if (this is MainWindow)
			{
				return;
			}

			// 只有显式要求居中时才干预位置（保持其它窗口按系统默认/用户拖拽的位置打开）。
			if (WindowStartupLocation != global::Avalonia.Controls.WindowStartupLocation.CenterOwner
				&& WindowStartupLocation != global::Avalonia.Controls.WindowStartupLocation.CenterScreen)
			{
				return;
			}

			try
			{
				Window owner = global::ForkPlus.UI.WpfCompat.WindowOwnerCompat.TryGetOwner(this)
					?? global::ForkPlus.UI.WpfCompat.WpfApp.ActiveWindow(this)
					?? global::ForkPlus.UI.WpfCompat.WpfApp.MainWindow;

				var screen = owner?.Screens?.ScreenFromWindow(owner)
					?? Screens?.ScreenFromWindow(this)
					?? Screens?.Primary;
				if (screen == null)
				{
					return;
				}

				var wa = screen.WorkingArea;
				int wPx = (int)global::System.Math.Max(1, global::System.Math.Round(ClientSize.Width * screen.Scaling));
				int hPx = (int)global::System.Math.Max(1, global::System.Math.Round(ClientSize.Height * screen.Scaling));
				int x, y;
				if (owner != null && owner.WindowState != global::Avalonia.Controls.WindowState.Minimized)
				{
					// 本轮25：相对 owner 窗口矩形居中（真"跟随"），而非仅同屏居中。
					// 显式走原生扩展——项目内 InputCompat 有同名 PointToScreen shim（返回 DIP Point）。
					global::Avalonia.PixelPoint centerPx = global::Avalonia.VisualExtensions.PointToScreen(owner, new global::Avalonia.Point(owner.Width / 2.0, owner.Height / 2.0));
					x = centerPx.X - wPx / 2;
					y = centerPx.Y - hPx / 2;
				}
				else
				{
					x = wa.X + (wa.Width - wPx) / 2;
					y = wa.Y + (wa.Height - hPx) / 2;
				}
				x = (int)global::System.Math.Max(wa.X, global::System.Math.Min(x, wa.X + wa.Width - wPx));
				y = (int)global::System.Math.Max(wa.Y, global::System.Math.Min(y, wa.Y + wa.Height - hPx));
				Position = new global::Avalonia.PixelPoint(x, y);
			}
			catch
			{
				// 定位失败不影响显示
			}
		}

		protected virtual void OnLocationChanged(EventArgs e)
		{
			LocationChanged?.Invoke(this, e);
		}

		// WPF Window.ResizeMode（Avalonia 12 无该属性，映射到 CanResize）。
		public global::ForkPlus.UI.WpfCompat.ResizeMode ResizeMode
		{
			get
			{
				return CanResize ? global::ForkPlus.UI.WpfCompat.ResizeMode.CanResize : global::ForkPlus.UI.WpfCompat.ResizeMode.NoResize;
			}
			set
			{
				CanResize = value != global::ForkPlus.UI.WpfCompat.ResizeMode.NoResize;
			}
		}

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
				return new Thickness(0.0);
			}
		}

		static CustomWindow()
		{
			// Migration note：WPF DependencyProperty.Register + PropertyMetadata → WpfPropertyCompat.Register（Avalonia StyledProperty）。
			HeaderHeightProperty = global::ForkPlus.UI.WpfCompat.WpfPropertyCompat.Register<CustomWindow, double>("HeaderHeight", 22.0);
			ShowHeaderProperty = global::ForkPlus.UI.WpfCompat.WpfPropertyCompat.Register<CustomWindow, bool>("ShowHeader", true, (owner, e) => OnShowHeaderChanged(owner, e));
			HideMinimizeMaximizeButtonsProperty = global::ForkPlus.UI.WpfCompat.WpfPropertyCompat.Register<CustomWindow, bool>("HideMinimizeMaximizeButtons", false);
			IsTitleVisibleProperty = global::ForkPlus.UI.WpfCompat.WpfPropertyCompat.Register<CustomWindow, bool>("IsTitleVisible", false);
			WindowResizeBorderThicknessProperty = global::ForkPlus.UI.WpfCompat.WpfPropertyCompat.Register<CustomWindow, global::Avalonia.Thickness>("WindowResizeBorderThickness", default(global::Avalonia.Thickness));
			// Migration note：WPF DefaultStyleKeyProperty.OverrideMetadata 在 Avalonia 由 ControlTheme 接管，移除。
		}

		public CustomWindow()
		{
			// Migration note（根因）：WPF WindowChrome 借 WM_NCCALCSIZE 自绘标题栏；Avalonia 12 对应
			// ExtendClientAreaToDecorationsHint——客户区延伸进装饰区，系统标题栏不再渲染
			// （Win32 走 DwmExtendFrameIntoClientArea + NCRP_ENABLED，原生边框/阴影/缩放保留）。
			// 修复"皮肤外还有系统原生窗口（含最小化/最大化/关闭）"。
			// TitleBarHeightHint=-1：按 OS 默认标题栏高度扩展（完全覆盖原生标题栏区域）。
			//
			// BorderOnly 会保留 Win32/DWM 的透明 resize frame，最大化时看起来像外面还套一圈。
			// 改为 None 后完全由应用模板绘制窗口外观，缩放由 OnWindowPointerPressedForMoveDrag 自行接管。
			SystemDecorations = WindowDecorations.None;
			ExtendClientAreaToDecorationsHint = false;
			ExtendClientAreaTitleBarHeightHint = 0;
			// Migration note：WPF SetResourceReference(StyleProperty, type) 隐式样式已由 Avalonia ControlTheme 接管，移除调用。;
			if (IsDesignMode)
			{
				_tempWindowResizeBorderThickness = new Thickness(6.0);
				WindowResizeBorderThickness = _tempWindowResizeBorderThickness;
				return;
			}
			WindowChrome windowChrome = new WindowChrome
			{
				CornerRadius = 0.0,
				GlassFrameThickness = new Thickness(0.0),
				UseAeroCaptionButtons = false
			};
			Binding binding = new Binding("HeaderHeight")
			{
				Source = this
			};
			global::ForkPlus.UI.WpfCompat.BindingCompat.SetBinding(windowChrome, WindowChrome.CaptionHeightProperty, binding);
			WindowChrome.SetWindowChrome(this, windowChrome);
			_tempWindowResizeBorderThickness = WindowResizeBorderThickness;
			base.Loaded += Window_Loaded;

			// 批量修复：当窗口自定义标题栏被隐藏时，仍允许拖动窗口（按原版系统标题栏体验）。
			AddHandler(global::Avalonia.Input.InputElement.PointerPressedEvent,
				OnWindowPointerPressedForMoveDrag,
				global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
		}

		private void OnWindowPointerPressedForMoveDrag(object sender, global::Avalonia.Input.PointerPressedEventArgs e)
		{
			if (IsDesignMode)
			{
				return;
			}

			if (TryBeginResizeDrag(e))
			{
				return;
			}

			// 有标题栏时由 PART_WindowHeader 负责拖动；这里只处理“标题栏隐藏”的窗口。
			if (ShowHeader)
			{
				return;
			}

			// 只在顶部一小段区域允许拖动，避免影响主体交互（列表选择/文本编辑等）。
			Point pt = e.GetPosition(this);
			if (pt.Y > DragRegionHeightWhenHeaderHidden)
			{
				return;
			}

			// 点击交互控件（按钮/输入框/列表等）不启动拖动。
			if (e.Source is global::Avalonia.Visual source && IsInteractiveChromeElement(source))
			{
				return;
			}

			if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			{
				BeginMoveDragWithSnapTracking(e);
			}
		}

		private bool TryBeginResizeDrag(global::Avalonia.Input.PointerPressedEventArgs e)
		{
			if (!CanResize || base.WindowState != global::Avalonia.Controls.WindowState.Normal)
			{
				return false;
			}
			if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			{
				return false;
			}

			const double ResizeGripThickness = 6.0;
			Point point = e.GetPosition(this);
			double width = Bounds.Width;
			double height = Bounds.Height;
			bool left = point.X <= ResizeGripThickness;
			bool right = point.X >= width - ResizeGripThickness;
			bool top = point.Y <= ResizeGripThickness;
			bool bottom = point.Y >= height - ResizeGripThickness;
			if (!left && !right && !top && !bottom)
			{
				return false;
			}

			global::Avalonia.Controls.WindowEdge edge;
			if (top && left)
			{
				edge = global::Avalonia.Controls.WindowEdge.NorthWest;
			}
			else if (top && right)
			{
				edge = global::Avalonia.Controls.WindowEdge.NorthEast;
			}
			else if (bottom && left)
			{
				edge = global::Avalonia.Controls.WindowEdge.SouthWest;
			}
			else if (bottom && right)
			{
				edge = global::Avalonia.Controls.WindowEdge.SouthEast;
			}
			else if (left)
			{
				edge = global::Avalonia.Controls.WindowEdge.West;
			}
			else if (right)
			{
				edge = global::Avalonia.Controls.WindowEdge.East;
			}
			else if (top)
			{
				edge = global::Avalonia.Controls.WindowEdge.North;
			}
			else
			{
				edge = global::Avalonia.Controls.WindowEdge.South;
			}

			try
			{
				BeginResizeDrag(edge, e);
				e.Handled = true;
				return true;
			}
			catch (global::System.InvalidOperationException)
			{
				return false;
			}
		}

		protected void OnContentRendered(EventArgs e)
		{
			if (base.SizeToContent == global::Avalonia.Controls.SizeToContent.WidthAndHeight)
			{
				InvalidateMeasure();
			}
		}

		// Migration note（根因）：WPF Window.StateChanged 事件 / OnStateChanged 虚方法在 Avalonia 无直接
		// 对应（无 StateChanged 事件）。原 protected void OnStateChanged 无人调用（死代码），导致
		// 最大化/还原按钮可见性切换、最大化边框厚度调整全部失效。现通过 OnPropertyChanged
		// 监听 WindowStateProperty 转发，并改为 virtual 供子类（MainWindow 等）override。
		protected override void OnPropertyChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == WindowStateProperty)
			{
				OnStateChanged(EventArgs.Empty);
			}
		}

		protected virtual void OnStateChanged(EventArgs e)
		{
			if (IsDesignMode)
			{
				return;
			}
			AdjustButtonsVisibilityToWindowState();
			if (base.WindowState == global::Avalonia.Controls.WindowState.Maximized)
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

		protected override void OnApplyTemplate(global::Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			UnwireChromeEvents();
			_templatePartWindowHeader = this.GetTemplateChild("PART_WindowHeader") as global::Avalonia.Controls.Control;
			_closeButton = this.GetTemplateChild("PART_CloseButton") as Button;
			_minimizeButton = this.GetTemplateChild("PART_MinimizeButton") as Button;
			_maximizeButton = this.GetTemplateChild("PART_MaximizeButton") as Button;
			_restoreButton = this.GetTemplateChild("PART_RestoreButton") as Button;
			WireChromeEvents();
			AdjustButtonsVisibilityToWindowState();
		}

		// Migration note（根因）：WPF 模板按钮用 Command="{x:Static SystemCommands.XxxWindowCommand}" +
		// Window.CommandBindings 路由；Avalonia 无 CommandBindings 路由（CommandRouter 只管键盘手势），
		// 模板按钮又不携带 Command → 最小化/最大化/还原/关闭全部失灵。改为代码后置直接挂 Click。
		private void WireChromeEvents()
		{
			if (_minimizeButton != null)
			{
				_minimizeButton.Click += MinimizeButton_Click;
			}
			if (_maximizeButton != null)
			{
				_maximizeButton.Click += MaximizeButton_Click;
			}
			if (_restoreButton != null)
			{
				_restoreButton.Click += RestoreButton_Click;
			}
			if (_closeButton != null)
			{
				_closeButton.Click += CloseButton_Click;
			}
			if (_templatePartWindowHeader != null)
			{
				_templatePartWindowHeader.PointerPressed += WindowHeader_PointerPressed;
				_templatePartWindowHeader.DoubleTapped += WindowHeader_DoubleTapped;
			}
		}

		/// <summary>模板可能重复应用（主题切换等），重挂前先解绑旧实例，防止重复触发。</summary>
		private void UnwireChromeEvents()
		{
			if (_minimizeButton != null)
			{
				_minimizeButton.Click -= MinimizeButton_Click;
			}
			if (_maximizeButton != null)
			{
				_maximizeButton.Click -= MaximizeButton_Click;
			}
			if (_restoreButton != null)
			{
				_restoreButton.Click -= RestoreButton_Click;
			}
			if (_closeButton != null)
			{
				_closeButton.Click -= CloseButton_Click;
			}
			if (_templatePartWindowHeader != null)
			{
				_templatePartWindowHeader.PointerPressed -= WindowHeader_PointerPressed;
				_templatePartWindowHeader.DoubleTapped -= WindowHeader_DoubleTapped;
			}
		}

		private void MinimizeButton_Click(object sender, RoutedEventArgs e)
		{
			base.WindowState = global::Avalonia.Controls.WindowState.Minimized;
		}

		private void MaximizeButton_Click(object sender, RoutedEventArgs e)
		{
			base.WindowState = global::Avalonia.Controls.WindowState.Maximized;
		}

		private void RestoreButton_Click(object sender, RoutedEventArgs e)
		{
			base.WindowState = global::Avalonia.Controls.WindowState.Normal;
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		// Migration note（根因）：WPF WindowChrome.CaptionHeight 区域原生支持拖动/双击最大化；
		// Avalonia ExtendClientArea 后整个窗口都是客户区，需自实现标题栏交互。
		// Button/Menu 等控件会吃掉 PointerPressed（Handled），事件不再冒泡到标题栏，
		// 因此点按钮/菜单不会误触发拖动。
		private void WindowHeader_PointerPressed(object sender, global::Avalonia.Input.PointerPressedEventArgs e)
		{
			if (IsDesignMode)
			{
				return;
			}
			// 标题栏区域包含菜单/按钮等交互控件时，不要启动窗口拖动，否则会导致菜单点击无响应。
			if (e.Source is global::Avalonia.Visual source && IsInteractiveChromeElement(source))
			{
				return;
			}
			if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			{
				BeginMoveDragWithSnapTracking(e);
			}
		}

		// ===== 问题H：标题栏拖拽贴屏幕顶边松手 → 最大化（模拟原生 Aero Snap 顶边行为）=====

		/// <summary>
		/// 贴顶吸附是否在当前窗口启用。测试子类 override 以在 headless 下驱动完整链路。
		/// </summary>
		protected virtual bool IsTitleBarDragSnapEnabled => TitleBarDragSnapTracker.IsPlatformSupported;

		/// <summary>
		/// 创建贴顶吸附跟踪器。测试子类 override 注入假光标/屏幕提供者；
		/// 生产实现用 MouseHelper.GetMousePosition（硬件级光标查询，模态循环中可用）
		/// 与 Screens.ScreenFromPoint（与原生按光标所在屏幕判定一致）。
		/// </summary>
		protected virtual TitleBarDragSnapTracker CreateTitleBarDragSnapTracker()
		{
			return new TitleBarDragSnapTracker(
				MouseHelper.GetMousePosition,
				delegate (global::Avalonia.PixelPoint point)
				{
					return Screens?.ScreenFromPoint(point);
				});
		}

		/// <summary>
		/// 启动标题栏移动拖拽并跟踪贴顶吸附（拖到屏幕顶边松手 → 最大化，原生 Aero Snap 顶边行为）。
		/// 关键机制（Avalonia 12 Win32 源码实证）：
		///   1) BeginMoveDrag 只是向 Dispatcher post 一个 DispatcherPriority.Send 任务，任务内
		///      SendMessage(WM_SYSCOMMAND, SC_MOUSEMOVE) 进入原生模态移动循环并阻塞该任务直到
		///      用户松开鼠标；
		///   2) 移动循环期间 Avalonia 不派发指针事件，但窗口每次 WM_MOVE 都会触发 PositionChanged
		///      （WindowImpl.AppWndProc），借它做拖拽中的贴顶采样；
		///   3) 紧随其后 post 同优先级任务——同优先级 FIFO，必在移动循环任务之后执行，即拖拽已
		///      结束的时机——此时仍贴顶则最大化。
		/// </summary>
		private void BeginMoveDragWithSnapTracking(global::Avalonia.Input.PointerPressedEventArgs e)
		{
			if (!TitleBarDragSnapTracker.ShouldTrackDrag(IsTitleBarDragSnapEnabled, CanResize, base.WindowState))
			{
				TryBeginMoveDrag(e);
				return;
			}

			TitleBarDragSnapTracker tracker = CreateTitleBarDragSnapTracker();
			tracker.Begin();
			_activeDragSnapTracker = tracker;
			EventHandler<global::Avalonia.Controls.PixelPointEventArgs> positionHandler = delegate (object sender, global::Avalonia.Controls.PixelPointEventArgs args)
			{
				_activeDragSnapTracker?.Sample(args.Point);
			};
			PositionChanged += positionHandler;
			try
			{
				BeginMoveDrag(e);
			}
			catch (global::System.InvalidOperationException)
			{
				// 窗口尚未显示等边缘状态：忽略，避免拖动破坏显示。
				PositionChanged -= positionHandler;
				_activeDragSnapTracker = null;
				return;
			}
			global::Avalonia.Threading.Dispatcher.UIThread.Post(delegate
			{
				PositionChanged -= positionHandler;
				TitleBarDragSnapTracker activeTracker = _activeDragSnapTracker;
				_activeDragSnapTracker = null;
				if (activeTracker != null
					&& TitleBarDragSnapTracker.ShouldMaximize(activeTracker.TopSnapEngaged, IsVisible, base.WindowState))
				{
					base.WindowState = global::Avalonia.Controls.WindowState.Maximized;
				}
			}, global::Avalonia.Threading.DispatcherPriority.Send);
		}

		/// <summary>裸 BeginMoveDrag + 边缘状态吞异常（与历史行为一致）。</summary>
		private void TryBeginMoveDrag(global::Avalonia.Input.PointerPressedEventArgs e)
		{
			try
			{
				BeginMoveDrag(e);
			}
			catch (global::System.InvalidOperationException)
			{
				// 窗口尚未显示等边缘状态：忽略
			}
		}

		private void WindowHeader_DoubleTapped(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
		{
			if (IsDesignMode || !CanResize)
			{
				return;
			}
			// 双击按钮/菜单等交互元素时不切换最大化，仅双击空白标题栏生效。
			if (e.Source is global::Avalonia.Visual source && IsInteractiveChromeElement(source))
			{
				return;
			}
			if (base.WindowState == global::Avalonia.Controls.WindowState.Maximized)
			{
				base.WindowState = global::Avalonia.Controls.WindowState.Normal;
			}
			else
			{
				base.WindowState = global::Avalonia.Controls.WindowState.Maximized;
			}
		}

		/// <summary>判断可视元素（或其祖先）是否为交互控件（按钮/菜单/输入框），用于排除双击误判。</summary>
		private static bool IsInteractiveChromeElement(global::Avalonia.Visual visual)
		{
			for (global::Avalonia.Visual current = visual; current != null; current = current.GetVisualParent())
			{
				if (current is Button
					|| current is global::Avalonia.Controls.Primitives.ToggleButton
					|| current is global::Avalonia.Controls.MenuItem
					|| current is global::Avalonia.Controls.Menu
					|| current is global::Avalonia.Controls.TextBox
					|| current is global::Avalonia.Controls.ComboBox
					|| current is global::Avalonia.Controls.ListBox
					|| current is global::Avalonia.Controls.ListBoxItem
					|| current is global::Avalonia.Controls.Primitives.ScrollBar
					|| current is global::Avalonia.Controls.ScrollViewer
					|| current is global::Avalonia.Controls.Slider)
				{
					return true;
				}
			}
			return false;
		}

		protected void OnSourceInitialized(EventArgs e)
		{
			if (IsDesignMode)
			{
				return;
			}
			// Migration note：WPF HwndSource.AddHook(WM_NCCALCSIZE 等自绘 chrome 钩子)为 Win32 专用，
			// Avalonia 由 SystemDecorations/ExtendClientAreaChromeIntoTitleBar 替代，暂 no-op 保留 HwndSourceHook 方法。
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			if (IsDesignMode)
			{
				return;
			}
			// Migration note（根因）：原 WPF SystemCommands 的 CommandBinding 路由在 Avalonia 无对应
			// （CommandRouter 只派发键盘手势，SystemCommands shim 不带手势，绑定永不触发），
			// 窗口按钮改为 OnApplyTemplate 里直接挂 Click（见 WireChromeEvents），此处删除死代码。
			_tempWindowState = base.WindowState;
			_tempBorderThickness = base.BorderThickness;
			if (base.WindowState == global::Avalonia.Controls.WindowState.Maximized)
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
			case global::Avalonia.Controls.WindowState.Normal:
				_maximizeButton?.Show();
				_restoreButton?.Collapse();
				break;
			case global::Avalonia.Controls.WindowState.Maximized:
				_maximizeButton?.Collapse();
				_restoreButton?.Show();
				break;
			}
			switch (ResizeMode)
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
				WindowResizeBorderThickness = ((base.WindowState == global::Avalonia.Controls.WindowState.Maximized) ? MaximizedWindowResizeBorderThickness : _tempWindowResizeBorderThickness);
				break;
			case 36:
				WindowLocationStateExtensions.GetMinMaxInfo(hwnd, lparam);
				WindowResizeBorderThickness = ((base.WindowState == global::Avalonia.Controls.WindowState.Maximized) ? MaximizedWindowResizeBorderThickness : _tempWindowResizeBorderThickness);
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

		private static void OnShowHeaderChanged(global::Avalonia.AvaloniaObject d, global::Avalonia.AvaloniaPropertyChangedEventArgs e)
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
