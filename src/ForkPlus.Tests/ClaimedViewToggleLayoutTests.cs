// v5.6.3：插件认领文件的「文本 / 结构化」切换按钮布局与可读性验证。
// 1) 按钮容器位于 Diff 顶部工具栏最左列（Grid.Column=0、左对齐）；
// 2) ShowClaimedViewToggle=true 后可见；
// 3) 使用独立的 ClaimedViewToggleButtonStyle，不再复用 16x16 图标按钮样式；
// 4) 文字字号 >= 12，按钮最小宽度 >= 70，文字不会被 12x12 ContentPresenter 裁切。
using System;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ClaimedViewToggleLayoutTests
	{
		[Fact]
		public void ViewToggle_IsTopLeft_WithReadableTextButton()
		{
			string report = HeadlessAppBootstrap.Run(delegate
			{
				var sb = new StringBuilder();
				try
				{
					var window = new Window { Width = 900, Height = 60 };
					window.Show();
					var header = new FileControlHeaderUserControl();
					window.Content = header;
					Dispatcher.UIThread.RunJobs();

					header.Show("a/b.json", null, FileControlHeaderMode.Text);
					header.ShowClaimedViewToggle = true;
					Dispatcher.UIThread.RunJobs();

					var container = header.FindControl<StackPanel>("ClaimedViewModeButtonsContainer");
					var button = header.FindControl<ToggleButton>("PreferTextDiffToggleButton");
					var textBlock = header.FindControl<TextBlock>("PreferTextDiffToggleTextBlock");
					Assert.True(container != null && button != null && textBlock != null,
						"视图切换按钮控件未找到");

					Assert.True(container.IsVisible, "ShowClaimedViewToggle=true 后按钮应可见");
					Assert.Equal(0, Grid.GetColumn(container));
					Assert.Equal(HorizontalAlignment.Left, container.HorizontalAlignment);
					sb.AppendLine("case1 column=" + Grid.GetColumn(container)
						+ ", alignment=" + container.HorizontalAlignment
						+ ", visible=" + container.IsVisible);

					bool found = button.TryFindResource("ClaimedViewToggleButtonStyle", out object theme);
					Assert.True(found && button.Theme == theme,
						"视图切换按钮应使用 ClaimedViewToggleButtonStyle");
					Assert.True(button.MinWidth >= 70, "按钮最小宽度应容纳完整文字");
					Assert.True(!double.IsNaN(button.Height) && button.Height >= 18,
						"按钮高度应足够显示 12px 文字");
					Assert.True(textBlock.FontSize >= 12, "按钮文字不应过小");

					// 几何不变量：ContentPresenter 的实际高度必须容纳文字行高，否则文字会被裁切。
					var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().FirstOrDefault();
					Assert.True(presenter != null, "按钮模板中未找到 ContentPresenter");
					var ruler = new TextBlock { Text = textBlock.Text, FontSize = textBlock.FontSize };
					ruler.Measure(Size.Infinity);
					Assert.True(presenter.Bounds.Height + 0.01 >= ruler.DesiredSize.Height,
						"按钮内容槽高度不足以容纳文字：" + presenter.Bounds.Height.ToString("F1")
						+ " < 行高 " + ruler.DesiredSize.Height.ToString("F1"));

					bool oldFound = button.TryFindResource("CodeEditorHeaderToggleButtonStyle", out object oldTheme);
					Assert.True(!oldFound || button.Theme != oldTheme,
						"不应再复用 16x16 图标按钮样式");
					sb.AppendLine("case2 theme=" + (button.Theme?.GetType().Name ?? "null")
						+ ", minWidth=" + button.MinWidth
						+ ", height=" + button.Height
						+ ", fontSize=" + textBlock.FontSize
						+ ", text=" + textBlock.Text);

					window.Close();
				}
				catch (Exception e)
				{
					sb.AppendLine("EXCEPTION: " + e);
					throw;
				}
				return sb.ToString();
			});
			Assert.DoesNotContain("EXCEPTION", report);
		}
	}
}
