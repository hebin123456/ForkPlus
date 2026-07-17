using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
	// Below the grid: a 5-step color legend (Less ... More) and a summary line
	// (total contributions / longest streak / most active day).
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

		private const double LegendCellSize = 10.0;

		private readonly Grid _heatmapGrid;

		private readonly Border[] _legendCells = new Border[5];

		private readonly TextBlock _summaryText;

		public ContributionHeatmap()
		{
			// 外层两行：热力图 + (图例/摘要)
			RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

			// 热力图子 Grid（53 列 × 7 行）
			_heatmapGrid = new Grid();
			for (int i = 0; i < WeeksCount; i++)
			{
				_heatmapGrid.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(CellSize + CellGap)
				});
			}
			for (int j = 0; j < DayCount; j++)
			{
				_heatmapGrid.RowDefinitions.Add(new RowDefinition
				{
					Height = new GridLength(CellSize + CellGap)
				});
			}
			SetRow(_heatmapGrid, 0);
			Children.Add(_heatmapGrid);

			// 底部行：图例 + 摘要，水平排列
			StackPanel bottomPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Margin = new Thickness(0, 8, 0, 0)
			};
			SetRow(bottomPanel, 1);
			Children.Add(bottomPanel);

			// 图例：Less [5 色块] More
			StackPanel legendPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				VerticalAlignment = VerticalAlignment.Center
			};
			TextBlock lessLabel = new TextBlock
			{
				Text = TranslateLegend("Less"),
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 11,
				Margin = new Thickness(0, 0, 4, 0),
				Foreground = Theme.SecondaryLabelBrush
			};
			legendPanel.Children.Add(lessLabel);
			for (int k = 0; k < 5; k++)
			{
				Border legendCell = new Border
				{
					Width = LegendCellSize,
					Height = LegendCellSize,
					CornerRadius = new CornerRadius(2),
					Margin = new Thickness(0, 0, 2, 0),
					VerticalAlignment = VerticalAlignment.Center
				};
				_legendCells[k] = legendCell;
				legendPanel.Children.Add(legendCell);
			}
			TextBlock moreLabel = new TextBlock
			{
				Text = TranslateLegend("More"),
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 11,
				Margin = new Thickness(4, 0, 16, 0),
				Foreground = Theme.SecondaryLabelBrush
			};
			legendPanel.Children.Add(moreLabel);
			bottomPanel.Children.Add(legendPanel);

			// 摘要文本
			_summaryText = new TextBlock
			{
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 11,
				TextWrapping = TextWrapping.NoWrap,
				Foreground = Theme.SecondaryLabelBrush
			};
			bottomPanel.Children.Add(_summaryText);

			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
		}

		private static string TranslateLegend(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
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
			_heatmapGrid.Children.Clear();
			Dictionary<DateTime, DayContributionInfo> data = CommitsByDate;
			if (data == null)
			{
				_summaryText.Text = "";
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
					_heatmapGrid.Children.Add(border);
				}
			}

			// 刷新图例色块颜色
			for (int k = 0; k < 5; k++)
			{
				_legendCells[k].Background = palette[k];
			}

			// 计算并刷新统计摘要
			_summaryText.Text = BuildSummary(data);
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

		// 摘要：总贡献数 | 最长连续提交天数 | 最活跃日。
		// 连续天数基于 data 中有 commits 的日期排序后逐日判定（gap == 1 天）。
		private static string BuildSummary(Dictionary<DateTime, DayContributionInfo> data)
		{
			if (data == null || data.Count == 0)
			{
				return "";
			}
			int total = 0;
			int longestStreak = 0;
			int currentStreak = 0;
			DateTime? prevDate = null;
			DateTime mostActiveDate = DateTime.MinValue;
			int mostActiveCount = 0;
			// 按日期升序遍历，同时累计 total / streak / most active
			foreach (KeyValuePair<DateTime, DayContributionInfo> kvp in data.OrderBy(kv => kv.Key))
			{
				int commits = kvp.Value?.Commits ?? 0;
				total += commits;
				if (commits > 0)
				{
					if (prevDate.HasValue && kvp.Key == prevDate.Value.AddDays(1))
					{
						currentStreak++;
					}
					else
					{
						currentStreak = 1;
					}
					if (currentStreak > longestStreak)
					{
						longestStreak = currentStreak;
					}
					if (commits > mostActiveCount)
					{
						mostActiveCount = commits;
						mostActiveDate = kvp.Key;
					}
					prevDate = kvp.Key;
				}
				else
				{
					currentStreak = 0;
					prevDate = null;
				}
			}

			string totalFormat = PreferencesLocalization.Translate("Total: {0}", ForkPlusSettings.Default.UiLanguage);
			string streakFormat = PreferencesLocalization.Translate("Longest streak: {0} days", ForkPlusSettings.Default.UiLanguage);
			string mostActiveFormat = PreferencesLocalization.Translate("Most active: {0} ({1})", ForkPlusSettings.Default.UiLanguage);
			StringBuilder sb = new StringBuilder();
			sb.Append(string.Format(totalFormat, total));
			if (longestStreak > 0)
			{
				sb.Append("  ·  ").Append(string.Format(streakFormat, longestStreak));
			}
			if (mostActiveCount > 0)
			{
				string dateStr = mostActiveDate.ToString("yyyy-MM-dd", CultureInfo.CurrentCulture);
				sb.Append("  ·  ").Append(string.Format(mostActiveFormat, dateStr, mostActiveCount));
			}
			return sb.ToString();
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
			if (ForkPlusSettings.Default.Theme.IsDarkBase())
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
