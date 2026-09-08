// 回归测试（2026-09-07 v3.14，"很多 TextBox 左侧有这么大的留空，比如推理服务 URL 的框"）：
// PlaceholderTextBox/AutoCompleteTextBox 系模板含"图标列"（Column0=Auto: Image#IconElement
// 14x14 + Margin4）。WPF 原模板有 <Trigger Property=Icon><x:Null/> → IconElement Collapsed>
// ——无图标时 Auto 列折叠 0 宽；迁移版该 Trigger 被注释丢弃，空 Image 恒占位 → 128 处无 Icon
// 的输入框（AI 偏好"推理服务 URL"、外部工具、自定义命令等）左侧凭空多 18px 留白。
// 修复：Avalonia 选择器无"属性=null"匹配 → 控件类维护 :noicon 伪类（PlaceholderTextBox.cs
// 构造器 + IconProperty.Changed.AddClassHandler），主题 ^:noicon /template/ Image#IconElement
// → IsVisible=False（不可见元素不参与度量，Auto 列折叠 0，与 WPF Collapsed 同效）。
// 本测试守卫：1) 无 Icon 时 IconElement 折叠且 TextPresenter 左缘贴边（~3px = 边框1+Padding2，
// 修复前 ~21px）；2) 有 Icon 时图标列正常占位（~21px）；3) 运行时 Icon 属性变化伪类实时切换；
// 4) AutoCompleteTextBox 派生主题同样生效。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.Controls;
using System.Linq;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class PlaceholderTextBoxNoIconGapTests
	{
		private static Image FindIconElement(TextBox box)
		{
			return box.GetVisualDescendants().OfType<Image>().First((Image i) => i.Name == "IconElement" || i.Name == "iconElement");
		}

		private static TextPresenter FindTextPresenter(TextBox box)
		{
			return box.GetVisualDescendants().OfType<TextPresenter>().First();
		}

		/// <summary>TextPresenter 左缘相对控件原点的 X（含边框/内边距，不含图标列间隙）。</summary>
		private static double PresenterLeft(TextBox box)
		{
			TextPresenter presenter = FindTextPresenter(box);
			Point? left = presenter.TranslatePoint(new Point(0, 0), box);
			Assert.True(left.HasValue, "TextPresenter 与控件不在同一视觉树");
			return left!.Value.X;
		}

		[Fact]
		public void WithoutIcon_IconElementCollapsed_PresenterAtLeftEdge()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				var tb = new PlaceholderTextBox { Text = "https://api.openai.com", Width = 260, Height = 30 };
				var window = new Window { Width = 320, Height = 100, Content = tb };
				window.Show();
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

				Image icon = FindIconElement(tb);
				// 修复前：Trigger 丢失 → 空 Image 恒可见（IsVisible=true）。
				Assert.False(icon.IsVisible, "无 Icon 时 IconElement 应被 :noicon 伪类样式折叠");
				// 修复前：图标列占位 ~18px（margin4+宽14），presenter 左缘 ≈21px；
				// 修复后：边框 1 + Padding 2 = ~3px。
				Assert.True(PresenterLeft(tb) < 6.0,
					"无 Icon 时文字起点应贴左侧（实测 " + PresenterLeft(tb) + "px，图标列未折叠即 18px 留白回归）");
				window.Close();
				return 0;
			});
		}

		[Fact]
		public void WithIcon_IconElementVisible_PresenterOffsetByIconColumn()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				using var icon = new WriteableBitmap(new PixelSize(14, 14), new Vector(96, 96));
				var tb = new PlaceholderTextBox { Text = "enter branch name", Width = 260, Height = 30, Icon = icon };
				var window = new Window { Width = 320, Height = 100, Content = tb };
				window.Show();
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

				Assert.True(FindIconElement(tb).IsVisible, "有 Icon 时 IconElement 应可见");
				// 边框 1 + margin 4 + 图标 14 + Padding 2 ≈ 21px（图标列正常占位）。
				double left = PresenterLeft(tb);
				Assert.True(left > 15.0 && left < 26.0,
					"有 Icon 时文字起点应让出图标列（实测 " + left + "px）");
				window.Close();
				return 0;
			});
		}

		[Fact]
		public void IconChangedAtRuntime_TogglesPseudoClassAndLayout()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				var tb = new PlaceholderTextBox { Text = "abc", Width = 260, Height = 30 };
				var window = new Window { Width = 320, Height = 100, Content = tb };
				window.Show();
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				Assert.True(PresenterLeft(tb) < 6.0, "初始无 Icon 应无图标列");

				// 运行时赋 Icon（DynamicResource 解析晚于模板挂载的现实时序）→ 伪类切换、图标列出现。
				using var icon = new WriteableBitmap(new PixelSize(14, 14), new Vector(96, 96));
				tb.Icon = icon;
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				Assert.True(FindIconElement(tb).IsVisible, "运行时设置 Icon 后 IconElement 应可见");
				Assert.True(PresenterLeft(tb) > 15.0, "运行时设置 Icon 后图标列应占位（实测 " + PresenterLeft(tb) + "px）");

				// 再清空 Icon → 回到无图标列形态。
				tb.Icon = null;
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				Assert.False(FindIconElement(tb).IsVisible, "运行时清空 Icon 后 IconElement 应再次折叠");
				Assert.True(PresenterLeft(tb) < 6.0, "运行时清空 Icon 后图标列应收起（实测 " + PresenterLeft(tb) + "px）");
				window.Close();
				return 0;
			});
		}

		[Fact]
		public void AutoCompleteTextBox_WithoutIcon_IconElementCollapsed()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				var tb = new AutoCompleteTextBox { Text = "feature/", Width = 260, Height = 30 };
				var window = new Window { Width = 320, Height = 100, Content = tb };
				window.Show();
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

				Assert.False(FindIconElement(tb).IsVisible, "AutoCompleteTextBox 无 Icon 时 IconElement 应折叠");
				Assert.True(PresenterLeft(tb) < 6.0,
					"AutoCompleteTextBox 文字起点应贴左侧（实测 " + PresenterLeft(tb) + "px）");
				window.Close();
				return 0;
			});
		}
	}
}
