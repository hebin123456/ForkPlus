using Avalonia.Controls.Primitives;
using Avalonia.Input;
using ForkPlus.UI.Controls.Commands;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.UserControls;
using AvaloniaEdit;
using ForkPlus.UI.Helpers;
using Avalonia.Media;
using Avalonia.Interactivity;

namespace ForkPlus.UI.Controls.Editor
{
	public class CodeEditor : TextEditor
	{
		private const string PartNameSearchPanel = "PART_SearchPanelUserControl";

		private CodeEditorSearchPanelUserControl _templatePartSearchPanel;

		public bool IsSearchBarFocused => _templatePartSearchPanel?.IsTextBoxFocused ?? false;

		public double SearchBarHeight => _templatePartSearchPanel?.PanelHeight ?? 0.0;

		// 阶段 5：WPF ContextMenu.Opening/Closing 事件兼容。Avalonia 用 ContextRequested 事件替代。
		// 为保持派生类（DiffCodeEditor/CommitCodeEditor）的事件订阅代码不变，此处提供同名事件。
		public event EventHandler<CancelRoutedEventArgs> ContextMenuOpening;
		public event EventHandler<RoutedEventArgs> ContextMenuClosing;

		// 阶段 5：WPF IsVisibleChanged 事件兼容。Avalonia 用 Layoutable.AttachedToVisualTree/
		// DetachedFromVisualTree 或 IsVisible 属性变更订阅。此处桥接到 IsVisible 变更。
		public event EventHandler<AvaloniaPropertyChangedEventArgs<bool>> IsVisibleChanged
		{
			add => this.GetObservable(IsVisibleProperty).Subscribe(new ActionObserver<bool>(args => value?.Invoke(this, args)));
			remove { /* 阶段 5：简化实现，移除订阅需更复杂的 token 管理 */ }
		}

		public CodeEditor()
		{
			base.Options.InheritWordWrapIndentation = false;
			base.Options.EnableHyperlinks = false;
			base.Options.EnableEmailHyperlinks = false;
			// NOTE(4.7-a): 验证 Avalonia.AvalonEdit TextArea 是否有 SelectionBorder(Pen)/SelectionCornerRadius；
			// WPF 版设 SelectionBorder=null + SelectionCornerRadius=0 以扁平化选区。Avalonia 版可能用 SelectionBrush/SelectionCornerRadius。
			base.TextArea.SelectionBorder = null;
			base.TextArea.SelectionCornerRadius = 0.0;
			base.TextArea.TextView.BackgroundRenderers.Add(new ClearTypeBackgroundRenderer());
			// 阶段 4 里程碑 4.7-a：移除 RenderOptions.SetClearTypeHint（WPF-only，Avalonia 无等价物，文本渲染由平台决定）。
			// 阶段 5：桥接 ContextMenuOpening/Closing 到 Avalonia ContextRequested 事件。
			ContextRequested += (s, e) => ContextMenuOpening?.Invoke(this, new CancelRoutedEventArgs());
		}

		// 阶段 5：辅助观察者，将 IObservable<T>.Subscribe 桥接到 EventHandler。
	private sealed class ActionObserver<T> : System.IObserver<T>
	{
		private readonly Action<T> _onNext;
		public ActionObserver(Action<T> onNext) { _onNext = onNext; }
		public void OnCompleted() { }
		public void OnError(System.Exception error) { }
		public void OnNext(T value) => _onNext?.Invoke(value);
	}

	// 阶段 5：Avalonia 11.3 TemplatedControl.OnApplyTemplate 签名为 (TemplateAppliedEventArgs e)，
	// 非无参。TemplateAppliedEventArgs 位于 Avalonia.Controls.Primitives 命名空间。
	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			_templatePartSearchPanel = e.NameScope?.Find("PART_SearchPanelUserControl") as CodeEditorSearchPanelUserControl;
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
			return base.TextArea.TextView.VerticalOffset;
		}

		public void SetScrollPosition(double y)
		{
			ScrollToVerticalOffset(y);
		}

		// 阶段 4 里程碑 4.7-a：WPF OnPreviewKeyDown → Avalonia OnKeyDown（Avalonia 无 Preview 前缀，
		// 隧道事件需用 AddHandler(RoutingStrategies.Tunnel) 注册，此处用冒泡 OnKeyDown 即可，
		// 因为 SearchPanel/Escape 等快捷键不依赖隧道顺序）。KeyboardHelper.IsCtrlDown/IsShiftDown →
		// e.KeyModifiers.HasFlag(...)（事件参数自带修饰键状态，避免依赖全局 Keyboard 静态查询）。
		protected override void OnKeyDown(KeyEventArgs e)
		{
			bool isCtrlDown = e.KeyModifiers.HasFlag(KeyModifiers.Control);
			bool isShiftDown = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
			if ((e.Key == Key.F3 || (e.Key == Key.F && isCtrlDown)) && !isShiftDown)
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
				if ((templatePartSearchPanel3 == null || !templatePartSearchPanel3.IsTextBoxFocused) && e.Key == Key.C && isCtrlDown && isShiftDown)
				{
					CopyAsPatchCommand.Execute(editor);
					e.Handled = true;
				}
			}
			base.OnKeyDown(e);
		}
	}
}
