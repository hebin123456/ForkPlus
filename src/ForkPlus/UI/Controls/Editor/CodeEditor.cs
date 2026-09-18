using System;
using System.Reflection;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using ForkPlus.UI.Controls.Commands;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.UserControls;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using ForkPlus.UI.Helpers;
using ForkPlus.UI.WpfCompat;
using Avalonia;

namespace ForkPlus.UI.Controls.Editor
{
	public class CodeEditor : TextEditor
	{
		private const string PartNameSearchPanel = "PART_SearchPanelUserControl";

		/// <summary>
		/// 修复（2026-09-14，"展开全部代码后拖动选取区域界面回弹"）：AvaloniaEdit 12.0.0 的
		/// TextArea 在构造函数里订阅 Caret.PositionChanged → ScrollToLine(line, 2) →
		/// BringIntoView(Rect(1, 行号±2, 0, 1))——把"行号"当像素 Y 坐标用。文档行数超过
		/// 视口像素高度时（如"展开全部"后上千行），每次 caret 移动（含拖动选取的每一次
		/// 指针移动）都会触发"跳变 + extent 钳回"的视觉回弹。反射摘除该订阅，换成
		/// TextEditor.ScrollTo(line, column)（真实像素定位，GetVisualPosition 数学正确），
		/// 且鼠标按住（点选/拖动选取）期间不滚动。TextView.HighlightedLine 无任何消费者
		///（全仓库检索为空），随订阅一并失效无副作用。
		/// </summary>
		[Null]
		private static readonly MethodInfo TextAreaCaretPositionChangedMethod = typeof(TextArea).GetMethod("CaretPositionChanged", BindingFlags.NonPublic | BindingFlags.Instance);

		private CodeEditorSearchPanelUserControl _templatePartSearchPanel;

		private bool _pointerSelecting;

	/// <summary>当前拖选按下的指针（用于捕获被夺走后取回）。</summary>
	private global::Avalonia.Input.IPointer _dragPointer;

	/// <summary>挂起的"重挂可视树后取回捕获"回调（AdornerLayer 重建窗口内容树场景）。</summary>
	private EventHandler<VisualTreeAttachmentEventArgs> _recaptureOnAttach;

		public bool IsSearchBarFocused => _templatePartSearchPanel?.IsTextBoxFocused ?? false;

		public double SearchBarHeight => _templatePartSearchPanel?.PanelHeight ?? 0.0;

		public CodeEditor()
		{
			object codeEditorTheme = Application.Current?.TryFindResource(typeof(CodeEditor));
			if (codeEditorTheme != null)
			{
				global::ForkPlus.UI.WpfCompat.StyleCompat.SetStyle(this, codeEditorTheme);
			}
			base.Options.InheritWordWrapIndentation = false;
			base.Options.EnableHyperlinks = false;
			base.Options.EnableEmailHyperlinks = false;
			// Bug 修复（2026-09-15，"同一区域 WPF 3.13.2 显示 43 行、Avalonia 只有 36 行"）：
			// WPF AvalonEdit 的行槽 = TextLine 自然行高（Consolas@13 实测 ~15.22px）；
			// AvaloniaEdit 12.0.0 的 TextEditorOptions.LineHeightFactor 默认 1.16，行槽被
			// 放大到 自然高×1.16（≈17.66px）→ 同视口可见行数少 16%（43→36）。显式置 1.0
			// 对齐 WPF 自然行高，行密度与 3.13.2 一致。副作用联动：默认行槽变矮后，含中文
			// 行的自然高必须 ≤ 新行槽——内嵌 CJK 回退字体垂直度量同版本从 1.25em 收紧到
			// 1.16em（见 FontSetup），否则 CJK 行槽超默认槽，SideBySide 行错位回归。
			base.Options.LineHeightFactor = 1.0;
			// Bug 修复（2026-09-04，"FileDiff 高度计算多了，滚动条可拉到很下面有一大块空白"）：
			// WPF AvalonEdit 的 AllowScrollBelowDocument 默认 false（拉到底即文档末尾）；
			// AvaloniaEdit 12.x 把默认值改成了 true——TextView.MeasureOverride 会给
			// 滚动 extent 加"viewport 高 - 一行"的额外空间，diff/代码编辑器都能滚到
			// 文档底部之下一大块空白（探针实测 Extent=文档高+viewport）。显式关闭对齐 WPF。
			base.Options.AllowScrollBelowDocument = false;
			base.TextArea.SelectionBorder = null;
			base.TextArea.SelectionCornerRadius = 0.0;
			base.TextArea.TextView.BackgroundRenderers.Add(new ClearTypeBackgroundRenderer());
			for (int i = 0; i < base.TextArea.TextView.Layers.Count; i++)
			{
				RenderOptionsShim.SetClearTypeHint(base.TextArea.TextView.Layers[i], ClearTypeHint.Enabled);
			}
			ReplaceBrokenCaretFollowScroll();
			// 修复（2026-09-15，"程序化替换 Text 后视口跳到文档底部"）：Text setter（TextDocument.Replace）
			// 过程中 caret 会先被临时移动到文档末行再重置回行首，每次移动都触发上面新装的
			// caret 跟随 → ScrollTo 在视觉行未重建时会挂 pending 滚动，布局完成后按"末行"
			// 的陈旧请求把视口滚到文档底部（诊断探针实证：500 行文档 Text 赋值后
			// Offset.Y 已等于 maxOffset，GitMmAnsiOutputTests 滚动通道测试假红；
			// 产品侧表现为打开大文件/diff 后视口不在顶部而在底部）。WPF 原版语义是
			// 程序化文档替换不跟随滚动 → 这里在 TextChanged 时挂起跟随一轮，用
			// Background 优先级的延迟恢复（吞掉本 dispatcher 轮次内 Text 替换引发的
			// 全部 caret 事件，包括布局管线里 pending 的那一次），之后的用户键盘/
			// 鼠标 caret 移动恢复正常跟随。
			base.TextChanged += CodeEditor_TextChanged_SuspendCaretFollow;
		}

		private bool _suppressCaretFollow;

		private void CodeEditor_TextChanged_SuspendCaretFollow(object sender, EventArgs e)
		{
			_suppressCaretFollow = true;
			global::Avalonia.Threading.Dispatcher.UIThread.Post(delegate
			{
				_suppressCaretFollow = false;
			}, global::Avalonia.Threading.DispatcherPriority.Background);
		}

	/// <summary>
	/// 摘除 AvaloniaEdit TextArea 内部的坏 caret 跟随滚动（行号当像素），替换为
	/// 正确的 TextEditor.ScrollTo；反射成员缺失时保持原生行为（不抛异常）。
	/// </summary>
	private void ReplaceBrokenCaretFollowScroll()
	{
		if (TextAreaCaretPositionChangedMethod == null)
		{
			return;
		}
		try
		{
			EventHandler brokenHandler = (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), TextArea, TextAreaCaretPositionChangedMethod);
			TextArea.Caret.PositionChanged -= brokenHandler;
		}
		catch
		{
			// 摘除失败（包内部结构变更）：不替换，保持原生行为
			return;
		}
		TextArea.Caret.PositionChanged += CaretFollowScrollHandler;
		// 修复（2026-09-17，"大区域从下往上拖选暂存内容，界面弹上去"）：
		// 原先用普通事件订阅（TextArea.PointerPressed += ...），但 AvaloniaEdit 的
		// SelectionMouseHandler（构造期先订阅、同一元素）在 PointerPressed 处理末尾把
		// e.Handled 置 true——后订阅且未带 handledEventsToo 的处理器全部被跳过，
		// _pointerSelecting 在真实鼠标按下时从未置位。拖选期间 caret 跟随未被抑制，
		// 每次指针移动 caret 变更都触发 ScrollTo(line, column)（30% 视口阈值 + 把 caret
		// 行滚到视口中央的语义）——从下往上拖选时视口逐事件"居中跳变"上百像素，
		// 指针越过视口上缘后更是一路弹跳到文档顶部。
		// 改用 AddHandler + handledEventsToo 挂接：按下走 Tunnel（先于包内处理器收到，
		// 抢在其 SetCaretOffsetToMousePosition 移动 caret 之前完成置位）；抬起走
		// Bubble（订阅晚于包内处理器 → 后执行，包内 ExtendSelectionOnMouseUp 的
		// caret 移动仍处于抑制中，抬起本身不再触发居中跳变）。
		TextArea.AddHandler(InputElement.PointerPressedEvent, TextArea_PointerPressed,
			global::Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
		TextArea.AddHandler(InputElement.PointerReleasedEvent, TextArea_PointerReleased,
			global::Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
		// PointerCaptureLost 是 Direct 路由事件（Avalonia 12 注册为 Direct），必须以
		// Direct 策略挂接才能收到——Bubble 策略的处理器对 Direct 事件不触发。
		TextArea.AddHandler(InputElement.PointerCaptureLostEvent, TextArea_PointerCaptureLost,
			global::Avalonia.Interactivity.RoutingStrategies.Direct, handledEventsToo: true);
		// 拖选中途捕获被外部挪走又恢复（如 AdornerLayer 首建重建窗口内容树）时，
		// 按下处理器错过的后续拖动 move 事件在此重新置位（Tunnel 先于包内扩展选区
		// 的 Bubble 处理器，保证抑制先于 caret 移动生效）。
		TextArea.AddHandler(InputElement.PointerMovedEvent, TextArea_PointerMoved,
			global::Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
	}

		/// <summary>正确的 caret 跟随滚动：真实像素定位（视口比例滚动）；鼠标按住选取与程序化文档替换期间不滚动。</summary>
		private void CaretFollowScrollHandler(object sender, EventArgs e)
		{
			if (_pointerSelecting || _suppressCaretFollow)
			{
				return;
			}
			ScrollTo(TextArea.Caret.Line, TextArea.Caret.Column);
		}

private void TextArea_PointerPressed(object sender, PointerPressedEventArgs e)
{
	// 按住期间（点击/拖动选取）暂停 caret 跟随：拖动选取时 caret 随指针高频移动，
	// 逐次滚动会造成视口跳动；点击落点本身就在可视区内，无需滚动。
	// 仅左键（AvaloniaEdit 只用左键拖选/定位 caret），右键菜单按压不抑制。
	if (e.GetCurrentPoint(TextArea).Properties.IsLeftButtonPressed)
	{
		_pointerSelecting = true;
		_dragPointer = e.Pointer;
	}
}

private void TextArea_PointerReleased(object sender, PointerReleasedEventArgs e)
{
	_pointerSelecting = false;
	_dragPointer = null;
	CancelPendingRecapture();
}

private void TextArea_PointerCaptureLost(object sender, PointerCaptureLostEventArgs e)
{
	// 修复（2026-09-17，"首次拖选暂存内容时选区冻结在两行"）：差异视图悬浮
	// Stage/Discard 按钮首次构建时，WpfCompat AdornerLayer 会把窗口内容树整棵
	// 摘下重挂（cc.Content = null → grid 重包）。摘除瞬间 Avalonia 把指针捕获
	// 挪到最近仍在树的祖先（Pointer.OnCaptureDetached → GetNextCapture），拖选
	// 的 move/Release 从此不再路由进 TextArea——选区冻结，且 Release 收不到、
	// 抑制标志悬挂。重挂完成（AttachedToVisualTree，与摘除同一次同步调用内）
	// 立即取回捕获：捕获仍空或停在祖先上（被"停车"）才取，被无关元素（弹窗
	// 等）正当持有时不抢。
	if (_pointerSelecting && _dragPointer != null)
	{
		var ptr = _dragPointer;
		var ta = TextArea;
		CancelPendingRecapture();
		_recaptureOnAttach = delegate
		{
			_recaptureOnAttach = null;
			var captured = ptr.Captured;
			if (ReferenceEquals(ptr.Captured, ta)) return;
			if (captured == null || (captured is Visual v && v.IsVisualAncestorOf(ta)))
			{
				ptr.Capture(ta);
				// 取回后拖选继续：重新武装抑制（后续 move 的 Tunnel 处理器亦会兜底置位）。
				_pointerSelecting = true;
			}
		};
		ta.AttachedToVisualTree += _recaptureOnAttach;
	}
	_pointerSelecting = false;
	_dragPointer = null;
}

private void CancelPendingRecapture()
{
	if (_recaptureOnAttach != null)
	{
		TextArea.AttachedToVisualTree -= _recaptureOnAttach;
		_recaptureOnAttach = null;
	}
}

private void TextArea_PointerMoved(object sender, PointerEventArgs e)
{
	// 兜底重新置位：捕获中途丢失又被恢复的拖拽（按下事件未再触发）期间，
	// 只要仍按着左键就保持抑制，防 caret 跟随的居中跳变混入拖选。
	if (e.GetCurrentPoint(TextArea).Properties.IsLeftButtonPressed)
	{
		_pointerSelecting = true;
		_dragPointer = e.Pointer;
	}
}

		protected override void OnApplyTemplate(global::Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			_templatePartSearchPanel = this.GetTemplateChild("PART_SearchPanelUserControl") as CodeEditorSearchPanelUserControl;
			_templatePartSearchPanel?.Attach(base.TextArea);
		}

		public void ShowSearchBar()
		{
			_templatePartSearchPanel?.ShowSearchBar();
		}

		public void HideSearchBar()
		{
			_templatePartSearchPanel?.HideSearchBar();
		}

		public double GetScrollPosition()
		{
			return base.TextArea.TextView.ScrollOffset.Y;
		}

		public void SetScrollPosition(double y)
		{
			// Migration note：AvaloniaEdit 的 TextEditor.ScrollToVerticalOffset 是空操作，
			// 改走 ScrollViewerCompat（经模板 PART_ScrollViewer.Offset 真正滚动）。
			this.ScrollToVerticalOffsetCompat(y);
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if ((e.Key == Key.F3 || (e.Key == Key.F && KeyboardHelper.IsCtrlDown)) && !KeyboardHelper.IsShiftDown)
			{
				CodeEditorSearchPanelUserControl templatePartSearchPanel = _templatePartSearchPanel;
				if (templatePartSearchPanel == null || !templatePartSearchPanel.IsTextBoxFocused)
				{
					ShowSearchBar();
					e.Handled = true;
				}
			}
			if (e.Key == Key.Escape)
			{
				CodeEditorSearchPanelUserControl templatePartSearchPanel2 = _templatePartSearchPanel;
				if (templatePartSearchPanel2 != null && templatePartSearchPanel2.IsTextBoxFocused)
				{
					HideSearchBar();
					e.Handled = true;
				}
			}
			if (this is DiffCodeEditor editor)
			{
				CodeEditorSearchPanelUserControl templatePartSearchPanel3 = _templatePartSearchPanel;
				if ((templatePartSearchPanel3 == null || !templatePartSearchPanel3.IsTextBoxFocused) && e.Key == Key.C && KeyboardHelper.IsCtrlDown && KeyboardHelper.IsShiftDown)
				{
					CopyAsPatchCommand.Execute(editor);
					e.Handled = true;
				}
			}
			base.OnKeyDown(e);
		}
	}
}
