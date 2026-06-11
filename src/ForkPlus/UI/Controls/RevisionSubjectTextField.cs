using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ForkPlus.UI.Controls
{
	public class RevisionSubjectTextField : TextField
	{
		public static readonly DependencyProperty IsParentSelectedProperty = DependencyProperty.RegisterAttached("IsParentSelected", typeof(bool), typeof(RevisionSubjectTextField), new PropertyMetadata(delegate(DependencyObject s, DependencyPropertyChangedEventArgs e)
		{
			(s as RevisionSubjectTextField).RefreshInlines();
		}));

		public static readonly DependencyProperty HasBodyProperty = DependencyProperty.RegisterAttached("HasBody", typeof(bool), typeof(RevisionSubjectTextField));

		public bool IsParentSelected
		{
			get
			{
				return (bool)GetValue(IsParentSelectedProperty);
			}
			set
			{
				SetValue(IsParentSelectedProperty, value);
			}
		}

		public bool HasBody
		{
			get
			{
				return (bool)GetValue(HasBodyProperty);
			}
			set
			{
				SetValue(HasBodyProperty, value);
			}
		}

		protected override void RefreshInlines()
		{
			string stringValue = base.StringValue;
			string highlightString = base.HighlightString;
			base.Inlines.Clear();
			if (string.IsNullOrEmpty(stringValue))
			{
				return;
			}
			List<Range> prefixHighlighting = GetPrefixHighlighting(stringValue);
			List<Range> codeHighlighting = GetCodeHighlighting(stringValue);
			List<Range> searchMatchRanges = TextField.GetSearchMatchRanges(stringValue, highlightString);
			if (prefixHighlighting.Count == 0 && codeHighlighting.Count == 0 && searchMatchRanges.Count == 0 && !HasBody)
			{
				base.Inlines.Add(new Run(stringValue));
				return;
			}
			Brush matchForegroundBrush = Theme.FindBrush("ForegroundBrush");
			Brush matchBackgroundBrush = Theme.FindBrush("RevisionList.SearchMatch.ForegroundBrush");
			Brush codeSolidBackgroundBrush = Theme.FindBrush("RevisionList.Code.BackgroundBrush");
			Brush codeTransparentBackgroundBrush = Theme.FindBrush("RevisionList.Code.Selected.BackgroundBrush");
			new Range(0, stringValue.Length).Merge(new List<Range>[3] { prefixHighlighting, codeHighlighting, searchMatchRanges }, delegate(Range range, int? prefixIndex, int? codeIndex, int? searchIndex)
			{
				Run run2 = new Run(stringValue.Substring(range));
				if (prefixIndex.HasValue)
				{
					run2.FontWeight = FontWeights.SemiBold;
				}
				if (codeIndex.HasValue)
				{
					run2.FontFamily = FontConstants.MonospaceFontFamily;
					run2.FontSize = 11.0;
					run2.Background = (IsParentSelected ? codeTransparentBackgroundBrush : codeSolidBackgroundBrush);
				}
				if (searchIndex.HasValue)
				{
					run2.Background = matchBackgroundBrush;
					run2.Foreground = matchForegroundBrush;
				}
				base.Inlines.Add(run2);
			});
			if (HasBody)
			{
				Run run = new Run(" ↩");
				run.FontSize = 10.0;
				run.Foreground = (IsParentSelected ? Theme.FindBrush("RevisionList.BodyIndicator.Selected.ForegroundBrush") : Theme.FindBrush("RevisionList.BodyIndicator.ForegroundBrush"));
				base.Inlines.Add(run);
			}
		}

		private static List<Range> GetPrefixHighlighting(string stringValue)
		{
			if (stringValue.StartsWith("["))
			{
				int num = stringValue.IndexOf(']');
				if (num != -1)
				{
					return new List<Range>
					{
						new Range(0, num + 1)
					};
				}
			}
			int num2 = stringValue.IndexOf(' ');
			if (num2 == -1 || num2 == 0)
			{
				return TextField.Empty;
			}
			int num3 = stringValue.IndexOf(':', 1, num2);
			if (num3 == -1)
			{
				return TextField.Empty;
			}
			return new List<Range>
			{
				new Range(0, num3)
			};
		}

		private static List<Range> GetCodeHighlighting(string stringValue)
		{
			Range? range = FindCodeBlock(stringValue, 0);
			if (range.HasValue)
			{
				Range valueOrDefault = range.GetValueOrDefault();
				List<Range> list = new List<Range>();
				list.Add(valueOrDefault);
				int num = valueOrDefault.End + 1;
				while (num < stringValue.Length)
				{
					range = FindCodeBlock(stringValue, num);
					if (!range.HasValue)
					{
						break;
					}
					Range valueOrDefault2 = range.GetValueOrDefault();
					list.Add(valueOrDefault2);
					num = valueOrDefault2.End + 1;
				}
				return list;
			}
			return TextField.Empty;
		}

		private static Range? FindCodeBlock(string str, int start)
		{
			int num = str.IndexOf('`', start);
			if (num == -1)
			{
				return null;
			}
			int num2 = str.IndexOf('`', num + 1);
			if (num2 == -1)
			{
				return null;
			}
			return new Range(num, num2 + 1);
		}
	}
}
