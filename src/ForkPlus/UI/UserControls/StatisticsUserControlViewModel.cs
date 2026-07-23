using System;
using System.ComponentModel;
using System.Threading;
using ForkPlus.Git.Commands;
using OxyPlot;
using OxyPlot.Series;

namespace ForkPlus.UI.UserControls
{
	/// <summary>
	/// 阶段 3：StatisticsUserControl 的 ViewModel。
	/// 承载纯数据/纯逻辑（零 WPF 依赖）。View 保留 OxyPlot.Wpf 控件操作、事件处理，
	/// 以及依赖 Theme（WPF 类型）的 PlotHelper 工厂方法。
	/// 本 VM 只引用 OxyPlot 核心（平台无关），不引用 OxyPlot.Wpf 与 System.Windows.*。
	/// </summary>
	public class StatisticsUserControlViewModel
	{
		/// <summary>折线/柱状图调色板（原 View 第 258-273 行）。
		/// 原实现用 WPF ColorConverter.ConvertFromString + ToOxyColor()，此处改用
		/// OxyColor.Parse（OxyPlot 核心，平台无关）。颜色值与原实现逐字节等价：
		/// #RRGGBB→不透明（A=255），#AARRGGBB→带 alpha。</summary>
		private static readonly OxyColor[] _colors = new OxyColor[13]
		{
			OxyColor.Parse("#FF9502"),
			OxyColor.Parse("#64DA38"),
			OxyColor.Parse("#1CADF8"),
			OxyColor.Parse("#FF3B30"),
			OxyColor.Parse("#A2845E"),
			OxyColor.Parse("#CB73E1"),
			OxyColor.Parse("#FFCC00"),
			OxyColor.Parse("#8E8E91"),
			OxyColor.Parse("#FF2968"),
			OxyColor.Parse("#30D5C8"),
			OxyColor.Parse("#5856D6"),
			OxyColor.Parse("#B4D435"),
			OxyColor.Parse("#FF6F61")
		};

		/// <summary>饼图调色板（原 View 第 275-290 行），同上改用 OxyColor.Parse。</summary>
		private static readonly OxyColor[] _pieChartColors = new OxyColor[13]
		{
			OxyColor.Parse("#B3FF9502"),
			OxyColor.Parse("#B364DA38"),
			OxyColor.Parse("#B31CADF8"),
			OxyColor.Parse("#B3FF3B30"),
			OxyColor.Parse("#B3A2845E"),
			OxyColor.Parse("#B3CB73E1"),
			OxyColor.Parse("#B3FFCC00"),
			OxyColor.Parse("#B38E8E91"),
			OxyColor.Parse("#B3FF2968"),
			OxyColor.Parse("#B330D5C8"),
			OxyColor.Parse("#B35856D6"),
			OxyColor.Parse("#B3B4D435"),
			OxyColor.Parse("#B3FF6F61")
		};

		/// <summary>折线/柱状图调色板（供 View 的 PlotHelper 及 UpdateCodeLinesPlot 索引）。</summary>
		public static OxyColor[] Colors => _colors;

		/// <summary>饼图调色板（供 View 的 UpdateCodeLinesPlot 索引）。</summary>
		public static OxyColor[] PieChartColors => _pieChartColors;

		/// <summary>按 CurrentCulture 的 FirstDayOfWeek 排列的 7 个星期枚举（原 View 第 516-526 行）。</summary>
		public static DayOfWeek[] DaysOfWeek()
		{
			DayOfWeek[] array = new DayOfWeek[7];
			DayOfWeek dayOfWeek = Thread.CurrentThread.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
			for (int i = 0; i < 7; i++)
			{
				array[i] = dayOfWeek;
				dayOfWeek = ((dayOfWeek != DayOfWeek.Saturday) ? (dayOfWeek + 1) : DayOfWeek.Sunday);
			}
			return array;
		}

		/// <summary>OxyColor → "#RRGGBB" 十六进制字符串，给 XAML Rectangle.Fill 用（原 View 第 834-838 行）。</summary>
		public static string OxyColorToHex(OxyColor c)
		{
			return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
		}

		/// <summary>构造作者提交折线序列（原 View 第 538-551 行，原为 View 实例方法，此处提升为 VM 静态方法）。</summary>
		public static LineSeries CreateLineSeries(AuthorStats authorStat, int colorIndex)
		{
			return new LineSeries
			{
				Title = authorStat.Name,
				DataFieldX = "Item1",
				DataFieldY = "Item2",
				InterpolationAlgorithm = InterpolationAlgorithms.CatmullRomSpline,
				CanTrackerInterpolatePoints = false,
				TrackerFormatString = "{2},\n{0}: {4}",
				Color = _colors[colorIndex % _colors.Length],
				ItemsSource = authorStat.CommitsByDate
			};
		}

		/// <summary>构造作者提交占比饼图切片（原 View 第 553-559 行，原为 View 实例方法，此处提升为 VM 静态方法）。</summary>
		public static PieSlice CreatePieSlice(AuthorStats authorStat, int colorIndex)
		{
			return new PieSlice(authorStat.Name ?? "", authorStat.TotalCommits)
			{
				Fill = _pieChartColors[colorIndex % _colors.Length]
			};
		}
	}

	/// <summary>作者提交统计行 ViewModel（原 View 第 28-49 行嵌套类，提升为顶级类）。
	/// INPC 为退化实现（add/remove 空体），仅满足列表数据绑定对 INPC 的可选契约，保持与原行为一致。</summary>
	public class AuthorStatViewModel : INotifyPropertyChanged
	{
		public string Name { get; }

		public int TotalCommits { get; }

		public event PropertyChangedEventHandler PropertyChanged
		{
			add
			{
			}
			remove
			{
			}
		}

		public AuthorStatViewModel(string name, int totalCommits)
		{
			Name = name;
			TotalCommits = totalCommits;
		}
	}

	/// <summary>代码行数列表行 ViewModel。每行一个语言（原 View 第 52-71 行嵌套类，提升为顶级类）。</summary>
	public class CodeLineLanguageViewModel
	{
		public string Name { get; }
		public long Files { get; }
		public long Code { get; }
		public long Comments { get; }
		public long Blanks { get; }
		/// <summary>饼图色块颜色，XAML 里 Rectangle.Fill 绑定。</summary>
		public string Color { get; }

		public CodeLineLanguageViewModel(string name, long files, long code, long comments, long blanks, string color)
		{
			Name = name;
			Files = files;
			Code = code;
			Comments = comments;
			Blanks = blanks;
			Color = color;
		}
	}
}
