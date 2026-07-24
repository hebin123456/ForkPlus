using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ForkPlus.UI.Controls
{
	// 阶段 4.5：WPF System.Windows.* → Avalonia.*。
	// WPF VisualTreeHelper.GetChildrenCount/GetChild → Avalonia GetVisualDescendants().OfType<ScrollViewer>()。
	// WPF CLR 事件 DragOver/DragLeave/Drop (control.DragOver +=) → Avalonia RoutedEvent AddHandler(DragDrop.XxxEvent, handler)。
	// WPF DispatcherTimer → Avalonia.Threading.DispatcherTimer（API 兼容）。
	// WPF ItemsControl.ActualHeight → Avalonia Bounds.Height。
	public class DragAutoScrollHelper
	{
		private const double EdgeThreshold = 25.0;

		private readonly ItemsControl _control;

		private DispatcherTimer _timer;

		private int _scrollDirection;

		public DragAutoScrollHelper(ItemsControl control)
		{
			_control = control;
			// 阶段 4.5：Avalonia 拖拽事件为 RoutedEvent，需用 AddHandler 订阅（无 CLR 事件包装）。
			_control.AddHandler(DragDrop.DragOverEvent, OnDragOver);
			_control.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
			_control.AddHandler(DragDrop.DropEvent, OnDrop);
		}

		private void OnDragOver(object sender, DragEventArgs e)
		{
			Point position = e.GetPosition(_control);
			if (position.Y < EdgeThreshold)
			{
				StartAutoScroll(-1);
			}
			else if (position.Y > _control.Bounds.Height - EdgeThreshold)
			{
				StartAutoScroll(1);
			}
			else
			{
				StopAutoScroll();
			}
		}

		private void OnDragLeave(object sender, DragEventArgs e)
		{
			StopAutoScroll();
		}

		private void OnDrop(object sender, DragEventArgs e)
		{
			StopAutoScroll();
		}

		private void StartAutoScroll(int direction)
		{
			_scrollDirection = direction;
			if (_timer == null)
			{
				_timer = new DispatcherTimer();
				_timer.Interval = TimeSpan.FromMilliseconds(50.0);
				_timer.Tick += OnTimerTick;
			}
			if (!_timer.IsEnabled)
			{
				_timer.Start();
			}
		}

		public void StopAutoScroll()
		{
			_scrollDirection = 0;
			_timer?.Stop();
		}

		private void OnTimerTick(object sender, EventArgs e)
		{
			ScrollViewer scrollViewer = GetScrollViewer();
			if (scrollViewer != null)
			{
				if (_scrollDirection < 0)
				{
					// TODO(4.5): 验证 Avalonia ScrollViewer.LineUp 是否存在；如不存在改用 Offset 调整。
					scrollViewer.LineUp();
				}
				else if (_scrollDirection > 0)
				{
					scrollViewer.LineDown();
				}
			}
		}

		// 阶段 4.5：WPF VisualTreeHelper 逐层查找 Border→ScrollViewer → Avalonia GetVisualDescendants 一次性查找。
		private ScrollViewer GetScrollViewer()
		{
			return _control.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
		}
	}
}
