using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ForkPlus.Git.Commands;
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
			"CommitsByDate", typeof(Dictionary<DateTime, DayContributionInfo>), typeof(ContributionHeatmap),
			new PropertyMetadata(null, OnCommitsByDateChanged));

		public Dictionary<DateTime, DayContributionInfo> CommitsByDate
		{
			get
			{
				return (Dictionary<DateTime, DayContributionInfo>)GetValue(CommitsByDateProperty);
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

		private const int MaxAuthorsShown = 3;

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
			Dictionary<DateTime, DayContributionInfo> data = CommitsByDate;
			if (data == null)
			{
				return;
			}
			DateTime today = DateTime.Today;
			int todayDow = (int)today.DayOfWeek;
			DateTime lastSunday = today.AddDays(-todayDow);
			DateTime startDate = lastSunday.AddDays(-(WeeksCount - 1) * 7);
			int maxCommits = 0;
			foreach (KeyValuePair<DateTime, DayContributionInfo> kvp in data)
			{
				if (kvp.Value.Commits > maxCommits)
				{
					maxCommits = kvp.Value.Commits;
				}
			}
			Brush[] palette = GetPalette();
			string tooltipFormat = PreferencesLocalization.Translate("{0} contributions on {1}", ForkPlusSettings.Default.UiLanguage);
			string authorsFormat = PreferencesLocalization.Translate("Authors: {0}", ForkPlusSettings.Default.UiLanguage);
			string moreFormat = PreferencesLocalization.Translate("+{0} more", ForkPlusSettings.Default.UiLanguage);
			for (int week = 0; week < WeeksCount; week++)
			{
				for (int dow = 0; dow < DayCount; dow++)
				{
					DateTime date = startDate.AddDays(week * 7 + dow);
					if (date > today)
					{
						continue;
					}
					DayContributionInfo info = data.TryGetValue(date, out var c) ? c : null;
					int commits = info?.Commits ?? 0;
					int level = GetLevel(commits, maxCommits);
					Border border = new Border
					{
						Width = CellSize,
						Height = CellSize,
						Background = palette[level],
						CornerRadius = new CornerRadius(2),
						ToolTip = BuildTooltip(tooltipFormat, authorsFormat, moreFormat, date, commits, info),
						HorizontalAlignment = HorizontalAlignment.Left,
						VerticalAlignment = VerticalAlignment.Top
					};
					SetColumn(border, week);
					SetRow(border, dow);
					Children.Add(border);
				}
			}
		}

		private static string BuildTooltip(string line1Format, string authorsFormat, string moreFormat, DateTime date, int commits, DayContributionInfo info)
		{
			string dateStr = date.ToString("yyyy-MM-dd ddd", CultureInfo.CurrentCulture);
			string line1 = string.Format(line1Format, commits, dateStr);
			if (commits <= 0 || info == null || info.AuthorCount == 0)
			{
				return line1;
			}
			List<string> top = info.GetTopAuthors(MaxAuthorsShown);
			if (top.Count == 0)
			{
				return line1;
			}
			int remaining = info.AuthorCount - top.Count;
			StringBuilder sb = new StringBuilder();
			sb.Append(string.Join(", ", top));
			if (remaining > 0)
			{
				sb.Append(", ").Append(string.Format(moreFormat, remaining));
			}
			string line2 = string.Format(authorsFormat, sb.ToString());
			return line1 + Environment.NewLine + line2;
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
