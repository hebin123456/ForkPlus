using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ForkPlus.UI.Controls
{
	public class TextField : TextBlock
	{
		protected static readonly List<Range> Empty = new List<Range>();

		public static readonly DependencyProperty StringValueProperty = DependencyProperty.RegisterAttached("StringValue", typeof(string), typeof(TextField), new PropertyMetadata(delegate(DependencyObject s, DependencyPropertyChangedEventArgs e)
		{
			(s as TextField).RefreshInlines();
		}));

		public static readonly DependencyProperty HighlightPatternProperty = DependencyProperty.RegisterAttached("HighlightString", typeof(string), typeof(TextField), new PropertyMetadata(delegate(DependencyObject s, DependencyPropertyChangedEventArgs e)
		{
			(s as TextField).RefreshInlines();
		}));

		public string StringValue
		{
			get
			{
				return (string)GetValue(StringValueProperty);
			}
			set
			{
				SetValue(StringValueProperty, value);
			}
		}

		public string HighlightString
		{
			get
			{
				return (string)GetValue(HighlightPatternProperty);
			}
			set
			{
				SetValue(HighlightPatternProperty, value);
			}
		}

		protected virtual void RefreshInlines()
		{
			base.Inlines.Clear();
			string stringValue = StringValue;
			string highlightString = HighlightString;
			if (string.IsNullOrEmpty(stringValue))
			{
				return;
			}
			if (string.IsNullOrEmpty(highlightString))
			{
				base.Inlines.Add(new Run(stringValue));
				return;
			}
			List<Range> searchMatchRanges = GetSearchMatchRanges(stringValue, highlightString);
			if (searchMatchRanges.Count == 0)
			{
				base.Inlines.Add(new Run(stringValue));
				return;
			}
			Brush matchForegroundBrush = Theme.FindBrush("ForegroundBrush");
			Brush matchBackgroundBrush = Theme.FindBrush("RevisionList.SearchMatch.ForegroundBrush");
			new Range(0, stringValue.Length).Merge(new List<Range>[1] { searchMatchRanges }, delegate(Range range, int? searchIndex, int? _, int? __)
			{
				Run run = new Run(stringValue.Substring(range));
				if (searchIndex.HasValue)
				{
					run.Background = matchBackgroundBrush;
					run.Foreground = matchForegroundBrush;
				}
				base.Inlines.Add(run);
			});
		}

		protected static List<Range> GetSearchMatchRanges(string stringValue, [Null] string highlightString)
		{
			if (string.IsNullOrEmpty(highlightString))
			{
				return Empty;
			}
			int num = stringValue.IndexOf(highlightString, StringComparison.OrdinalIgnoreCase);
			if (num == -1)
			{
				return Empty;
			}
			List<Range> list = new List<Range>();
			int length = highlightString.Length;
			while (num != -1)
			{
				int num2 = num + length;
				list.Add(new Range(num, num2));
				num = stringValue.IndexOf(highlightString, num2, StringComparison.OrdinalIgnoreCase);
			}
			return list;
		}
	}
}
