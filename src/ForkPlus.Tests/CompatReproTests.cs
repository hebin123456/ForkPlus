// 最小复现：ContextMenuCompat.AddContextMenuOpeningHandler 订阅后，
// 直接 RaiseEvent(ContextRequested) 是否触发 handler。隔离 case4 失败根因。
using System;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class CompatReproTests
	{
		[Fact]
		public void Compat_RaiseEvent_Fires_On_Button()
		{
			string report = HeadlessAppBootstrap.Run(delegate
			{
				var sb = new System.Text.StringBuilder();
				var window = new Window { Width = 300, Height = 200 };
				window.Show();
				var button = new Button { Content = "b" };
				window.Content = button;
				Dispatcher.UIThread.RunJobs();

				var menu = new ContextMenu();
				button.ContextMenu = menu;
				bool fired = false;
				ContextMenuCompat.AddContextMenuOpeningHandler(button, delegate (object s, ContextMenuEventArgs e)
				{
					fired = true;
				});
				// 对照1：绕过 compat，原生 CLR 事件直接订阅
				bool nativeFired = false;
				button.ContextRequested += delegate (object s, global::Avalonia.Input.ContextRequestedEventArgs e)
				{
					nativeFired = true;
				};
				// 对照2：AddHandler + 显式 Bubble
				bool bubbleFired = false;
				button.AddHandler(global::Avalonia.Input.InputElement.ContextRequestedEvent,
					delegate (object s, global::Avalonia.Input.ContextRequestedEventArgs e)
					{
						bubbleFired = true;
					}, global::Avalonia.Interactivity.RoutingStrategies.Bubble);
				// 对照3：不同路由事件（Tapped，Bubble only）
				bool tappedFired = false;
				button.AddHandler(global::Avalonia.Input.InputElement.TappedEvent,
					delegate (object s, global::Avalonia.Input.TappedEventArgs e)
					{
						tappedFired = true;
					}, global::Avalonia.Interactivity.RoutingStrategies.Bubble);
				// 对照4：Button.Click（Direct|Bubble 的 routed event）
				bool clickFired = false;
				button.Click += delegate (object s, global::Avalonia.Interactivity.RoutedEventArgs e) { clickFired = true; };

				// 对照5：KeyDown（同为 Tunnel|Bubble 策略）
				bool keyFired = false;
				button.AddHandler(global::Avalonia.Input.InputElement.KeyDownEvent,
					delegate (object s, global::Avalonia.Input.KeyEventArgs e)
					{
						keyFired = true;
					}, global::Avalonia.Interactivity.RoutingStrategies.Bubble);
				// 对照6：handledEventsToo 订阅 ContextRequested——若它触发，说明 Tunnel 阶段有人 set Handled
				bool handledTooFired = false;
				button.AddHandler(global::Avalonia.Input.InputElement.ContextRequestedEvent,
					delegate (object s, global::Avalonia.Input.ContextRequestedEventArgs e)
					{
						handledTooFired = true;
					}, global::Avalonia.Interactivity.RoutingStrategies.Bubble, true);

				sb.AppendLine("attached=" + (window.Content == button) + ", ContextMenu=" + (button.ContextMenu != null));

				var ctxArgs = new global::Avalonia.Input.ContextRequestedEventArgs();
				button.RaiseEvent(ctxArgs);
				button.RaiseEvent(new global::Avalonia.Input.KeyEventArgs
				{
					RoutedEvent = global::Avalonia.Input.InputElement.KeyDownEvent
				});
				button.RaiseEvent(new global::Avalonia.Input.TappedEventArgs(global::Avalonia.Input.InputElement.TappedEvent, null));
				button.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(global::Avalonia.Controls.Button.ClickEvent));
				Dispatcher.UIThread.RunJobs();

				sb.AppendLine("compat=" + fired + ", native=" + nativeFired + ", bubble=" + bubbleFired
					+ ", handledToo=" + handledTooFired + ", key=" + keyFired
					+ ", tapped=" + tappedFired + ", click=" + clickFired
					+ ", ctxArgs.Handled=" + ctxArgs.Handled);
				return sb.ToString();
			});
			System.IO.File.WriteAllText("/tmp/compat_repro_button.txt", report);
			// compat（Tunnel 订阅）必须触发；native/bubble 对照组保持 False 是 bug 证据（内置
			// ControlContextRequested 先于它们置 Handled），handledToo=True 佐证该机制。
			Assert.Contains("compat=True", report);
			Assert.Contains("handledToo=True", report);
			Assert.Contains("native=False", report);
			Assert.Contains("bubble=False", report);
			Assert.Contains("ctxArgs.Handled=True", report);
		}

		[Fact]
		public void Compat_RaiseEvent_Fires_On_CommitCodeEditor()
		{
			string report = HeadlessAppBootstrap.Run(delegate
			{
				var sb = new System.Text.StringBuilder();
				var window = new Window { Width = 900, Height = 400 };
				window.Show();
				var editor = new CommitCodeEditor(DiffViewMode.Split);
				window.Content = editor;
				Dispatcher.UIThread.RunJobs();

				var menu = new ContextMenu();
				editor.ContextMenu = menu;
				bool fired = false;
				ContextMenuCompat.AddContextMenuOpeningHandler(editor, delegate (object s, ContextMenuEventArgs e)
				{
					fired = true;
				});
				sb.AppendLine("subscription done, ContextMenu=" + (editor.ContextMenu != null));

				// 1) 直接在编辑器上 raise
				editor.RaiseEvent(new global::Avalonia.Input.ContextRequestedEventArgs());
				sb.AppendLine("after editor-raise: fired=" + fired);

				// 2) 从 TextView raise（真实右键路径的等价物）
				editor.TextArea.TextView.RaiseEvent(new global::Avalonia.Input.ContextRequestedEventArgs());
				sb.AppendLine("after textview-raise: fired=" + fired);
				return sb.ToString();
			});
			System.IO.File.WriteAllText("/tmp/compat_repro_editor.txt", report);
			Assert.Contains("after editor-raise: fired=True", report);
			Assert.Contains("after textview-raise: fired=True", report);
		}
	}
}
