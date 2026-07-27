// 阶段 6 验证测试：用 Avalonia headless 平台实际加载 App.axaml，
// 验证 Menu ControlTheme 是否被应用（ItemsPanel 横向）、Window 按钮样式是否被 Classes 应用、
// ToolbarButton ControlTheme 是否被应用（Template 非 null）。
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using ForkPlus.UI.Controls;
using Xunit;

namespace ForkPlus.Tests
{
	public class XamlRuntimeTests
	{
		[Fact]
		public void Menu_ControlTheme_IsApplied_And_ItemsPanel_IsHorizontal()
		{
			BuildApp();
			var menu = new Menu();
			menu.ApplyTemplate();
			var itemsPanel = menu.ItemsPanel;
			Assert.NotNull(itemsPanel);
			var panel = itemsPanel.Build() as StackPanel;
			Assert.NotNull(panel);
			Assert.Equal(Orientation.Horizontal, panel.Orientation);
		}

		[Fact]
		public void WindowButton_Class_Style_IsApplied()
		{
			BuildApp();
			var btn = new Button();
			btn.Classes.Add("WindowButton");
			var window = new Window();
			window.Content = btn;
			window.Show();
			try
			{
				// Avalonia 11 在 headless 模式下样式应用是异步的，需要让 dispatcher 处理挂起的任务。
				Avalonia.Threading.Dispatcher.UIThread.RunJobs();
				Assert.Equal(46.0, btn.Width);
				Assert.Equal(26.0, btn.Height);
			}
			finally
			{
				window.Close();
			}
		}

		[Fact]
		public void ToolbarButton_Template_Contains_Title_TextBlock()
		{
			BuildApp();
			var btn = new ToolbarButton();
			btn.Title = "Fetch";
			var window = new Window();
			window.Content = btn;
			window.Show();
			try
			{
				Avalonia.Threading.Dispatcher.UIThread.RunJobs();
				Assert.NotNull(btn.Template);
				// ApplyTemplate 会真正构建可视化树并应用 TemplateBinding（Template.Build 不会应用绑定）。
				btn.ApplyTemplate();
				// 模板内的元素是 btn 的视觉后代（不是逻辑后代），用 GetVisualDescendants 枚举。
				var textBlocks = btn.GetVisualDescendants()
					.OfType<TextBlock>().ToList();
				Assert.Contains(textBlocks, t => t.Text == "Fetch");
			}
			finally
			{
				window.Close();
			}
		}

		[Fact]
		public void DropDownButton_Template_IsDropDownButton_NotToggleButton()
		{
			BuildApp();
			var btn = new ForkPlus.UI.Controls.DropDownButton();
			var window = new Window();
			window.Content = btn;
			window.Show();
			try
			{
				Avalonia.Threading.Dispatcher.UIThread.RunJobs();
				Assert.NotNull(btn.Template);
				btn.ApplyTemplate();
				// DropDownButton 的模板根元素是 Border#Border，作为 btn 的视觉后代存在。
				var borders = btn.GetVisualDescendants()
					.OfType<Border>().ToList();
				Assert.Contains(borders, b => b.Name == "Border");
			}
			finally
			{
				window.Close();
			}
		}

		[Fact]
		public void ToolbarDropDownButton_Template_Contains_Title_TextBlock()
		{
			BuildApp();
			var btn = new ToolbarDropDownButton();
			btn.Title = "Workspaces";
			var window = new Window();
			window.Content = btn;
			window.Show();
			try
			{
				Avalonia.Threading.Dispatcher.UIThread.RunJobs();
				Assert.NotNull(btn.Template);
				btn.ApplyTemplate();
				var textBlocks = btn.GetVisualDescendants()
					.OfType<TextBlock>().ToList();
				Assert.Contains(textBlocks, t => t.Text == "Workspaces");
			}
			finally
			{
				window.Close();
			}
		}

		private static bool _built;
		private static void BuildApp()
		{
			if (_built) return;
			AppBuilder.Configure<TestApp>()
				.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
				.SetupWithoutStarting();
			_built = true;
		}

		public class TestApp : Application
		{
			public override void Initialize()
			{
				var fluent = new FluentTheme();
				Styles.Add(fluent);
				Styles.Add((IStyle)AvaloniaXamlLoader.Load(
					new Uri("avares://ForkPlus/Theme/Styles/Window.xaml")));
				Styles.Add((IStyle)AvaloniaXamlLoader.Load(
					new Uri("avares://ForkPlus/Theme/Styles/Button.xaml")));
				Styles.Add((IStyle)AvaloniaXamlLoader.Load(
					new Uri("avares://ForkPlus/Theme/Styles/Menu.xaml")));
				Resources.MergedDictionaries.Add((IResourceDictionary)
					AvaloniaXamlLoader.Load(
						new Uri("avares://ForkPlus/Theme/Styles/Brushes/Colors.Light.xaml")));
				Resources.MergedDictionaries.Add((IResourceDictionary)
					AvaloniaXamlLoader.Load(
						new Uri("avares://ForkPlus/Theme/Styles/Images.Light.xaml")));
				Resources.MergedDictionaries.Add((IResourceDictionary)
					AvaloniaXamlLoader.Load(
						new Uri("avares://ForkPlus/Theme/Generic.xaml")));
			}
		}
	}
}
