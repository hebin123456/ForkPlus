// 回归测试（2026-09-04，"windows版本，重命名仓库，仓库名被选中样式还是白色的看不清" +
// "TextBox 里面的字和左/上边框贴得太紧"）：
// 守卫重命名编辑框（EditableTextBlock.CreateAdornerTextBox 创建的 adorner TextBox，
// 仓库管理/侧栏/标签页/子模块重命名共用同一入口）：
//   1) 选区必须是全皮肤固定蓝 TextBox.Selection.*（#236BD2 + 白，5.1:1 WCAG AA），
//      不得随 Windows 系统强调色漂移——历史上选区曾绑 AccentBrush/SystemAccentBrush，
//      Windows 深色系统下系统强调色 ×1.6 亮化后为高亮浅色（典型 #7FD4FF），白字打上去
//      不可读，即用户报告的"选中样式还是白色的看不清"；
//   2) SelectAll 的选区索引必须流入 TextPresenter（漏绑 TemplateBinding 时恒 0/0，
//      画刷再对也不渲染任何高亮）；
//   3) 编辑框 Padding 必须拿到主题默认（文字不贴左/上边框）——曾因 CreateAdornerTextBox
//      无条件 textBox.Padding = base.Padding（未显式设置时为 0）以局部值覆盖主题默认，
//      重命名编辑框文字 0px 贴边框；
//   4) 调用方显式 Padding 时正常下传（历史行为保留）。
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class RenameEditorStyleTests
	{
		private const string RepoName = "my-repository-name";

		// Windows 深色系统条件下 Theme.Refresh() 写入的 SystemAccentBrush（DWM 强调色 ×1.6
		// 亮化）典型值——高亮浅蓝。注入它复现 Windows 真实条件：若选区（回归性地）重新绑回
		// 系统强调色，本测试的选区颜色断言即失败。
		private static readonly Color WindowsDarkSystemAccent = Color.FromRgb(0x7F, 0xD4, 0xFF);

		// 创建 RepositoryManagerEditableTextBlock（仓库管理重命名的真实控件）并进入编辑态，
		// 返回 adorner 里的编辑 TextBox。etbPadding 模拟调用方显式设置的 Padding。
		private static TextBox EnterEditModeAndFindEditor(Thickness? etbPadding, out Window window)
		{
			var etb = new ForkPlus.UI.Controls.RepositoryManagerEditableTextBlock
			{
				FontSize = 14,
				Height = 22,
				HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
				Value = RepoName,
				Width = 220
			};
			if (etbPadding.HasValue)
			{
				etb.Padding = etbPadding.Value;
			}
			var row = new Grid { Height = 22 };
			row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
			row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			Grid.SetColumn(etb, 1);
			row.Children.Add(etb);
			window = new Window { Width = 420, Height = 120, Content = row };
			window.Show();
			Dispatcher.UIThread.RunJobs();
			etb.IsInEditMode = true;
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			Task.Delay(100).GetAwaiter().GetResult();
			Dispatcher.UIThread.RunJobs();
			return window.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
		}

		[Fact]
		public void RenameEditor_SelectionIsReadableBlue_EvenWithWindowsLightAccent()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var sysDict = new ResourceDictionary
				{
					["SystemAccentBrush"] = new SolidColorBrush(WindowsDarkSystemAccent)
				};
				Application.Current!.Resources.MergedDictionaries.Add(sysDict);
				try
				{
					TextBox editor = EnterEditModeAndFindEditor(null, out Window window);
					Assert.NotNull(editor);

					ISolidColorBrush selBg = editor.SelectionBrush as ISolidColorBrush;
					ISolidColorBrush selFg = editor.SelectionForegroundBrush as ISolidColorBrush;
					Assert.NotNull(selBg);
					Assert.NotNull(selFg);
					// 全皮肤固定选区蓝（同列表/树选中项 #236BD2），不随系统强调色/皮肤 accent 漂移
					Assert.Equal(Color.FromRgb(0x23, 0x6B, 0xD2), selBg!.Color);
					Assert.Equal(Colors.White, selFg!.Color);

					// SelectAll 索引必须流入渲染层（漏绑时恒 0/0 → 无任何高亮）
					Avalonia.Controls.Presenters.TextPresenter presenter =
						editor.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>().First();
					Assert.Equal(0, presenter.SelectionStart);
					Assert.Equal(RepoName.Length, presenter.SelectionEnd);

					window.Close();
				}
				finally
				{
					Application.Current!.Resources.MergedDictionaries.Remove(sysDict);
				}
				return 0;
			}).GetAwaiter().GetResult();
		}

		[Fact]
		public void RenameEditor_PaddingNotGluedToBorder()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				// ETB 未显式设 Padding：编辑框拿到 TextBox 主题默认 Padding（非零），
				// 且经 Margin 下传 TextPresenter——修复前局部值 0 覆盖主题默认，文字贴边框。
				TextBox editor = EnterEditModeAndFindEditor(null, out Window window);
				Assert.NotNull(editor);
				Assert.NotEqual(default(Thickness), editor.Padding);
				Avalonia.Controls.Presenters.TextPresenter presenter =
					editor.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>().First();
				Assert.Equal(editor.Padding, presenter.Margin);
				window.Close();

				// ETB 显式设 Padding：正常下传（显式值优先于主题默认，历史行为保留）
				Thickness explicitPadding = new Thickness(6, 4, 6, 4);
				TextBox editor2 = EnterEditModeAndFindEditor(explicitPadding, out Window window2);
				Assert.NotNull(editor2);
				Assert.Equal(explicitPadding, editor2.Padding);
				Avalonia.Controls.Presenters.TextPresenter presenter2 =
					editor2.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>().First();
				Assert.Equal(explicitPadding, presenter2.Margin);
				window2.Close();
				return 0;
			}).GetAwaiter().GetResult();
		}
	}
}
