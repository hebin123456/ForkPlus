using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using ForkPlus.UI.Controls;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.UserControls
{
	public partial class CodeEditorSearchPanelUserControl : UserControl
	{
		private class SearchResultBackgroundRenderer : IBackgroundRenderer
		{
			public KnownLayer Layer => KnownLayer.Selection;

			public TextSegmentCollection<SearchResult> CurrentResults { get; }

			public SearchResultBackgroundRenderer()
			{
				CurrentResults = new TextSegmentCollection<SearchResult>();
			}

			// 阶段 4 里程碑 4.7-a：WPF DrawingContext → Avalonia.Media.DrawingContext（同名不同类型）。
			// WPF Brush → Avalonia IBrush。Application.Current.TryFindResource → Theme.FindBrush
			// （阶段 4.3-b 迁移后的资源查找门面，内部用 Application.Current.Resources.TryGetResource）。
			public void Draw([Null] TextView textView, [Null] DrawingContext drawingContext)
			{
				if (textView != null && drawingContext != null)
				{
					Geometry geometry = CreateSearchResultsGeometry(textView, CurrentResults);
					if (geometry != null)
					{
						IBrush brush = Theme.FindBrush("RevisionList.SearchMatch.ForegroundBrush");
						drawingContext.DrawGeometry(brush, null, geometry);
					}
				}
			}

			private static Geometry CreateSearchResultsGeometry(TextView textView, TextSegmentCollection<SearchResult> searchResults)
			{
				if (!textView.VisualLinesValid)
				{
					return null;
				}
				ReadOnlyCollection<VisualLine> visualLines = textView.VisualLines;
				if (visualLines.Count == 0)
				{
					return null;
				}
				int offset = visualLines.First().FirstDocumentLine.Offset;
				int endOffset = visualLines.Last().LastDocumentLine.EndOffset;
				BackgroundGeometryBuilder backgroundGeometryBuilder = CreateBackgroundGeometryBuilder();
				foreach (SearchResult item in searchResults.FindOverlappingSegments(offset, endOffset - offset))
				{
					backgroundGeometryBuilder.AddSegment(textView, item);
				}
				return backgroundGeometryBuilder.CreateGeometry();
			}

			private static BackgroundGeometryBuilder CreateBackgroundGeometryBuilder()
			{
				double cornerRadius = 2.0;
				return new BackgroundGeometryBuilder
				{
					AlignToWholePixels = true,
					BorderThickness = 0.0,
					CornerRadius = cornerRadius
				};
			}
		}

		private class SearchResult : TextSegment
		{
			public SearchResult(int location, int length)
			{
				base.StartOffset = location;
				base.Length = length;
			}
		}

		private static readonly double ControlHeight = 30.0;

		private TextArea _textArea;

		private SearchResultBackgroundRenderer _renderer;

		private TextDocument _textDocument;

		private bool _isSearchBarVisible;

		// 阶段 4 里程碑 4.7-a：WPF DependencyProperty.Register + PropertyMetadata →
		// Avalonia StyledProperty<T> + AvaloniaProperty.Register。Grid 类型来自 Avalonia.Controls。
		public static readonly StyledProperty<Grid> SearchPanelPlaceholderProperty =
			AvaloniaProperty.Register<CodeEditorSearchPanelUserControl, Grid>(nameof(SearchPanelPlaceholder));

		public Grid SearchPanelPlaceholder
		{
			get
			{
				return GetValue(SearchPanelPlaceholderProperty);
			}
			set
			{
				SetValue(SearchPanelPlaceholderProperty, value);
			}
		}

		public string SearchRequest => SearchTextBox.Text;

		public bool IsTextBoxFocused => SearchTextBox.IsFocused;

		public double PanelHeight
		{
			get
			{
				if (!_isSearchBarVisible)
				{
					return 0.0;
				}
				return ControlHeight;
			}
		}

		public CodeEditorSearchPanelUserControl()
		{
			InitializeComponent();
			SearchTextBox.FontFamily = FontConstants.ProportionalFontFamily;
			base.Loaded += delegate
			{
				TranslateTransform.Y = 0.0 - ControlHeight;
				SearchPanelPlaceholder.Height = 0.0;
			};
			// 阶段 4 里程碑 4.7-a：WPF PreviewKeyDown → Avalonia KeyDown（Avalonia 无 Preview 前缀）。
			// WPF KeyboardHelper.IsShiftDown → e.KeyModifiers.HasFlag(KeyModifiers.Shift)。
			SearchTextBox.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Return || e.Key == Key.F3)
				{
					e.Handled = true;
					if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
					{
						FindPrevious();
					}
					else
					{
						FindNext();
					}
				}
			};
		}

		public void Attach(TextArea textArea)
		{
			_textArea = textArea;
			_renderer = new SearchResultBackgroundRenderer();
			_textDocument = _textArea.Document;
			if (_textDocument != null)
			{
				_textDocument.TextChanged += TextAreaDocument_TextChanged;
			}
			_textArea.DocumentChanged += TextArea_DocumentChanged;
		}

		public void ShowSearchBar()
		{
			_textArea.TextView.BackgroundRenderers.Add(_renderer);
			if (SlidingPanelHelper.ShowPanel(SearchPanelPlaceholder, TranslateTransform, ControlHeight))
			{
				SearchTextBox.Clear();
			}
			_isSearchBarVisible = true;
			if (!_textArea.Selection.IsEmpty)
			{
				SearchTextBox.Text = _textArea.Selection.GetText();
			}
			SearchTextBox.SelectAll();
			SearchTextBox.Focus();
		}

		public void HideSearchBar()
		{
			if (_isSearchBarVisible)
			{
				SlidingPanelHelper.HidePanel(SearchPanelPlaceholder, TranslateTransform, ControlHeight);
				_textArea.TextView.BackgroundRenderers.Remove(_renderer);
				if (IsTextBoxFocused)
				{
					_textArea.Focus();
				}
				_isSearchBarVisible = false;
				_renderer.CurrentResults.Clear();
			}
		}

		public void FindNext()
		{
			SearchResult searchResult = _renderer.CurrentResults.FindFirstSegmentWithStartAfter(_textArea.Caret.Offset + 1);
			if (searchResult == null)
			{
				searchResult = _renderer.CurrentResults.FirstSegment;
			}
			if (searchResult != null)
			{
				SelectResult(searchResult);
			}
		}

		public void FindPrevious()
		{
			SearchResult searchResult = _renderer.CurrentResults.FindFirstSegmentWithStartAfter(_textArea.Caret.Offset);
			if (searchResult != null)
			{
				searchResult = _renderer.CurrentResults.GetPreviousSegment(searchResult);
			}
			if (searchResult == null)
			{
				searchResult = _renderer.CurrentResults.LastSegment;
			}
			if (searchResult != null)
			{
				SelectResult(searchResult);
			}
		}

		private void CloseSearchContainerButton_Click(object sender, RoutedEventArgs e)
		{
			HideSearchBar();
		}

		private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			DoSearch(changeSelection: true);
		}

		private void JumpToNextSearchResultButton_Click(object sender, RoutedEventArgs e)
		{
			FindNext();
		}

		private void JumpToPreviousSearchResultButton_Click(object sender, RoutedEventArgs e)
		{
			FindPrevious();
		}

		private void TextArea_DocumentChanged(object sender, EventArgs e)
		{
			if (_textDocument != null)
			{
				_textDocument.TextChanged -= TextAreaDocument_TextChanged;
			}
			_textDocument = _textArea.Document;
			if (_textDocument != null)
			{
				_textDocument.TextChanged += TextAreaDocument_TextChanged;
				DoSearch(changeSelection: false);
			}
		}

		private void TextAreaDocument_TextChanged(object sender, EventArgs e)
		{
			DoSearch(changeSelection: false);
		}

		private void DoSearch(bool changeSelection)
		{
			if (!_isSearchBarVisible)
			{
				return;
			}
			_renderer.CurrentResults.Clear();
			if (string.IsNullOrEmpty(SearchRequest))
			{
				_textArea.ClearSelection();
				_textArea.TextView.InvalidateLayer(KnownLayer.Selection);
				return;
			}
			int offset = _textArea.Caret.Offset;
			if (changeSelection)
			{
				_textArea.ClearSelection();
			}
			foreach (SearchResult item in FindAll(SearchRequest, _textDocument))
			{
				if (changeSelection && item.StartOffset >= offset)
				{
					SelectResult(item);
					changeSelection = false;
				}
				_renderer.CurrentResults.Add(item);
			}
			_textArea.TextView.InvalidateLayer(KnownLayer.Selection);
		}

		private static IEnumerable<SearchResult> FindAll(string searchString, TextDocument document)
		{
			string text = document.Text;
			for (int index = text.IndexOf(searchString, StringComparison.OrdinalIgnoreCase); index != -1; index = text.IndexOf(searchString, index + searchString.Length, StringComparison.OrdinalIgnoreCase))
			{
				yield return new SearchResult(index, searchString.Length);
			}
		}

		private void SelectResult(SearchResult result)
		{
			_textArea.Caret.Offset = result.StartOffset;
			_textArea.Selection = Selection.Create(_textArea, result.StartOffset, result.EndOffset);
			_textArea.Caret.BringCaretToView();
			_textArea.Caret.Show();
		}

	}
}
