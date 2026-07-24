// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Controls
// - using System.Windows.Media → using Avalonia.Media
// - DependencyProperty.Register + PropertyMetadata 回调 → StyledProperty + OnPropertyChanged override
// - DependencyPropertyChangedEventArgs → AvaloniaPropertyChangedEventArgs
// - Brush（字段/属性类型）→ IBrush；SolidColorBrush 保持
// - Application.Current.TryFindResource → Theme.FindResource
// - FrameworkElement.ToolTip 属性 → ToolTip.SetTip 附加属性
// - WeakEventManager<T,S>.AddHandler → 直接事件订阅（阶段 6 改用 Avalonia WeakEvent）
// - Freeze() 移除（Avalonia 画刷默认不可变）
// - Grid 基类保持（Avalonia.Controls.Grid）
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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
		// 阶段 4.5：WPF DependencyProperty.Register + PropertyMetadata 回调
		// → Avalonia StyledProperty（AvaloniaProperty.Register<TOwner, TType>）+ OnPropertyChanged override。
		public static readonly StyledProperty<Dictionary<DateTime, DayContributionInfo>> CommitsByDateProperty =
			AvaloniaProperty.Register<ContributionHeatmap, Dictionary<DateTime, DayContributionInfo>>(nameof(CommitsByDate));

		public Dictionary<DateTime, DayContributionInfo> CommitsByDate
		{
			get
			{
				return GetValue(CommitsByDateProperty);
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

			// 阶段 4.5：WPF WeakEventManager<T,S>.AddHandler → 直接事件订阅。
			// TODO(4.6-a): 阶段 6 改用 Avalonia WeakEvent 避免内存泄漏。
			NotificationCenter.Current.ApplicationThemeChanged += ApplicationThemeChanged;
		}

		// 阶段 4.5：WPF OnPropertyChanged(DependencyPropertyChangedEventArgs) 静态回调
		// → Avalonia OnPropertyChanged(AvaloniaPropertyChangedEventArgs) override（change.Property API 兼容）。
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == CommitsByDateProperty)
			{
				RebuildCells();
			}
		}

		private static string TranslateLegend(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

		private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
		{
			RebuildCells();
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
			IBrush[] palette = GetPalette();
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
						HorizontalAlignment = HorizontalAlignment.Left,
						VerticalAlignment = VerticalAlignment.Top
					};
					// 阶段 4.5：WPF FrameworkElement.ToolTip 属性 → Avalonia ToolTip.SetTip 附加属性。
					ToolTip.SetTip(border, BuildTooltip(tooltipFormat, authorsFormat, moreFormat, date, commits, info));
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

		private static IBrush[] GetPalette()
		{
			// 优先读 Heatmap.LevelNColor 资源（CustomColorsDialog 覆盖或主题字典），取不到回退到硬编码默认值。
			// 不 Freeze 资源画刷，使其能随主题/自定义颜色变化更新。
			// 阶段 4.5：Avalonia 画刷默认不可变，无需 Freeze()。
			bool isDark = ForkPlusSettings.Default.Theme.IsDarkBase();
			Color[] defaults = isDark
				? new Color[5]
				{
					Color.FromRgb(22, 27, 34),
					Color.FromRgb(3, 58, 22),
					Color.FromRgb(25, 111, 26),
					Color.FromRgb(46, 160, 67),
					Color.FromRgb(63, 217, 94)
				}
				: new Color[5]
				{
					Color.FromRgb(235, 237, 240),
					Color.FromRgb(155, 233, 168),
					Color.FromRgb(64, 196, 99),
					Color.FromRgb(48, 161, 78),
					Color.FromRgb(33, 110, 57)
				};
			IBrush[] palette = new IBrush[5];
			for (int i = 0; i < 5; i++)
			{
				Color c = TryFindColor("Heatmap.Level" + i + "Color") ?? defaults[i];
				palette[i] = new SolidColorBrush(c);
			}
			return palette;
		}

		private static Color? TryFindColor(string key)
		{
			// 阶段 4.5：WPF Application.Current.TryFindResource → Theme.FindResource。
			object res = Theme.FindResource(key);
			if (res is Color c) return c;
			if (res is SolidColorBrush b) return b.Color;
			return null;
		}
	}
}
