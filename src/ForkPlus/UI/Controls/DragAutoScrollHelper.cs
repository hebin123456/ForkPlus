using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Input;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.Controls
{
	public class DragAutoScrollHelper
	{
		private const double EdgeThreshold = 25.0;

		private readonly ItemsControl _control;

		private DispatcherTimer _timer;

		private int _scrollDirection;

		public DragAutoScrollHelper(ItemsControl control)
		{
			_control = control;
			// Migration note：WPF 拖放事件属性（DragOver/DragLeave/Drop）→ Avalonia DragDrop 静态路由事件订阅。
			global::Avalonia.Input.DragDrop.AddDragOverHandler(_control, OnDragOver);
			global::Avalonia.Input.DragDrop.AddDragLeaveHandler(_control, OnDragLeave);
			global::Avalonia.Input.DragDrop.AddDropHandler(_control, OnDrop);
		}

		private void OnDragOver(object sender, DragEventArgs e)
		{
			Point position = e.GetPosition(_control);
			if (position.Y < 25.0)
			{
				StartAutoScroll(-1);
			}
			else if (position.Y > _control.Bounds.Height - 25.0)
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
					scrollViewer.LineUp();
				}
				else if (_scrollDirection > 0)
				{
					scrollViewer.LineDown();
				}
			}
		}

		private ScrollViewer GetScrollViewer()
		{
			return ScrollViewerHelper.FindScrollViewer(_control);
		}
	}
}
