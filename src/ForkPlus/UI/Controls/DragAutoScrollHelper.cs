using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

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
			_control.DragOver += OnDragOver;
			_control.DragLeave += OnDragLeave;
			_control.Drop += OnDrop;
		}

		private void OnDragOver(object sender, DragEventArgs e)
		{
			Point position = e.GetPosition(_control);
			if (position.Y < 25.0)
			{
				StartAutoScroll(-1);
			}
			else if (position.Y > _control.ActualHeight - 25.0)
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
			if (VisualTreeHelper.GetChildrenCount(_control) == 0)
			{
				return null;
			}
			if (!(VisualTreeHelper.GetChild(_control, 0) is Border reference))
			{
				return null;
			}
			return VisualTreeHelper.GetChild(reference, 0) as ScrollViewer;
		}
	}
}
