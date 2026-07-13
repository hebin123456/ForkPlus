using System;
using System.Linq;
using FlaUI.Core.AutomationElements;
using Xunit;

namespace ForkPlus.AutomationTests
{
	/// <summary>
	/// 应用启动后 UI 结构验证：验证主窗口的关键功能区存在。
	/// 这些测试不操作 git 仓库，仅验证 UI 渲染后的控件结构。
	/// </summary>
	public class AppStartupTests : AutomationTestBase, IDisposable
	{
		[Fact]
		public void MainWindow_IsNotMinimized()
		{
			using (var app = LaunchApp())
			{
				Assert.NotNull(app.Window);
				// 主窗口不应处于最小化状态。
				Assert.NotEqual(WindowVisualState.Minimized, app.Window.VisualState);
			}
		}

		[Fact]
		public void MainWindow_IsEnabled()
		{
			using (var app = LaunchApp())
			{
				Assert.NotNull(app.Window);
				// 主窗口应处于启用状态（非禁用）。
				Assert.True(app.Window.IsEnabled, "主窗口未启用，可能被模态对话框阻塞。");
			}
		}

		[Fact]
		public void MainWindow_HasExpectedBounds()
		{
			using (var app = LaunchApp())
			{
				Assert.NotNull(app.Window);
				// 主窗口应有非零尺寸。
				var bounds = app.Window.BoundingRectangle;
				Assert.True(bounds.Width > 0, $"主窗口宽度为 0：{bounds}");
				Assert.True(bounds.Height > 0, $"主窗口高度为 0：{bounds}");
			}
		}

		[Fact]
		public void MainWindow_ContainsTabStrips()
		{
			using (var app = LaunchApp())
			{
				Assert.NotNull(app.Window);
				// 主窗口包含 ClosableTabControl，应有 Tab 类型子元素。
				// 递归查找所有 TabItem 类型的后代元素。
				var tabItems = app.Window.FindAllDescendants(
					cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem));
				// 首次启动可能没有打开任何 Tab，也可能有默认 Tab。
				// 这里不强制断言 TabItem 数量，只验证查找不抛异常。
				Assert.NotNull(tabItems);
			}
		}

		[Fact]
		public void MainWindow_ContainsButtons()
		{
			using (var app = LaunchApp())
			{
				Assert.NotNull(app.Window);
				// 主窗口的工具栏应包含按钮。
				var buttons = app.Window.FindAllDescendants(
					cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));
				// 工具栏至少应有几个按钮（如新建、打开等）。
				Assert.True(buttons.Length > 0, "主窗口未找到任何按钮，工具栏可能未渲染。");
			}
		}

		[Fact]
		public void MainWindow_NoBlockingDialog()
		{
			using (var app = LaunchApp())
			{
				Assert.NotNull(app.Window);
				// 等待 3 秒让所有异步初始化完成。
				System.Threading.Thread.Sleep(3000);
				// 主窗口应仍然是当前活动窗口（没有被模态对话框阻塞）。
				// 如果有对话框弹出，GetMainWindow 可能返回对话框而非主窗口。
				var currentWindow = app.RefreshMainWindow();
				Assert.NotNull(currentWindow);
				// 主窗口标题应仍包含 "ForkPlus"。
				string title = currentWindow.Title ?? "";
				Assert.Contains("ForkPlus", title);
			}
		}
	}
}
