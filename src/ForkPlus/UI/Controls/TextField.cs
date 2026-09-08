using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class TextField : TextBlock
	{
		protected static readonly List<Range> Empty = new List<Range>();

		public TextField()
		{
			// Migration note：WPF 版 DependencyProperty.Register 带 PropertyChangedCallback → RefreshInlines，
			// 转换丢了回调 → Inlines 永不填充 → TextBlock 空白（提交主题不渲染，迁移期实证）。
			// GetObservable 订阅时立即触发一次 + 绑定赋值后再触发，补回整条触发链。
			this.GetObservable(StringValueProperty).Subscribe(new global::Avalonia.Reactive.AnonymousObserver<string>(delegate
			{
				RefreshInlines();
			}));
			this.GetObservable(HighlightStringProperty).Subscribe(new global::Avalonia.Reactive.AnonymousObserver<string>(delegate
			{
				RefreshInlines();
			}));
		}

		// Migration note：WPF DependencyProperty → Avalonia StyledProperty。
		// 原转换用 RegisterAttached<..., AvaloniaObject, ...>（附加属性形式），XAML 属性元素语法
		// <controls:TextField.StringValue><Binding/></...> 无法解析，改为普通 Register。
		public static readonly global::Avalonia.StyledProperty<string> StringValueProperty =
    global::Avalonia.AvaloniaProperty.Register<TextField, string>("StringValue");

		public static readonly global::Avalonia.StyledProperty<string> HighlightStringProperty =
    global::Avalonia.AvaloniaProperty.Register<TextField, string>("HighlightString");

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
				return GetValue(HighlightStringProperty);
			}
			set
			{
				SetValue(HighlightStringProperty, value);
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
			Brush matchForegroundBrush = global::ForkPlus.UI.Theme.FindBrush("ForegroundBrush");
			Brush matchBackgroundBrush = global::ForkPlus.UI.Theme.FindBrush("RevisionList.SearchMatch.ForegroundBrush");
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
