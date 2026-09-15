using System;
using System.Reflection;
using Avalonia.Input;
using Avalonia.Media;
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
			TextArea.PointerPressed += TextArea_PointerPressed;
			TextArea.PointerReleased += TextArea_PointerReleased;
			TextArea.PointerCaptureLost += TextArea_PointerCaptureLost;
		}

		/// <summary>正确的 caret 跟随滚动：真实像素定位（视口比例滚动）；鼠标按住选取期间不滚动。</summary>
		private void CaretFollowScrollHandler(object sender, EventArgs e)
		{
			if (_pointerSelecting)
			{
				return;
			}
			ScrollTo(TextArea.Caret.Line, TextArea.Caret.Column);
		}

		private void TextArea_PointerPressed(object sender, PointerPressedEventArgs e)
		{
			// 按住期间（点击/拖动选取）暂停 caret 跟随：拖动选取时 caret 随指针高频移动，
			// 逐次滚动会造成视口跳动；点击落点本身就在可视区内，无需滚动。
			_pointerSelecting = true;
		}

		private void TextArea_PointerReleased(object sender, PointerReleasedEventArgs e)
		{
			_pointerSelecting = false;
		}

		private void TextArea_PointerCaptureLost(object sender, PointerCaptureLostEventArgs e)
		{
			_pointerSelecting = false;
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
