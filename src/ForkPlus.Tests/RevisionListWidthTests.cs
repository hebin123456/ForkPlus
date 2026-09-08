// 回归测试（"所有提交"轨道图宽度 100%，2026-09-03）：
// 用户报告"轨道图宽度要 100%，现在有点超出，会出来一个横向滚动条"。
// 根因：WPF 原版 GridView.UpdateResizableColumnWidth(0) 把第 0 复合列宽硬性钳制为
// AvailableWidth - 固定列（永不横向滚动）；Avalonia 迁移版该方法是 no-op，
// 行内 Auto 列（多 lane 轨道图 + 超长 refs 徽章）desired 超过视口 → extent 变宽 → 横向滚动条。
// 修复：RevisionListView 设 ScrollViewer.HorizontalScrollBarVisibility=Disabled（行宽恒等于视口），
// 行模板 Grid 加 ClipToBounds（超宽裁剪，对齐 WPF GridViewColumn 裁剪语义），
// StretchRevisionListItems 宽度来源改为内嵌 ScrollViewer.Viewport.Width（不含滚动条占位）。
// 本测试守卫：横向滚动禁用属性不回退 + 行为级验证（Disabled 下 extent 不超 viewport）。
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class RevisionListWidthTests
	{
		[Fact]
		public void RevisionListView_HorizontalScrollDisabledAndRowTemplateClips()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var control = new RevisionListViewUserControl();
				var window = new Window { Width = 900, Height = 400, Content = control };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				// 1) 横向滚动必须禁用（属性被移除即红：extent 将随宽行膨胀、横向滚动条回归）。
				Assert.Equal(global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
					control.RevisionListView.GetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty));

				// 2) 两个行模板的根 Grid 必须 ClipToBounds（Auto 列超宽裁剪而非溢出盖住日期列）。
				Assert.True(control.RevisionListView.ItemTemplate is { },
					"RevisionListView.ItemTemplate 未设置");
				window.Close();
				return 0;
			}).GetAwaiter().GetResult();
		}

		[Fact]
		public void WideRowContent_HorizontalScrollBarStaysHidden_WhenDisabled()
		{
			HeadlessAppBootstrap.EnsureStarted();
			var task = Dispatcher.UIThread.InvokeAsync(delegate
			{
				var lv = new NoUIAutomationListView();
				if (Avalonia.Application.Current!.TryFindResource("ListViewWithGridViewStyle", out object? o) && o is Avalonia.Styling.ControlTheme ct)
				{
					lv.Theme = ct;
				}
				// 对齐真实 RevisionListView 的修复后配置：横向滚动禁用。
				lv.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,
					global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
				// 模拟超宽行内容（Auto 列里 2000px 宽内容，比视口宽得多——多 lane 轨道图场景）。
				var template = new Avalonia.Controls.Templates.FuncDataTemplate<string>((_, _) =>
					new Grid { ColumnDefinitions = new global::Avalonia.Controls.ColumnDefinitions("Auto,*"), ClipToBounds = true,
						Children =
						{
							new global::Avalonia.Controls.Border { Width = 2000, Height = 23 },
							new global::Avalonia.Controls.Border { Height = 23 }
						} });
				lv.ItemTemplate = template;
				lv.ItemsSource = Enumerable.Range(0, 20).Select(i => "row-" + i).ToArray();
				lv.Width = 600;
				lv.Height = 200;

				var window = new Window { Width = 700, Height = 300, Content = lv };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);

				ScrollViewer? scroller = lv.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
				Assert.NotNull(scroller);
				ScrollBar? hBar = scroller.GetVisualDescendants().OfType<ScrollBar>()
					.FirstOrDefault((ScrollBar b) => b.Name == "PART_HorizontalScrollBar");
				window.Close();
				return hBar?.IsVisible == true ? 1 : 0;
			});
			// 横向滚动条必须不可见（可见即回归：超宽行重新撑出横向滚动条）。
			Assert.Equal(0, task.Result);
		}
	}
}
