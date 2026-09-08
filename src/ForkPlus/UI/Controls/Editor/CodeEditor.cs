using Avalonia.Input;
using Avalonia.Media;
using ForkPlus.UI.Controls.Commands;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.UserControls;
using AvaloniaEdit;
using ForkPlus.UI.Helpers;
using ForkPlus.UI.WpfCompat;
using Avalonia;

namespace ForkPlus.UI.Controls.Editor
{
	public class CodeEditor : TextEditor
	{
		private const string PartNameSearchPanel = "PART_SearchPanelUserControl";

		private CodeEditorSearchPanelUserControl _templatePartSearchPanel;

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
