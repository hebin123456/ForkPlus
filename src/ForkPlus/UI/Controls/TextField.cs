using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace ForkPlus.UI.Controls
{
	public class TextField : TextBlock
	{
		protected static readonly List<Range> Empty = new List<Range>();

		public static readonly StyledProperty<string> StringValueProperty =
			AvaloniaProperty.Register<TextField, string>(nameof(StringValue));

		public static readonly StyledProperty<string> HighlightStringProperty =
			AvaloniaProperty.Register<TextField, string>(nameof(HighlightString));

		public string StringValue
		{
			get => GetValue(StringValueProperty);
			set => SetValue(StringValueProperty, value);
		}

		public string HighlightString
		{
			get => GetValue(HighlightStringProperty);
			set => SetValue(HighlightStringProperty, value);
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == StringValueProperty || change.Property == HighlightStringProperty)
			{
				RefreshInlines();
			}
		}

		protected virtual void RefreshInlines()
		{
			base.Inlines!.Clear();
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
			// TODO(4.5-g): Avalonia Run 不支持 Background/Foreground 属性（WPF 才有）。
			// 搜索匹配高亮着色需后续改用自定义 TextBlock 渲染（FormattedText + DrawText 范围着色）恢复。
			new Range(0, stringValue.Length).Merge(new List<Range>[1] { searchMatchRanges }, delegate(Range range, int? searchIndex, int? _, int? __)
			{
				Run run = new Run(stringValue.Substring(range));
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
