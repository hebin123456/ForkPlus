// 截图验证测试：用 Avalonia headless 模式渲染关键控件并保存为 PNG，
// 让用户可以直观看到按钮样式/模板是否真的生效。
using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	public class ScreenshotTests
	{
		private static bool _built;
		private static string _outDir;

		private static void BuildApp()
		{
			if (_built) return;
			_outDir = Environment.GetEnvironmentVariable("FP_SCREENSHOT_DIR")
				?? "/tmp/fp-screenshots";
			Directory.CreateDirectory(_outDir);
			AppBuilder.Configure<ScreenshotApp>()
				.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
				.SetupWithoutStarting();
			_built = true;
		}

		[Fact]
		public void Screenshot_ToolbarButton_Appearance()
		{
			BuildApp();
			var btn = new ToolbarButton();
			btn.Title = "Fetch";
			btn.Content = new TextBlock { Text = "F", FontSize = 14 };
			btn.Width = 80;
			btn.Height = 60;
			var window = new Window
			{
				Width = 200,
				Height = 120,
				Content = btn
			};
			window.Show();
			Avalonia.Threading.Dispatcher.UIThread.RunJobs();
			btn.ApplyTemplate();
			Avalonia.Threading.Dispatcher.UIThread.RunJobs();
			SavePng(window, "toolbarbutton.png");
			window.Close();
		}

		[Fact]
		public void Screenshot_DropDownButton_Appearance()
		{
			BuildApp();
			var btn = new ForkPlus.UI.Controls.DropDownButton();
			btn.Content = new TextBlock { Text = "▼", FontSize = 10 };
			btn.Width = 30;
			btn.Height = 60;
			var window = new Window
			{
				Width = 150,
				Height = 120,
				Content = btn
			};
			window.Show();
			Avalonia.Threading.Dispatcher.UIThread.RunJobs();
			btn.ApplyTemplate();
			Avalonia.Threading.Dispatcher.UIThread.RunJobs();
			SavePng(window, "dropdownbutton.png");
			window.Close();
		}

		[Fact]
		public void Screenshot_ToolbarDropDownButton_WithTitle()
		{
			BuildApp();
			var btn = new ToolbarDropDownButton();
			btn.Title = "Workspaces";
			btn.Content = new TextBlock { Text = "W", FontSize = 14 };
			btn.Width = 100;
			btn.Height = 60;
			var window = new Window
			{
				Width = 200,
				Height = 120,
				Content = btn
			};
			window.Show();
			Avalonia.Threading.Dispatcher.UIThread.RunJobs();
			btn.ApplyTemplate();
			Avalonia.Threading.Dispatcher.UIThread.RunJobs();
			SavePng(window, "toolbardropdownbutton.png");
			window.Close();
		}

		[Fact]
		public void Screenshot_WindowButton_Class()
		{
			BuildApp();
			var btn = new Button();
			btn.Classes.Add("WindowButton");
			btn.Content = new TextBlock { Text = "_", FontSize = 12 };
			var window = new Window
			{
				Width = 150,
				Height = 100,
				Content = btn
			};
			window.Show();
			Avalonia.Threading.Dispatcher.UIThread.RunJobs();
			SavePng(window, "windowbutton.png");
			window.Close();
		}

		[Fact]
		public void Screenshot_PlainButton_Default()
		{
			BuildApp();
			var btn = new Button();
			btn.Content = "OK";
			btn.Width = 80;
			btn.Height = 30;
			var window = new Window
			{
				Width = 150,
				Height = 100,
				Content = btn
			};
			window.Show();
			Avalonia.Threading.Dispatcher.UIThread.RunJobs();
			SavePng(window, "plainbutton.png");
			window.Close();
		}

		private static void SavePng(Window window, string fileName)
		{
			// Avalonia 11: RenderTargetBitmap.Render(control) 渲染到 bitmap（不是 control.Render）。
			var pixelSize = new PixelSize((int)window.Width, (int)window.Height);
			using var bmp = new RenderTargetBitmap(pixelSize);
			bmp.Render(window);
			var path = Path.Combine(_outDir, fileName);
			// 用 FileStream 显式保存（bmp.Save(string) 在部分 headless 环境下会跳过磁盘写入）。
			using (var fs = File.Create(path))
			{
				bmp.Save(fs);
			}
			Console.WriteLine($"Screenshot saved: {path} (exists={File.Exists(path)}, size={new FileInfo(path).Length})");
		}

		public class ScreenshotApp : Application
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
