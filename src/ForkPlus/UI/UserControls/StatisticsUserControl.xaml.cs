using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using OxyPlot.Wpf;

namespace ForkPlus.UI.UserControls
{
	public partial class StatisticsUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
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

		/// <summary>代码行数列表行 ViewModel。每行一个语言。</summary>
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

		public static class PlotHelper
		{
			public static PlotModel CreateLinePlotModel()
			{
				PlotModel obj = new PlotModel
				{
					Legends = { (LegendBase)new Legend
					{
						LegendPosition = LegendPosition.BottomLeft,
						LegendOrientation = LegendOrientation.Vertical,
						LegendPlacement = LegendPlacement.Outside,
						LegendMaxHeight = 70.0,
						LegendSymbolMargin = 8.0,
						LegendColumnSpacing = 20.0
					} },
					Axes = 
					{
						(Axis)new LinearAxis
						{
							MajorStep = 50.0,
							Minimum = 0.0,
							IsZoomEnabled = false,
							TickStyle = TickStyle.None,
							MajorGridlineStyle = LineStyle.Solid,
							MinorGridlineStyle = LineStyle.Dot
						},
						(Axis)new DateTimeAxis
						{
							Angle = -45.0,
							StringFormat = "MMM yyyy",
							IsZoomEnabled = false,
							TickStyle = TickStyle.None,
							MinorIntervalType = DateTimeIntervalType.Months,
							IntervalType = DateTimeIntervalType.Months,
							MajorGridlineStyle = LineStyle.Dot
						}
					}
				};
				RefreshPlotColors(obj);
				return obj;
			}

			public static PlotModel CreatePiePlotModel()
			{
				PlotModel obj = new PlotModel
				{
					Padding = new OxyThickness(40.0, 30.0, 40.0, 30.0),
					PlotMargins = new OxyThickness(0.0, 0.0, 0.0, 0.0),
					Series = { (Series)new PieSeries
					{
						InnerDiameter = 0.3,
						ExplodedDistance = 0.0,
						StrokeThickness = 0.0,
						StartAngle = 0.0,
						AngleSpan = 360.0,
						AreInsideLabelsAngled = true,
						TickLabelDistance = 0.0,
						TickDistance = 8.0,
						TickHorizontalLength = 0.0,
						TickRadialLength = 0.0,
						ColorField = "Fill",
						LabelField = "Label",
						ValueField = "Value",
						IsExplodedField = "IsExploded"
					} }
				};
				RefreshPlotColors(obj);
				return obj;
			}

			public static PlotModel CreateWeekDayPlotModel(DayOfWeek[] daysOfWeek)
			{
				PlotModel obj = new PlotModel
				{
					Padding = new OxyThickness(40.0, 0.0, 0.0, 40.0),
					PlotMargins = new OxyThickness(0.0, 10.0, 0.0, 0.0),
					Axes = 
					{
						(Axis)new LinearAxis
						{
							Key = "Value",
							Position = AxisPosition.Left,
							Minimum = 0.0,
							IsZoomEnabled = false,
							TickStyle = TickStyle.None,
							MajorGridlineStyle = LineStyle.Solid,
							MinorGridlineStyle = LineStyle.Dot
						},
						(Axis)new CategoryAxis
						{
							Key = "Category",
							Position = AxisPosition.Bottom,
							IsZoomEnabled = false,
							TickStyle = TickStyle.None,
							MajorGridlineStyle = LineStyle.Solid,
							ItemsSource = daysOfWeek.Map((DayOfWeek x) => Translate(x.ToString().Substring(0, 3)))
						}
					},
					Series = { (Series)new BarSeries
					{
						XAxisKey = "Value",
						YAxisKey = "Category",
						StrokeThickness = 0.0,
						FillColor = _colors[2]
					} }
				};
				RefreshPlotColors(obj);
				return obj;
			}

			public static PlotModel CreateDayHourPlotModel()
			{
				PlotModel plotModel = new PlotModel
				{
					Padding = new OxyThickness(40.0, 0.0, 0.0, 40.0),
					PlotMargins = new OxyThickness(0.0, 10.0, 0.0, 0.0)
				};
				plotModel.Axes.Add(new LinearAxis
				{
					Key = "Value",
					Position = AxisPosition.Left,
					Minimum = -1.0,
					IsZoomEnabled = false,
					TickStyle = TickStyle.None,
					MajorGridlineStyle = LineStyle.Solid,
					MinorGridlineStyle = LineStyle.Dot
				});
				plotModel.Axes.Add(new CategoryAxis
				{
					Key = "Category",
					Position = AxisPosition.Bottom,
					IsZoomEnabled = false,
					TickStyle = TickStyle.None,
					MajorGridlineStyle = LineStyle.Solid,
					ItemsSource = new string[24]
					{
						"0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
						"10", "11", "12", "13", "14", "15", "16", "17", "18", "19",
						"20", "21", "22", "23"
					}
				});
				plotModel.Series.Add(new BarSeries
				{
					XAxisKey = "Value",
					YAxisKey = "Category",
					StrokeThickness = 0.0,
					FillColor = _colors[2]
				});
				RefreshPlotColors(plotModel);
				return plotModel;
			}

			public static void RefreshPlotColors(PlotModel plot)
			{
				plot.Background = BackgroundColor;
				plot.PlotAreaBorderColor = BorderColor;
				plot.PlotAreaBackground = BackgroundColor;
				if (plot.Legends.FirstItem((LegendBase x) => x is Legend) is Legend)
				{
					plot.Legends[0].LegendTextColor = SecondaryLabelColor;
				}
				if (plot.Axes.FirstItem((Axis x) => x is LinearAxis) is LinearAxis linearAxis)
				{
					linearAxis.MajorGridlineColor = BorderColor;
					linearAxis.MinorGridlineColor = BorderColor;
					linearAxis.TextColor = SecondaryLabelColor;
				}
				if (plot.Axes.FirstItem((Axis x) => x is DateTimeAxis) is DateTimeAxis dateTimeAxis)
				{
					dateTimeAxis.MajorGridlineColor = BorderColor;
					dateTimeAxis.TextColor = SecondaryLabelColor;
				}
				if (plot.Axes.FirstItem((Axis x) => x is CategoryAxis) is CategoryAxis categoryAxis)
				{
					categoryAxis.MajorGridlineColor = BorderColor;
					categoryAxis.TextColor = SecondaryLabelColor;
				}
				if (plot.Series.FirstItem((Series x) => x is PieSeries) is PieSeries pieSeries)
				{
					pieSeries.InsideLabelColor = LabelColor;
					pieSeries.TextColor = SecondaryLabelColor;
				}
			}
		}

		private static readonly OxyColor[] _colors = new OxyColor[13]
		{
			((Color)ColorConverter.ConvertFromString("#FF9502")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#64DA38")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#1CADF8")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#FF3B30")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#A2845E")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#CB73E1")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#FFCC00")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#8E8E91")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#FF2968")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#30D5C8")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#5856D6")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#B4D435")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#FF6F61")).ToOxyColor()
		};

		private static readonly OxyColor[] _pieChartColors = new OxyColor[13]
		{
			((Color)ColorConverter.ConvertFromString("#B3FF9502")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#B364DA38")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#B31CADF8")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#B3FF3B30")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#B3A2845E")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#B3CB73E1")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#B3FFCC00")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#B38E8E91")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#B3FF2968")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#B330D5C8")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#B35856D6")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#B3B4D435")).ToOxyColor(),
			((Color)ColorConverter.ConvertFromString("#B3FF6F61")).ToOxyColor()
		};

		[Null]
		private GitModule _gitModule;

		[Null]
		private RepositoryStats _stats;

		private PlotModel _linePlotModel;

		private PlotModel _piePlotModel;

		private PlotModel _weekDayPlotModel;

		private PlotModel _dayHourPlotModel;

		/// <summary>代码行数饼图的 PlotModel（按语言代码行数占比）。</summary>
		private PlotModel _codeLinesPieModel;

		/// <summary>代码行数后台任务队列。串行化多次 refresh，避免切换 ref 时并发 spawn tokei。</summary>
		private readonly JobQueue _codeLinesJobQueue = new JobQueue();

		/// <summary>当前代码行数查询选中的 refSpec。null/空 = 工作区 snapshot。</summary>
		[Null]
		private string _currentCodeLinesRef;

		/// <summary>防止 CodeLinesRefComboBox 初始化 SelectionChanged 事件触发查询。</summary>
		private bool _isCodeLinesRefComboBoxInitializing;

		private bool _isCalendarUpdatingInProgress;

		private static OxyColor BorderColor => Theme.BorderBrush.ToOxyColor();

		private static OxyColor BackgroundColor => Theme.BackgroundBrush.ToOxyColor();

		private static OxyColor SecondaryLabelColor => Theme.SecondaryLabelBrush.ToOxyColor();

		private static OxyColor LabelColor => Theme.LabelBrush.ToOxyColor();

		public StatisticsUserControl()
		{
			InitializeComponent();
			ApplyLocalization();
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
			_linePlotModel = PlotHelper.CreateLinePlotModel();
			LinePlot.Model = _linePlotModel;
			_piePlotModel = PlotHelper.CreatePiePlotModel();
			PiePlot.Model = _piePlotModel;
			_weekDayPlotModel = PlotHelper.CreateWeekDayPlotModel(DaysOfWeek());
			WeekDayPlot.Model = _weekDayPlotModel;
			_dayHourPlotModel = PlotHelper.CreateDayHourPlotModel();
			DayHourPlot.Model = _dayHourPlotModel;
			// 代码行数饼图复用 CreatePiePlotModel（同样的 PieSeries 配置）
			_codeLinesPieModel = PlotHelper.CreatePiePlotModel();
			CodeLinesPiePlot.Model = _codeLinesPieModel;
			DateRangeButton.DateRangeChanged += delegate
			{
				if (!_isCalendarUpdatingInProgress)
				{
				UpdatePreview(_gitModule, DateRangeButton.DateRange.ToServiceCalendarDateRange());
				}
			};
			StatsContainer.Collapse();
			FallbackUserControl.FallbackTitle = Translate("Generating statistics...");
			FallbackUserControl.Show();
		}

		public void ApplyLocalization()
		{
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			FallbackUserControl.FallbackTitle = Translate(FallbackUserControl.FallbackTitle);
		}

		public void ShowStatistics(GitModule gitModule)
		{
			_gitModule = gitModule;
			UpdatePreview(gitModule, null);
			// 初始化代码行数 ref 下拉并触发首次 snapshot 查询
			InitializeCodeLinesRefComboBox(gitModule);
			RefreshCodeLines(null);
		}

		private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
		{
			if (_stats != null)
			{
				PlotHelper.RefreshPlotColors(_linePlotModel);
				PlotHelper.RefreshPlotColors(_piePlotModel);
				PlotHelper.RefreshPlotColors(_weekDayPlotModel);
				PlotHelper.RefreshPlotColors(_dayHourPlotModel);
			}
		}

private void UpdatePreview(GitModule gitModule, [Null] ForkPlus.Services.CalendarDateRange? dateRange)
		{
			StatsContainer.Collapse();
			FallbackUserControl.FallbackTitle = Translate("Generating statistics...");
			FallbackUserControl.Show();
			if (_gitModule != gitModule)
			{
				return;
			}
			new Task(delegate
			{
				GitCommandResult<RepositoryStats> statsResponse = new GetRepositoryStatsGitCommand().Execute(gitModule, dateRange);
				base.Dispatcher.Async(delegate
				{
					if (_gitModule == gitModule)
					{
						if (!statsResponse.Succeeded)
						{
							FallbackUserControl.FallbackTitle = Translate("Unable to generate statistics");
							FallbackUserControl.Show();
						}
						else
						{
							FallbackUserControl.Hide();
							StatsContainer.Show();
							_stats = statsResponse.Result;
							_isCalendarUpdatingInProgress = true;
							if (dateRange == null)
							{
								DateRangeButton.MinDate = _stats.Start;
								DateRangeButton.MaxDate = _stats.End;
							}
							DateRangeButton.DateRange = new CalendarDateRange(_stats.Start, _stats.End);
							_isCalendarUpdatingInProgress = false;
							UpdatePlots(_stats);
						}
					}
				});
			}).Start();
		}

		private void UpdatePlots(RepositoryStats stat)
		{
			AuthorStatListBox.ItemsSource = null;
			_linePlotModel.Series.Clear();
			(_piePlotModel.Series[0] as PieSeries).Slices.Clear();
			(_weekDayPlotModel.Series[0] as BarSeries).Items.Clear();
			(_dayHourPlotModel.Series[0] as BarSeries).Items.Clear();
			AuthorStats[] authorStat = stat.AuthorStat;
			for (int i = 0; i < authorStat.Length && i < 20; i++)
			{
				AuthorStats authorStat2 = authorStat[i];
				_linePlotModel.Series.Add(CreateLineSeries(authorStat2, i));
				(_piePlotModel.Series[0] as PieSeries).Slices.Add(CreatePieSlice(authorStat2, i));
			}
			AuthorStatListBox.ItemsSource = authorStat.Map((AuthorStats x) => new AuthorStatViewModel(x.Name, x.TotalCommits));
			LinePlot.InvalidatePlot();
			PiePlot.InvalidatePlot();
			UpdateCommitsPerWeekDayPlot(stat.CommitsByDayOfWeek);
			UpdateCommitsPerDayHourPlot(stat.CommitsByHourOfDay);
			Heatmap.CommitsByDate = stat.CommitsByDate;
		}

		private void UpdateCommitsPerWeekDayPlot(Dictionary<DayOfWeek, int> commitsPerWeekDay)
		{
			DayOfWeek[] array = DaysOfWeek();
			for (int i = 0; i < 7; i++)
			{
				int num = (commitsPerWeekDay.ContainsKey(array[i]) ? commitsPerWeekDay[array[i]] : 0);
				(_weekDayPlotModel.Series[0] as BarSeries).Items.Add(new BarItem(num, i));
			}
			WeekDayPlot.InvalidatePlot();
		}

		private static DayOfWeek[] DaysOfWeek()
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

		private void UpdateCommitsPerDayHourPlot(Dictionary<int, int> commitsPerDayHour)
		{
			for (int i = 0; i < 24; i++)
			{
				int num = (commitsPerDayHour.ContainsKey(i) ? commitsPerDayHour[i] : 0);
				(_dayHourPlotModel.Series[0] as BarSeries).Items.Add(new BarItem(num, i));
			}
			DayHourPlot.InvalidatePlot();
		}

		private LineSeries CreateLineSeries(AuthorStats authorStat, int colorIndex)
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

		private PieSlice CreatePieSlice(AuthorStats authorStat, int colorIndex)
		{
			return new PieSlice(authorStat.Name ?? "", authorStat.TotalCommits)
			{
				Fill = _pieChartColors[colorIndex % _colors.Length]
			};
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

		// ===================== 代码行数统计（tokei）=====================

		/// <summary>初始化 ref 下拉：Workspace（工作区）+ 本地分支 + tag。
		/// 走 git for-each-ref 一次性拿全，避免阻塞 UI。</summary>
		private void InitializeCodeLinesRefComboBox(GitModule gitModule)
		{
			_isCodeLinesRefComboBoxInitializing = true;
			CodeLinesRefComboBox.Items.Clear();
			// 第一项固定为"Workspace"（snapshot 模式）
			CodeLinesRefComboBox.Items.Add(new CodeLineRefItem(Translate("Workspace"), null));
			try
			{
				// 列本地分支和 tag。轻量命令，同步执行可接受（< 100ms 通常）
				var result = new ForkPlus.Git.Interaction.GitRequest(gitModule)
					.Command("for-each-ref", "--format=%(refname:short)", "refs/heads/", "refs/tags/")
					.Execute(silent: true);
				if (result.Success && !string.IsNullOrEmpty(result.Stdout))
				{
					string[] refs = result.Stdout.Split(Consts.Chars.NewLine);
					foreach (string r in refs)
					{
						string trimmed = r.Trim();
						if (!string.IsNullOrEmpty(trimmed))
						{
							CodeLinesRefComboBox.Items.Add(new CodeLineRefItem(trimmed, trimmed));
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to list refs for CodeLines combo", ex);
			}
			CodeLinesRefComboBox.SelectedIndex = 0;
			_isCodeLinesRefComboBoxInitializing = false;
		}

		/// <summary>ComboBox 选 ref 时触发。重新跑 tokei。</summary>
		private void CodeLinesRefComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_isCodeLinesRefComboBoxInitializing || _gitModule == null)
			{
				return;
			}
			CodeLineRefItem item = CodeLinesRefComboBox.SelectedItem as CodeLineRefItem;
			RefreshCodeLines(item?.RefSpec);
		}

		/// <summary>Refresh 按钮点击。用当前选中的 ref 重跑。</summary>
		private void CodeLinesRefreshButton_Click(object sender, RoutedEventArgs e)
		{
			CodeLineRefItem item = CodeLinesRefComboBox.SelectedItem as CodeLineRefItem;
			RefreshCodeLines(item?.RefSpec);
		}

		/// <summary>异步跑 tokei 拿代码行数统计，更新饼图 + 列表 + 摘要。
		/// 走 JobQueue 串行化，避免切换 ref 时并发 spawn。</summary>
		private void RefreshCodeLines([Null] string refSpec)
		{
			if (_gitModule == null)
			{
				return;
			}
			_currentCodeLinesRef = refSpec;
			// 立刻显示"加载中"，避免用户以为按钮没响应
			CodeLinesError.Visibility = Visibility.Collapsed;
			CodeLinesSummary.Text = Translate("Counting code lines...") + (string.IsNullOrEmpty(refSpec) ? "" : " (" + refSpec + ")");
			CodeLinesRefreshButton.IsEnabled = false;
			CodeLinesRefComboBox.IsEnabled = false;

			// 复制一份避免闭包捕获到后续变化的 refSpec
			string refSpecCopy = refSpec;
			GitModule gitModule = _gitModule;
			_codeLinesJobQueue.Add("CodeLinesStats", delegate (JobMonitor monitor)
			{
				var result = new GetCodeLineStatsGitCommand().Execute(gitModule, refSpecCopy, monitor);
				Dispatcher.Async(delegate
				{
					// 用户可能在此期间又触发了新查询，校验是否还是当前选中的 ref
					if (refSpecCopy != _currentCodeLinesRef)
					{
						return;
					}
					CodeLinesRefreshButton.IsEnabled = true;
					CodeLinesRefComboBox.IsEnabled = true;
					if (!result.Succeeded)
					{
						ShowCodeLinesError(result.Error?.FriendlyDescription ?? "Failed");
						return;
					}
					UpdateCodeLinesPlot(result.Result);
				});
			}, JobFlags.LongRunning, showMessageWhenDone: false);
		}

		/// <summary>把 CodeLineStats 渲染到饼图 + 列表 + 摘要。</summary>
		private void UpdateCodeLinesPlot(CodeLineStats stats)
		{
			CodeLinesError.Visibility = Visibility.Collapsed;
			// 饼图
			var pieSeries = _codeLinesPieModel.Series[0] as PieSeries;
			pieSeries.Slices.Clear();
			var listItems = new List<CodeLineLanguageViewModel>(stats.Languages.Length);
			// 饼图最多显示 Top 12 语言（颜色有限），其余合并为 "Other"
			long otherCode = 0;
			int otherCount = 0;
			for (int i = 0; i < stats.Languages.Length; i++)
			{
				var lang = stats.Languages[i];
				if (i < 12)
				{
					pieSeries.Slices.Add(new PieSlice(lang.Name, lang.Code)
					{
						Fill = _pieChartColors[i % _pieChartColors.Length]
					});
					listItems.Add(new CodeLineLanguageViewModel(
						lang.Name, lang.Files, lang.Code, lang.Comments, lang.Blanks,
						OxyColorToHex(_colors[i % _colors.Length])));
				}
				else
				{
					otherCode += lang.Code;
					otherCount += (int)lang.Files;
				}
			}
			if (otherCode > 0)
			{
				int idx = 12;
				pieSeries.Slices.Add(new PieSlice(Translate("Other"), otherCode)
				{
					Fill = _pieChartColors[idx % _pieChartColors.Length]
				});
				listItems.Add(new CodeLineLanguageViewModel(
					Translate("Other"), otherCount, otherCode, 0, 0,
					OxyColorToHex(_colors[idx % _colors.Length])));
			}
			CodeLinesListBox.ItemsSource = listItems;
			CodeLinesPiePlot.InvalidatePlot();

			// 摘要：总文件数 · 总代码行 · 总注释 · 总空白 · 当前 ref
			string refLabel = string.IsNullOrEmpty(stats.RefSpec)
				? Translate("Workspace")
				: stats.RefSpec;
			CodeLinesSummary.Text = string.Format(CultureInfo.CurrentUICulture,
				Translate("{0}: {1} files · {2} code · {3} comments · {4} blanks"),
				refLabel,
				stats.TotalFiles.ToString("N0"),
				stats.TotalCode.ToString("N0"),
				stats.TotalComments.ToString("N0"),
				stats.TotalBlanks.ToString("N0"));
		}

		private void ShowCodeLinesError(string message)
		{
			CodeLinesError.Text = message;
			CodeLinesError.Visibility = Visibility.Visible;
			CodeLinesSummary.Text = "";
			// 清空饼图和列表
			(_codeLinesPieModel.Series[0] as PieSeries).Slices.Clear();
			CodeLinesPiePlot.InvalidatePlot();
			CodeLinesListBox.ItemsSource = null;
		}

		/// <summary>OxyColor → "#RRGGBB" 十六进制字符串，给 XAML Rectangle.Fill 用。</summary>
		private static string OxyColorToHex(OxyColor c)
		{
			return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
		}

		/// <summary>ComboBox 项：显示名 + 实际 refSpec（null=工作区 snapshot）。</summary>
		public class CodeLineRefItem
		{
			public string Display { get; }
			[Null]
			public string RefSpec { get; }
			public CodeLineRefItem(string display, [Null] string refSpec)
			{
				Display = display;
				RefSpec = refSpec;
			}
			public override string ToString() => Display;
		}

	}
}
