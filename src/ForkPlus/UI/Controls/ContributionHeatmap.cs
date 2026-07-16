using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Controls
{
	// GitHub-style 53-week x 7-day contribution heatmap. Renders one Border per day,
	// colored by commit count level (5-step palette, theme-aware). Recent weeks are
	// on the right; the trailing partial week (future dates) is left empty.
	public class ContributionHeatmap : Grid
	{
		public static readonly DependencyProperty CommitsByDateProperty = DependencyProperty.Register(
			"CommitsByDate", typeof(Dictionary<DateTime, int>), typeof(ContributionHeatmap),
			new PropertyMetadata(null, OnCommitsByDateChanged));

		public Dictionary<DateTime, int> CommitsByDate
		{
			get
			{
				return (Dictionary<DateTime, int>)GetValue(CommitsByDateProperty);
			}
			set
			{
				SetValue(CommitsByDateProperty, value);
			}
		}

		private const int WeeksCount = 53;

		private const int DayCount = 7;

		private const double CellSize = 11.0;

		private const double CellGap = 3.0;

		public ContributionHeatmap()
		{
			for (int i = 0; i < WeeksCount; i++)
			{
				ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(CellSize + CellGap)
				});
			}
			for (int j = 0; j < DayCount; j++)
			{
				RowDefinitions.Add(new RowDefinition
				{
					Height = new GridLength(CellSize + CellGap)
				});
			}
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
		}

		private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
		{
			RebuildCells();
		}

		private static void OnCommitsByDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			((ContributionHeatmap)d).RebuildCells();
		}

		private void RebuildCells()
		{
			Children.Clear();
			Dictionary<DateTime, int> data = CommitsByDate;
			if (data == null)
			{
				return;
			}
			DateTime today = DateTime.Today;
			int todayDow = (int)today.DayOfWeek;
			DateTime lastSunday = today.AddDays(-todayDow);
			DateTime startDate = lastSunday.AddDays(-(WeeksCount - 1) * 7);
			int maxCommits = 0;
			foreach (KeyValuePair<DateTime, int> kvp in data)
			{
				if (kvp.Value > maxCommits)
				{
					maxCommits = kvp.Value;
				}
			}
			Brush[] palette = GetPalette();
			string tooltipFormat = PreferencesLocalization.Translate("{0} contributions on {1}", ForkPlusSettings.Default.UiLanguage);
			for (int week = 0; week < WeeksCount; week++)
			{
				for (int dow = 0; dow < DayCount; dow++)
				{
					DateTime date = startDate.AddDays(week * 7 + dow);
					if (date > today)
					{
						continue;
					}
					int commits = (data.TryGetValue(date, out var c) ? c : 0);
					int level = GetLevel(commits, maxCommits);
					Border border = new Border
					{
						Width = CellSize,
						Height = CellSize,
						Background = palette[level],
						CornerRadius = new CornerRadius(2),
						ToolTip = string.Format(tooltipFormat, commits, date.ToString("yyyy-MM-dd")),
						HorizontalAlignment = HorizontalAlignment.Left,
						VerticalAlignment = VerticalAlignment.Top
					};
					SetColumn(border, week);
					SetRow(border, dow);
					Children.Add(border);
				}
			}
		}

		private static int GetLevel(int commits, int maxCommits)
		{
			if (commits <= 0 || maxCommits <= 0)
			{
				return 0;
			}
			double ratio = (double)commits / (double)maxCommits;
			if (ratio <= 0.25)
			{
				return 1;
			}
			if (ratio <= 0.5)
			{
				return 2;
			}
			if (ratio <= 0.75)
			{
				return 3;
			}
			return 4;
		}

		private static Brush[] GetPalette()
		{
			if (ForkPlusSettings.Default.Theme == ThemeType.Dark)
			{
				return new Brush[5]
				{
					Freeze(new SolidColorBrush(Color.FromRgb(22, 27, 34))),
					Freeze(new SolidColorBrush(Color.FromRgb(3, 58, 22))),
					Freeze(new SolidColorBrush(Color.FromRgb(25, 111, 26))),
					Freeze(new SolidColorBrush(Color.FromRgb(46, 160, 67))),
					Freeze(new SolidColorBrush(Color.FromRgb(63, 217, 94)))
				};
			}
			return new Brush[5]
			{
				Freeze(new SolidColorBrush(Color.FromRgb(235, 237, 240))),
				Freeze(new SolidColorBrush(Color.FromRgb(155, 233, 168))),
				Freeze(new SolidColorBrush(Color.FromRgb(64, 196, 99))),
				Freeze(new SolidColorBrush(Color.FromRgb(48, 161, 78))),
				Freeze(new SolidColorBrush(Color.FromRgb(33, 110, 57)))
			};
		}

		private static Brush Freeze(Brush brush)
		{
			brush.Freeze();
			return brush;
		}
	}
}
