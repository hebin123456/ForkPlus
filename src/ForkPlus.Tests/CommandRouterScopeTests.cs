// 回归测试：CommandRouter 还原 WPF CommandBindings 语义
// （bug：任意界面按 Enter 弹出提交独立窗口、搜索框 Enter 被全局手势劫持）。
// WPF 原语义：
//   1. CommandBinding 挂在哪个控件上，仅当事件源位于该控件子树内才触发
//      （Window 级绑定覆盖全窗口）；
//   2. 手势在 KeyDown 冒泡结束后才翻译，已被控件标记 Handled 的按键不触发命令。
using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.UI.Commands;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class CommandRouterScopeTests
	{
		private sealed class TestCommand : IUICommand
		{
			public string Title => "Test";
			public KeyGesture Shortcut { get; } = new KeyGesture(Key.Return);
			public KeyGesture SecondaryShortcut => null;
		}

		private static KeyEventArgs EnterArgs() => new KeyEventArgs
		{
			RoutedEvent = InputElement.KeyDownEvent,
			Key = Key.Return
		};

		[Fact]
		public void CommandRouter_Scope_And_Handled_Semantics()
		{
			string report = HeadlessAppBootstrap.Run(delegate
			{
				// 结构：Window > root > (hostPanel > insideTextBox + searchTextBox) + outsideTextBox
				var window = new Window { Width = 400, Height = 300 };
				var hostPanel = new StackPanel();
				var insideTextBox = new TextBox();
				var searchTextBox = new TextBox();
				var outsideTextBox = new TextBox();
				hostPanel.Children.Add(insideTextBox);
				hostPanel.Children.Add(searchTextBox);
				var root = new StackPanel();
				root.Children.Add(hostPanel);
				root.Children.Add(outsideTextBox);
				window.Content = root;
				window.Show();
				Dispatcher.UIThread.RunJobs();

				// 模拟搜索框：隧道阶段消费 Enter（与 RevisionSearchPanelUserControl 相同注册方式）
				searchTextBox.AddHandler(InputElement.KeyDownEvent, delegate (object s, KeyEventArgs e)
				{
					if (e.Key == Key.Return) e.Handled = true;
				}, RoutingStrategies.Tunnel);

				var cmd = new TestCommand();
				int fired = 0;
				hostPanel.AddCommandBinding(cmd.CreateShortcutCommandBinding(delegate
				{
					fired++;
				}));

				// 1) 宿主子树外按键：不得触发（旧实现会触发 → 全局 Enter bug）
				outsideTextBox.Focus();
				outsideTextBox.RaiseEvent(EnterArgs());
				int outsideFired = fired;

				// 2) 宿主子树内普通控件按键：应触发
				insideTextBox.Focus();
				insideTextBox.RaiseEvent(EnterArgs());
				int insideFired = fired;

				// 3) 宿主子树内但控件已消费按键：不得触发（搜索框语义）
				searchTextBox.Focus();
				searchTextBox.RaiseEvent(EnterArgs());
				int handledFired = fired;

				return $"outside={outsideFired},inside={insideFired},handled={handledFired}";
			});
			// outside=0（作用域外不触发）、inside=1（作用域内触发）、handled=1（已消费不触发）
			Assert.Equal("outside=0,inside=1,handled=1", report);
		}

		[Fact]
		public void CommandRouter_Window_Level_Binding_Fires_Anywhere_In_Window()
		{
			string report = HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 400, Height = 300 };
				var panel = new StackPanel();
				var textBox = new TextBox();
				panel.Children.Add(textBox);
				window.Content = panel;
				window.Show();
				Dispatcher.UIThread.RunJobs();

				var cmd = new TestCommand();
				int fired = 0;
				window.AddCommandBinding(cmd.CreateShortcutCommandBinding(delegate
				{
					fired++;
				}));

				textBox.Focus();
				textBox.RaiseEvent(EnterArgs());
				return "fired=" + fired;
			});
			Assert.Equal("fired=1", report);
		}

		[Fact]
		public void CommandRouter_Nested_Host_Wins_Over_Outer_Host()
		{
			string report = HeadlessAppBootstrap.Run(delegate
			{
				// 结构：Window > outerPanel > innerPanel > textBox，两级宿主注册不同标记
				var window = new Window { Width = 400, Height = 300 };
				var outerPanel = new StackPanel();
				var innerPanel = new StackPanel();
				var textBox = new TextBox();
				innerPanel.Children.Add(textBox);
				outerPanel.Children.Add(innerPanel);
				window.Content = outerPanel;
				window.Show();
				Dispatcher.UIThread.RunJobs();

				var cmd = new TestCommand();
				string winner = null;
				innerPanel.AddCommandBinding(cmd.CreateShortcutCommandBinding(delegate
				{
					winner = "inner";
				}));
				outerPanel.AddCommandBinding(cmd.CreateShortcutCommandBinding(delegate
				{
					winner = "outer";
				}));

				textBox.Focus();
				textBox.RaiseEvent(EnterArgs());
				return "winner=" + (winner ?? "none");
			});
			// WPF 从焦点向上路由：最近（最深）宿主先命中
			Assert.Equal("winner=inner", report);
		}
	}
}
