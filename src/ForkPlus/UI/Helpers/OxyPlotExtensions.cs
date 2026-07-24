using Avalonia.Media;
using OxyPlot;

namespace ForkPlus.UI.Helpers
{
	/// <summary>
	/// 阶段 4 里程碑 4.7-b：OxyPlot.Avalonia 迁移辅助扩展。
	/// OxyPlot.Wpf 的 BrushExtensions.ToOxyColor() 在 OxyPlot.Avalonia 中无等价物，
	/// 此处提供 IBrush → OxyColor 的转换（仅支持 SolidColorBrush；渐变画刷取第一个 GradientStop）。
	/// </summary>
	public static class OxyPlotExtensions
	{
		public static OxyColor ToOxyColor(this IBrush brush)
		{
			if (brush is ISolidColorBrush solid)
			{
				return OxyColor.FromArgb(solid.Color.A, solid.Color.R, solid.Color.G, solid.Color.B);
			}
			if (brush is IGradientBrush gradient)
			{
				var stops = gradient.GradientStops;
				if (stops != null && stops.Count > 0)
				{
					var c = stops[0].Color;
					return OxyColor.FromArgb(c.A, c.R, c.G, c.B);
				}
			}
			return OxyColors.Transparent;
		}
	}
}
