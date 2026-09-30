// 回归测试（2026-09-03，"拉取/推送右上角数字样式太不显眼"修复产物）：
// 根因：WPF 原版 badge 经 Border.Style 引用 PullPushBadge 样式（灰底、圆角 6、
// 高 12、MinWidth 12、Padding 4,0,4,1）；迁移时 Style 块被注释化，badge 只剩
// 8px 白字无背景，浅色主题下几乎不可见。修复：按原版样式值内联到 Border。
// 本测试守卫：badge 显示后必须有非透明背景、原版几何（高 12 / 圆角 6 / 内边距）。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;
using System.Linq;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class PullPushBadgeTests
	{
		[Fact]
		public void Badges_HaveOriginalVisualProperties()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string report = Dispatcher.UIThread.InvokeAsync(delegate
			{
				var toolbar = new ToolbarUserControl();
				var window = new Window { Width = 1000, Height = 60, Content = toolbar };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				// behind/ahead 均为 1 → 两个 badge 都显示（IsValid 由 behind != -1 决定）。
				var status = new UpstreamStatus(1, 1);
				toolbar.RefreshPullPushBadges(status);
				Dispatcher.UIThread.RunJobs();

				Border pullBadge = toolbar.GetVisualDescendants().OfType<Border>()
					.First((Border b) => b.Name == "PullBadge");
				Border pushBadge = toolbar.GetVisualDescendants().OfType<Border>()
					.First((Border b) => b.Name == "PushBadge");

				string diag = "pull: bg=" + (pullBadge.Background as ISolidColorBrush)?.Color.ToString()
					+ " h=" + pullBadge.Height + " r=" + pullBadge.CornerRadius
					+ "; push: bg=" + (pushBadge.Background as ISolidColorBrush)?.Color.ToString();

				ISolidColorBrush brush = pullBadge.Background as ISolidColorBrush;
				Assert.NotNull(brush);
				Assert.NotEqual(Colors.Transparent, brush.Color);
				Assert.NotEqual(default, brush.Color);
				Assert.Equal(12, pullBadge.Height);
				Assert.Equal(12, pullBadge.MinWidth);
				Assert.Equal(new CornerRadius(6), pullBadge.CornerRadius);
				Assert.Equal(new Thickness(4, 0, 4, 1), pullBadge.Padding);
				Assert.True(pullBadge.IsVisible);
				Assert.True(pushBadge.IsVisible);

			window.Close();
			return diag;
		}).GetAwaiter().GetResult();
		System.IO.File.WriteAllText("/tmp/pull_push_badge.txt", report);
	}

	// WS2.5（2026-09-29，分叉徽章）：Ahead>0 且 Behind>0（分叉态）与普通态区分——
	// 独立警示色画刷（Toolbar.PullPushBadgeDivergedBrush，diverged 类激活）+ 专属 tooltip
	//（Diverged from remote: {0} ahead, {1} behind）；普通态各徽章简化文案。
	// 画刷断言用"同一控件先后两态的解析色不同/复原"作相对比较，不硬编码主题色值
	//（headless 默认主题不定，普通态灰色浅/深主题取值不同）。
	[Fact]
	public void Badges_DivergedState_UsesWarningBrushAndTooltip()
	{
		HeadlessAppBootstrap.EnsureStarted();
		Dispatcher.UIThread.InvokeAsync(delegate
		{
			var toolbar = new ToolbarUserControl();
			var window = new Window { Width = 1000, Height = 60, Content = toolbar };
			window.Show();
			Dispatcher.UIThread.RunJobs();

			Border pullBadge = toolbar.GetVisualDescendants().OfType<Border>()
				.First((Border b) => b.Name == "PullBadge");
			Border pushBadge = toolbar.GetVisualDescendants().OfType<Border>()
				.First((Border b) => b.Name == "PushBadge");

			// ===== 1) 普通态（behind 5 / ahead 0）：仅 PullBadge 显示、无 diverged 类 =====
			toolbar.RefreshPullPushBadges(new UpstreamStatus(5, 0));
			Dispatcher.UIThread.RunJobs();
			Assert.True(pullBadge.IsVisible, "落后 5 时 PullBadge 应显示");
			Assert.False(pushBadge.IsVisible, "ahead 0 时 PushBadge 应隐藏");
			Assert.False(pullBadge.Classes.Contains("diverged"), "普通态不应有 diverged 类");
			Color normalColor = ((ISolidColorBrush)pullBadge.Background).Color;
			Assert.NotEqual(Colors.Transparent, normalColor);
			string normalTip = global::Avalonia.Controls.ToolTip.GetTip(pullBadge) as string;
			Assert.NotNull(normalTip);
			Assert.Contains("5", normalTip);

			// ===== 2) 分叉态（behind 3 / ahead 2）：两徽章 diverged 类 + 警示色 + 分叉 tooltip =====
			toolbar.RefreshPullPushBadges(new UpstreamStatus(3, 2));
			Dispatcher.UIThread.RunJobs();
			Assert.True(pullBadge.IsVisible);
			Assert.True(pushBadge.IsVisible);
			Assert.Equal("3", toolbar.PullBadgeText.Text);
			Assert.Equal("2", toolbar.PushBadgeText.Text);
			Assert.True(pullBadge.Classes.Contains("diverged"), "分叉态 PullBadge 应有 diverged 类");
			Assert.True(pushBadge.Classes.Contains("diverged"), "分叉态 PushBadge 应有 diverged 类");
			ISolidColorBrush divergedBrush = pullBadge.Background as ISolidColorBrush;
			Assert.NotNull(divergedBrush);
			Assert.NotEqual(Colors.Transparent, divergedBrush.Color);
			Assert.True(!normalColor.Equals(divergedBrush.Color), "分叉态警示色应区别于普通态徽章色");
			Assert.Equal(divergedBrush.Color, ((ISolidColorBrush)pushBadge.Background).Color);
			string pullTip = global::Avalonia.Controls.ToolTip.GetTip(pullBadge) as string;
			string pushTip = global::Avalonia.Controls.ToolTip.GetTip(pushBadge) as string;
			Assert.NotNull(pullTip);
			Assert.Contains("2", pullTip); // ahead
			Assert.Contains("3", pullTip); // behind
			Assert.Equal(pullTip, pushTip); // 分叉态两徽章同一分叉文案

			// ===== 3) 回到普通态：警示色复原（换状态不清漏 diverged 类）=====
			toolbar.RefreshPullPushBadges(new UpstreamStatus(5, 0));
			Dispatcher.UIThread.RunJobs();
			Assert.False(pullBadge.Classes.Contains("diverged"), "回到普通态应移除 diverged 类");
			Assert.Equal(normalColor, ((ISolidColorBrush)pullBadge.Background).Color);
			Assert.False(pushBadge.IsVisible);

			// ===== 4) 无上游（invalid）：两徽章隐藏、tooltip 清空 =====
			toolbar.RefreshPullPushBadges(null);
			Dispatcher.UIThread.RunJobs();
			Assert.False(pullBadge.IsVisible);
			Assert.False(pushBadge.IsVisible);
			Assert.Null(global::Avalonia.Controls.ToolTip.GetTip(pullBadge));
			Assert.Null(global::Avalonia.Controls.ToolTip.GetTip(pushBadge));

			window.Close();
		}).GetAwaiter().GetResult();
	}
}
}
