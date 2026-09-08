// 探针4：验证 MultiselectionTreeView.OnDoubleTapped 清空 LastClickedItem 的时机
// 与 DoubleTapped 订阅者的执行顺序（问题2双击穿梭失效的根因验证）
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.UI;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	// 最小化测试用 MultiselectionTreeView 子类，记录调用顺序
	internal class ProbeTreeView : MultiselectionTreeView
	{
		public static List<string> Order = new List<string>();

		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			Order.Add("tree.OnPointerPressed(begin)");
			base.OnPointerPressed(e);
			Order.Add("tree.OnPointerPressed(end)");
		}

		protected override void OnDoubleTapped(TappedEventArgs e)
		{
			Order.Add("tree.OnDoubleTapped(begin)");
			base.OnDoubleTapped(e);
			Order.Add("tree.OnDoubleTapped(end)");
		}
	}

	[Collection("HeadlessAvalonia")]
	public class DoubleTapProbeTests
	{
		[Fact]
		public void Probe_DoubleTapped_Order()
		{
			string report = HeadlessAppBootstrap.Run(delegate
			{
				var sb = new System.Text.StringBuilder();
				try
				{
					ProbeTreeView.Order.Clear();
					var tree = new ProbeTreeView();
					var root = new FileListItem(new ChangedFile("", staged: false), "", null);
					var child = new FileListItem(new ChangedFile("b.txt", staged: false), "b.txt", null);
					root.Children.Add(child);
					tree.RootItem = root;
					root.IsExpanded = true;

					// 模拟 FileListUserControl 的订阅方式
					tree.DoubleTapped += delegate (object s, TappedEventArgs e)
					{
						sb.AppendLine($"subscriber fired: LastClickedItem = {tree.LastClickedItem?.GetType().Name ?? "null"}");
						ProbeTreeView.Order.Add("subscriber.DoubleTapped");
					};

					var window = new Window { Width = 400, Height = 300, Content = tree };
					window.Show();
					Dispatcher.UIThread.RunJobs();

					// 模拟双击：headless 直接 RaiseEvent DoubleTappedEvent（Gestures）
					// 先手动设置 LastClickedItem（模拟 PointerPressed 命中）
					// TappedEventArgs 公有构造（Avalonia 12）：RoutedEvent + PointerEventArgs
					var pointerArgs = new PointerEventArgs(
						Avalonia.Input.InputElement.PointerMovedEvent, tree,
						new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), Avalonia.Input.PointerType.Mouse, true),
						window, new Point(10, 10), (ulong)Environment.TickCount64,
						new PointerPointProperties(Avalonia.Input.RawInputModifiers.None, PointerUpdateKind.Other),
						Avalonia.Input.KeyModifiers.None);
					var tappedArgs = new TappedEventArgs(InputElement.DoubleTappedEvent, pointerArgs) { Source = tree };
					tree.RaiseEvent(tappedArgs);
					Dispatcher.UIThread.RunJobs();

					sb.AppendLine("order: " + string.Join(" -> ", ProbeTreeView.Order));
					sb.AppendLine($"after all: LastClickedItem = {tree.LastClickedItem?.GetType().Name ?? "null"}");
					window.Close();
				}
				catch (Exception e)
				{
					sb.AppendLine("EXCEPTION: " + e.GetType().Name + ": " + e.Message);
				}
				return sb.ToString();
			});
			System.IO.File.WriteAllText("/tmp/doubletap_probe.txt", report);
			Assert.True(true, report);
		}
	}
}
