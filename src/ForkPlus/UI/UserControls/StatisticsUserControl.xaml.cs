using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;
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
			DateRangeButton.DateRangeChanged += delegate
			{
				if (!_isCalendarUpdatingInProgress)
				{
					UpdatePreview(_gitModule, DateRangeButton.DateRange);
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

		private void UpdatePreview(GitModule gitModule, [Null] CalendarDateRange dateRange)
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

	}
}
