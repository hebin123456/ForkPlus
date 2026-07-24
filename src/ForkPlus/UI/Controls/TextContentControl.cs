// 阶段 4.5：WPF System.Windows.* → Avalonia.*。
// WPF SetResourceReference(FrameworkElement.StyleProperty, typeof(CodeEditor)) → Avalonia StyleKeyOverride。
// WPF WeakEventManager<T,S>.AddHandler → 直接事件订阅（阶段 6 改用 Avalonia WeakEvent）。
using System;
using Avalonia;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Diff;

namespace ForkPlus.UI.Controls
{
	public class TextContentControl : CodeEditor, FileContentControl.IFileContentControlSubControl
	{
		// 阶段 4.5：WPF SetResourceReference(FrameworkElement.StyleProperty, typeof(CodeEditor))
		// → Avalonia StyledElement.StyleKeyOverride（让派生控件复用基类 CodeEditor 的默认样式）。
		protected override Type StyleKeyOverride => typeof(CodeEditor);

		[Null]
		private Content _content;

		private readonly SyntaxHighlighting _syntaxHighlighting;

		private readonly CodeEditorLineNumberMargin _codeEditorLineNumberMargin;

		public CodeEditorScrollPositionCache PositionCache { get; set; }

		public TextContentControl()
		{
			// 阶段 4.5：WPF SetResourceReference → StyleKeyOverride（见上）。
			_codeEditorLineNumberMargin = new CodeEditorLineNumberMargin();
			base.TextArea.LeftMargins.Add(_codeEditorLineNumberMargin);
			_syntaxHighlighting = new SyntaxHighlighting();
			base.TextArea.TextView.LineTransformers.Add(_syntaxHighlighting);
			base.FontSize = ForkPlusSettings.Default.CodeEditorFontSize;
			// 阶段 4.5：WPF WeakEventManager<T,S>.AddHandler → 直接事件订阅。
			// TODO(4.6-a): 阶段 6 改用 Avalonia WeakEvent 避免内存泄漏。
			NotificationCenter.Current.CodeEditorFontSizeChanged += delegate
			{
				base.FontSize = ForkPlusSettings.Default.CodeEditorFontSize;
			};
			NotificationCenter.Current.ApplicationThemeChanged += ApplicationThemeChanged;
			NotificationCenter.Current.DisableSyntaxHighlightingChanged += DisableSyntaxHighlightingChanged;
		}

		public void SetContent([Null] TextContent content)
		{
			SaveScrollPosition(_content);
			_content = content;
			base.Text = content?.Text ?? string.Empty;
			_codeEditorLineNumberMargin.UpdateLineNumbersData();
			RefreshSyntaxHighlighting();
			RestoreScrollPosition(content);
			InvalidateVisual();
		}

		public void ControlWillBeRemovedFromFileContentControl()
		{
			SaveScrollPosition(_content);
		}

		private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
		{
			base.TextArea.TextView.Redraw();
		}

		private void DisableSyntaxHighlightingChanged(object sender, EventArgs<bool> e)
		{
			RefreshSyntaxHighlighting();
			base.TextArea.TextView.Redraw();
		}

		private void RefreshSyntaxHighlighting()
		{
			if (!ForkPlusSettings.Default.DisableSyntaxHighlighting)
			{
				Content content = _content;
				if (content != null)
				{
					_syntaxHighlighting.Highlight(content.Path, base.Text);
					return;
				}
			}
			_syntaxHighlighting.Clear();
		}

		private void RestoreScrollPosition([Null] Content content)
		{
			if (content != null)
			{
				CodeEditorScrollPositionCache positionCache = PositionCache;
				if (positionCache != null)
				{
					string key = MakeKey(content);
					CodeEditorScrollPositionCache.Position position = positionCache.GetPosition(key) ?? CodeEditorScrollPositionCache.Position.Empty;
					double valueOrDefault = this.GetScrollPositionByCharacterIndex(position.Src.GetValueOrDefault()).GetValueOrDefault();
					ScrollToVerticalOffset(valueOrDefault + position.OffsetY);
				}
			}
		}

		private void SaveScrollPosition([Null] Content content)
		{
			if (content != null)
			{
				CodeEditorScrollPositionCache positionCache = PositionCache;
				if (positionCache != null)
				{
					string key = MakeKey(content);
					var (value, offsetY) = this.GetFirstVisibleCharacterPosition();
					positionCache.SetPosition(key, new CodeEditorScrollPositionCache.Position(value, null, offsetY));
				}
			}
		}

		private string MakeKey([Null] Content content)
		{
			string path = content.Path;
			return "FileTree:" + path;
		}
	}
}
