using System;
using System.Globalization;
using Avalonia.Data.Converters;
using ForkPlus.Git;

// Avalonia spike 版 UpstreamStatusToImageConverter（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/UpstreamStatusToImageConverter.cs（46 行）：
//   - WPF: public class UpstreamStatusToImageConverter : MarkupExtension, IValueConverter
//   - Convert(value, ...)：value 为 UpstreamStatus
//     IsValid → Theme.BranchIcon / BranchSelectedIcon（按 parameter=="true" 选 selected）
//     !IsValid → Theme.BranchWarningIcon / BranchWarningSelectedIcon
//     非 UpstreamStatus → Theme.BranchPaleIcon / BranchPaleSelectedIcon
//   - 返回 ImageSource（图片资源）
//   - ProvideValue 返回 this（MarkupExtension 模式）
//
// Avalonia 版差异（spike 简化策略，task spec：IValueConverter + emoji 替代图片）：
//   1. WPF MarkupExtension + IValueConverter → Avalonia.Data.Converters.IValueConverter
//      （Avalonia 在 axaml 用 {x:Static ...} 引用静态 Instance，无需 MarkupExtension）
//   2. WPF 返回 Theme.BranchIcon 等图片资源 → spike 用 emoji 字符串替代：
//        Ahead(>0 && behind==0) = "⬆"  Behind(>0 && ahead==0) = "⬇"
//        InSync(==0 && ==0)      = "✓"  Diverged(>0 && >0)     = "⇅"
//   3. WPF 按 parameter=="true" 区分 selected 图标 → spike 跳过（emoji 无 selected 变体）
//   4. WPF !IsValid / 非 UpstreamStatus 返回 pale 图标 → spike 返回空串
//   5. spike 提供 Instance 静态单例（Avalonia 转换器常用模式）
//
// spike 简化（task spec 关键 API）：
//   - 实现 Avalonia.Data.Converters.IValueConverter
//   - Convert：UpstreamStatus → emoji（⬆/⬇/✓/⇅）
//   - ConvertBack：NotImplementedException（与 WPF 一致）
//   - Instance 静态单例
namespace ForkPlus.Avalonia
{
	public class UpstreamStatusToImageConverter : IValueConverter
	{
		public static readonly UpstreamStatusToImageConverter Instance = new UpstreamStatusToImageConverter();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			// spike 版：emoji 替代图片（task spec）
			//   Ahead="⬆" / Behind="⬇" / InSync="✓" / Diverged="⇅"
			if (value is UpstreamStatus upstreamStatus)
			{
				if (!upstreamStatus.IsValid)
				{
					return "";
				}
				bool ahead = upstreamStatus.Ahead > 0;
				bool behind = upstreamStatus.Behind > 0;
				if (ahead && behind)
				{
					return "⇅"; // Diverged
				}
				if (ahead)
				{
					return "⬆"; // Ahead
				}
				if (behind)
				{
					return "⬇"; // Behind
				}
				return "✓"; // InSync
			}
			return "";
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
