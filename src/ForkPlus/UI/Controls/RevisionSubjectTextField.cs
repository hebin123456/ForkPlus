using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace ForkPlus.UI.Controls
{
	public class RevisionSubjectTextField : TextField
	{
		public static readonly StyledProperty<bool> IsParentSelectedProperty =
			AvaloniaProperty.Register<RevisionSubjectTextField, bool>(nameof(IsParentSelected));

		public static readonly StyledProperty<bool> HasBodyProperty =
			AvaloniaProperty.Register<RevisionSubjectTextField, bool>(nameof(HasBody));

		public bool IsParentSelected
		{
			get => GetValue(IsParentSelectedProperty);
			set => SetValue(IsParentSelectedProperty, value);
		}

		public bool HasBody
		{
			get => GetValue(HasBodyProperty);
			set => SetValue(HasBodyProperty, value);
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == IsParentSelectedProperty || change.Property == HasBodyProperty)
			{
				RefreshInlines();
			}
		}

		protected override void RefreshInlines()
		{
			string stringValue = base.StringValue;
			string highlightString = base.HighlightString;
			base.Inlines!.Clear();
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
			// TODO(4.5-g): Avalonia Run 不支持 FontWeight/FontFamily/FontSize/Background/Foreground 属性。
			// 前缀粗体、代码块着色、搜索匹配高亮待后续自定义 TextBlock 渲染恢复。
			new Range(0, stringValue.Length).Merge(new List<Range>[3] { prefixHighlighting, codeHighlighting, searchMatchRanges }, delegate(Range range, int? prefixIndex, int? codeIndex, int? searchIndex)
			{
				Run run2 = new Run(stringValue.Substring(range));
				base.Inlines.Add(run2);
			});
			if (HasBody)
			{
				base.Inlines.Add(new Run(" ↩"));
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
