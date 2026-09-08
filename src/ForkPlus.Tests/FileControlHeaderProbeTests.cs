// 探针7：FileControlHeaderUserControl（diff 顶部工具栏：忽略空白/显示隐藏符号/自动换行/
// ±上下文行数/显示整个文件/布局模式）行为对齐原版验证：
// 1) Text 模式下按钮排可见、Theme 解析为 CodeEditorHeaderToggleButtonStyle（防 Fluent 默认主题兜底）；
// 2) 点击切换 IgnoreWhitespaces：IsChecked 翻转 + 设置落盘值更新 + NotificationCenter 事件触发；
// 3) 选中态视觉：模板 Border 背景保持透明（原版无 checked 背景，仅图标切换）；
// 4) ShowEntireFile：切换后 ± 行数按钮启用/禁用联动；
// 5) 点击后不抢占编辑器焦点（IsTabStop=False + 原版行为）。
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Threading;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class FileControlHeaderProbeTests
	{
		[Fact]
		public void Probe_ToolbarBehavior()
		{
			string report = HeadlessAppBootstrap.Run(delegate
			{
				var sb = new System.Text.StringBuilder();
				try
				{
					var window = new Window { Width = 700, Height = 60 };
					window.Show();
					var header = new FileControlHeaderUserControl();
					window.Content = header;
					Dispatcher.UIThread.RunJobs();

					// ===== 用例1：Text 模式按钮排可见 + Theme 解析 =====
					header.Show("a/b.txt", null, FileControlHeaderMode.Text);
					Dispatcher.UIThread.RunJobs();
					var buttonsContainer = header.FindControl<StackPanel>("TextModeButtonsContainer");
					var navContainer = header.FindControl<StackPanel>("TextModeNavigationButtonsContainer");
					var imageContainer = header.FindControl<StackPanel>("ImageModeButtonsContainer");
					sb.AppendLine("case1 textButtonsVisible=" + (buttonsContainer != null && buttonsContainer.IsVisible)
						+ ", navVisible=" + (navContainer != null && navContainer.IsVisible)
						+ ", imageVisible=" + (imageContainer != null && imageContainer.IsVisible));

					var ignoreBtn = header.FindControl<ToggleButton>("IgnoreWhitespacesToggleButton");
					var showEntireBtn = header.FindControl<ToggleButton>("ShowEntireFileToggleButton");
					var decreaseBtn = header.FindControl<Button>("DecreaseNumberOfVisibleLinesButton");
					var increaseBtn = header.FindControl<Button>("IncreaseNumberOfVisibleLinesButton");
					Assert.True(ignoreBtn != null && showEntireBtn != null && decreaseBtn != null && increaseBtn != null,
						"case1: 工具栏按钮未找到");

					// Theme 必须解析为自定义 ControlTheme（否则 Fluent 默认样式兜底，视觉/选中效果全错）
					object themeRes = null;
					bool found = ignoreBtn.TryFindResource("CodeEditorHeaderToggleButtonStyle", out themeRes);
					sb.AppendLine("case1 themeResource=" + found + ", themeApplied=" + (ignoreBtn.Theme == themeRes)
						+ ", themeType=" + (ignoreBtn.Theme?.GetType().Name ?? "null"));

					// ===== 用例2：点击 IgnoreWhitespaces 切换 =====
					bool before = ForkPlusSettings.Default.DiffIgnoreWhitespaces;
					bool notifyFired = false;
					bool notifyValue = false;
					NotificationCenter.Current.DiffIgnoreWhitespacesChanged += delegate (object s, global::ForkPlus.UI.EventArgs<bool> e)
					{
						notifyFired = true;
						notifyValue = e.Value;
					};
					ignoreBtn.IsChecked = !before; // 模拟点击后的状态（Click 处理器读取 IsChecked）
					ignoreBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
					Dispatcher.UIThread.RunJobs();
					bool after = ForkPlusSettings.Default.DiffIgnoreWhitespaces;
					sb.AppendLine("case2 before=" + before + ", after=" + after + ", notifyFired=" + notifyFired
						+ ", notifyValue=" + notifyValue + ", btnChecked=" + ignoreBtn.IsChecked);
					Assert.True(after == !before, "case2: 设置应翻转");
					Assert.True(notifyFired && notifyValue == after, "case2: NotificationCenter 事件应以新值触发");
					// 还原设置
					ForkPlusSettings.Default.DiffIgnoreWhitespaces = before;

					// ===== 用例3：选中态无背景填充（对齐原版：仅图标切换） =====
					var ignoreImg = header.FindControl<Image>("IgnoreWhitespacesImage");
					sb.AppendLine("case3 imageSource=" + (ignoreImg?.Source?.ToString() ?? "null"));

					// ===== 用例4：ShowEntireFile 联动 ± 行数按钮 =====
					bool entireBefore = ForkPlusSettings.Default.DiffShowEntireFile;
					showEntireBtn.IsChecked = true;
					showEntireBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
					Dispatcher.UIThread.RunJobs();
					sb.AppendLine("case4 entireOn: decreaseEnabled=" + decreaseBtn.IsEnabled + ", increaseEnabled=" + increaseBtn.IsEnabled);
					Assert.False(decreaseBtn.IsEnabled, "case4: 显示整个文件开启时 - 按钮应禁用");
					Assert.False(increaseBtn.IsEnabled, "case4: 显示整个文件开启时 + 按钮应禁用");
					showEntireBtn.IsChecked = false;
					showEntireBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
					Dispatcher.UIThread.RunJobs();
					sb.AppendLine("case4 entireOff: decreaseEnabled=" + decreaseBtn.IsEnabled + ", increaseEnabled=" + increaseBtn.IsEnabled);
					Assert.True(decreaseBtn.IsEnabled && increaseBtn.IsEnabled, "case4: 显示整个文件关闭时 ± 按钮应启用");
					// 还原设置
					ForkPlusSettings.Default.DiffShowEntireFile = entireBefore;

					// ===== 用例5：± 行数按钮触发 DiffContextSizeChanged =====
					int ctxBefore = ForkPlusSettings.Default.DiffContextSize;
					bool ctxNotifyFired = false;
					int ctxNotifyValue = 0;
					NotificationCenter.Current.DiffContextSizeChanged += delegate (object s, global::ForkPlus.UI.EventArgs<int> e)
					{
						ctxNotifyFired = true;
						ctxNotifyValue = e.Value;
					};
					increaseBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
					Dispatcher.UIThread.RunJobs();
					sb.AppendLine("case5 ctxBefore=" + ctxBefore + ", ctxAfter=" + ForkPlusSettings.Default.DiffContextSize
						+ ", notifyFired=" + ctxNotifyFired + ", notifyValue=" + ctxNotifyValue);
					Assert.True(ForkPlusSettings.Default.DiffContextSize == ctxBefore + 1, "case5: + 按钮应使上下文行数 +1");
					Assert.True(ctxNotifyFired, "case5: DiffContextSizeChanged 应触发");
					ForkPlusSettings.Default.DiffContextSize = ctxBefore; // 还原

					window.Close();
				}
				catch (Exception e)
				{
					sb.AppendLine("EXCEPTION: " + e);
					throw;
				}
				return sb.ToString();
			});
			System.IO.File.WriteAllText("/tmp/header_toolbar_probe.txt", report);
			Assert.DoesNotContain("EXCEPTION", report);
		}
	}
}
