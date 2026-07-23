using System.Windows.Input;
using ForkPlus.UI.Controls.Commands;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.UserControls;
using ICSharpCode.AvalonEdit;
using ForkPlus.UI.Helpers;

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
			base.Options.InheritWordWrapIndentation = false;
			base.Options.EnableHyperlinks = false;
			base.Options.EnableEmailHyperlinks = false;
			// TODO(4.7-a): 验证 Avalonia.AvalonEdit TextArea 是否有 SelectionBorder(Pen)/SelectionCornerRadius；
			// WPF 版设 SelectionBorder=null + SelectionCornerRadius=0 以扁平化选区。Avalonia 版可能用 SelectionBrush/SelectionCornerRadius。
			base.TextArea.SelectionBorder = null;
			base.TextArea.SelectionCornerRadius = 0.0;
			base.TextArea.TextView.BackgroundRenderers.Add(new ClearTypeBackgroundRenderer());
			// 阶段 4 里程碑 4.7-a：移除 RenderOptions.SetClearTypeHint（WPF-only，Avalonia 无等价物，文本渲染由平台决定）。
		}

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();
			_templatePartSearchPanel = GetTemplateChild("PART_SearchPanelUserControl") as CodeEditorSearchPanelUserControl;
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

		protected override void OnPreviewKeyDown(KeyEventArgs e)
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
			base.OnPreviewKeyDown(e);
		}
	}
}
