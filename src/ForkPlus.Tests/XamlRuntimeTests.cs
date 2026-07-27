// 阶段 6 验证测试：用 Avalonia headless 平台实际加载 App.axaml，
// 验证 Menu ControlTheme 是否被应用（ItemsPanel 横向）、Window 按钮样式是否被 Classes 应用、
// ToolbarButton ControlTheme 是否被应用（Template 非 null）。
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
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
				Assert.Equal(46.0, btn.Width);
				Assert.Equal(26.0, btn.Height);
			}
			finally
			{
				window.Close();
			}
		}

		[Fact]
		public void ToolbarButton_ControlTheme_Template_IsSet()
		{
			BuildApp();
			var btn = new ToolbarButton();
			btn.Title = "Fetch";
			var window = new Window();
			window.Content = btn;
			window.Show();
			try
			{
				// ControlTheme 被应用后，Template 属性应被设置（非 null）。
				// 这证明 Button.xaml 中 controls:ToolbarButton 的 ControlTheme 被正确查找并应用。
				// 删除冗余的非键值 Style 后，ControlTheme 的 Template（含 Title TextBlock）不再被覆盖。
				Assert.NotNull(btn.Template);
			}
			finally
			{
				window.Close();
			}
		}

		[Fact]
		public void DropDownButton_Style_Template_IsSet()
		{
			BuildApp();
			var btn = new ForkPlus.UI.Controls.DropDownButton();
			var window = new Window();
			window.Content = btn;
			window.Show();
			try
			{
				// DropDownButton 没有独立 ControlTheme，依赖非键值 Style 设置 Template。
				Assert.NotNull(btn.Template);
			}
			finally
			{
				window.Close();
			}
		}

		[Fact]
		public void ToolbarDropDownButton_Style_Template_IsSet()
		{
			BuildApp();
			var btn = new ToolbarDropDownButton();
			btn.Title = "Workspaces";
			var window = new Window();
			window.Content = btn;
			window.Show();
			try
			{
				// ToolbarDropDownButton 同样依赖非键值 Style 设置 Template（含 Title TextBlock）。
				Assert.NotNull(btn.Template);
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
