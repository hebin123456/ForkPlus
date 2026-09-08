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
	}
}
