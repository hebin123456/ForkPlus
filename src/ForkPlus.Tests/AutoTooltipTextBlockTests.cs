// 回归测试（2026-09-03，"暂存区文件鼠标覆盖有个空的 tips"修复产物）：
// 根因：WPF ToolTipOpening 里 e.Handled = true 即取消弹窗；Avalonia 12 的
// ToolTip.IsOpenChanged 只检查 CancelRoutedEventArgs.Cancel，Handled 无效。
// AutoTooltipTextBlock 构造时预置空字符串 Tip，"无可显示内容"分支只置 Handled
// 时事件不取消 → 空 tooltip 框照样弹出。修复：该分支补 e.Cancel = true。
// 本测试守卫 WPF 原版三种行为：
//   1) 无 CustomToolTip 且文本未截断 → 不显示 tooltip（IsOpen 回落 false）；
//   2) 有 CustomToolTip（重命名文件 Old/New 路径）→ Tip 更新为自定义内容；
//   3) 文本被截断 → Tip 更新为完整文本（auto tooltip）。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Layout;
using ForkPlus.UI.Controls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class AutoTooltipTextBlockTests
	{
		[Fact]
		public void NoContentAndNotTrimmed_TooltipIsCancelled()
		{
			string diag = HeadlessAppBootstrap.Run(delegate
			{
				var tb = new AutoTooltipTextBlock { Text = "a.txt" };
				var window = new Window { Width = 200, Height = 60, Content = tb };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				// 构造函数预置空字符串 Tip（保证 opening 事件能触发）。
				Assert.Equal("", ToolTip.GetTip(tb));

				// 模拟 tooltip 服务置 IsOpen=true：opening 事件被 Cancel 取消，
				// IsOpen 必须回落 false——修复前 Handled 无效，弹空框且 IsOpen 保持 true。
				ToolTip.SetIsOpen(tb, true);
				Dispatcher.UIThread.RunJobs();
				bool isOpenAfterOpen = ToolTip.GetIsOpen(tb);
				Assert.False(isOpenAfterOpen, "无内容且未截断时 tooltip 必须被取消（IsOpen 回落 false）");

				window.Close();
				return "tip='" + ToolTip.GetTip(tb) + "' isOpen=" + isOpenAfterOpen;
			});
			System.IO.File.WriteAllText("/tmp/auto_tooltip_cancel.txt", diag);
		}

		[Fact]
		public void CustomToolTip_RenamesTipContent()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var tb = new AutoTooltipTextBlock { Text = "a.txt", CustomToolTip = "Old:\told.txt\nNew:\ta.txt" };
				var window = new Window { Width = 200, Height = 60, Content = tb };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				ToolTip.SetIsOpen(tb, true);
				Dispatcher.UIThread.RunJobs();

				Assert.Equal("Old:\told.txt\nNew:\ta.txt", ToolTip.GetTip(tb));

				window.Close();
			});
		}

		[Fact]
		public void TrimmedText_TipBecomesFullText()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				string longText = new string('x', 200);
				var tb = new AutoTooltipTextBlock { Text = longText };
				// 固定小宽度容器 → 文本必然被截断（TextTrimming 在控件构造里设置）。
				var panel = new StackPanel { Width = 80, Children = { tb } };
				var window = new Window { Width = 200, Height = 60, Content = panel };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				Assert.True(tb.Bounds.Width < 200, "前置条件：文本容器宽度需小于文本全长");

				ToolTip.SetIsOpen(tb, true);
				Dispatcher.UIThread.RunJobs();

				// 截断场景下 tooltip 显示完整文本。
				Assert.Equal(longText, ToolTip.GetTip(tb));

				window.Close();
			});
		}
	}
}
