using System.Windows;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Diff;

namespace ForkPlus.UI.Controls
{
	public class TextContentControl : CodeEditor, FileContentControl.IFileContentControlSubControl
	{
		[Null]
		private Content _content;

		private readonly SyntaxHighlighting _syntaxHighlighting;

		private readonly CodeEditorLineNumberMargin _codeEditorLineNumberMargin;

		public CodeEditorScrollPositionCache PositionCache { get; set; }

		public TextContentControl()
		{
			SetResourceReference(FrameworkElement.StyleProperty, typeof(CodeEditor));
			_codeEditorLineNumberMargin = new CodeEditorLineNumberMargin();
			base.TextArea.LeftMargins.Add(_codeEditorLineNumberMargin);
			_syntaxHighlighting = new SyntaxHighlighting();
			base.TextArea.TextView.LineTransformers.Add(_syntaxHighlighting);
			base.FontSize = ForkPlusSettings.Default.CodeEditorFontSize;
			WeakEventManager<NotificationCenter, EventArgs<double>>.AddHandler(NotificationCenter.Current, "CodeEditorFontSizeChanged", delegate
			{
				base.FontSize = ForkPlusSettings.Default.CodeEditorFontSize;
			});
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
			WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(NotificationCenter.Current, "DisableSyntaxHighlightingChanged", DisableSyntaxHighlightingChanged);
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
