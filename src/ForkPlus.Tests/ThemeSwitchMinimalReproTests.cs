// 最小复现（2026-09-07，主题切换崩溃定位）：隔离 ModernTabControl 在主题切换（字典换装 +
// RequestedThemeVariant 同步）下是否触发 "Grid already has a visual parent
// ContentPresenter(PART_SelectedContentHost)" 崩溃。
using System;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI;
using ForkPlus.UI.Controls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ThemeSwitchMinimalReproTests
	{
		private static Window CreateTabWindow()
		{
			var tab1 = new TabItem { Header = "Tab1", Content = new Grid() };
			var tab2 = new TabItem { Header = "Tab2", Content = new Grid() };
			var tabControl = new ModernTabControl { Width = 400, Height = 300 };
			tabControl.Items.Add(tab1);
			tabControl.Items.Add(tab2);
			return new Window { Width = 500, Height = 400, Content = tabControl };
		}

		[Fact]
		public void MinimalModernTabControl_ThemeSwitch_NoCrash()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = CreateTabWindow();
				window.Show();
				Dispatcher.UIThread.RunJobs();
				try
				{
					MainWindow.Commands.SwitchApplicationTheme.Execute(ThemeType.Dark);
					Dispatcher.UIThread.RunJobs();
					MainWindow.Commands.SwitchApplicationTheme.Execute(ThemeType.Light);
					Dispatcher.UIThread.RunJobs();
				}
				finally
				{
					window.Close();
				}
			});
		}

		[Fact]
		public void MinimalModernTabControl_OnlyVariantSwitch_NoCrash()
		{
			// 只切 RequestedThemeVariant（不动皮肤字典）——区分两种失效源。
			HeadlessAppBootstrap.Run(delegate
			{
				var window = CreateTabWindow();
				window.Show();
				Dispatcher.UIThread.RunJobs();
				try
				{
					Avalonia.Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
					Dispatcher.UIThread.RunJobs();
					Avalonia.Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
					Dispatcher.UIThread.RunJobs();
				}
				finally
				{
					window.Close();
				}
			});
		}
	}
}
