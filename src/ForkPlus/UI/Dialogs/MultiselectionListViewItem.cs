using System;
using ForkPlus.UI.WpfCompat;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using ForkPlus.UI.Helpers;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace ForkPlus.UI.Dialogs
{
	public class MultiselectionListViewItem : global::Avalonia.Controls.ListBoxItem
	{
		private bool _wasSelected;

		private Point _dragStartPoint;

		private DragAndDropListBoxAdorner _adorner;

		private DropPlaceAdorner _dropAdorner;

		public MultiselectionListView ParentListView { get; internal set; }

		public DropPosition DropPosition { get; internal set; }

		protected override void OnPointerPressed(global::Avalonia.Input.PointerPressedEventArgs e)
		{
			// WPF 语义对齐：ComboBox / 按钮 / 文本框等内嵌交互控件在 WPF 下会把
			// MouseLeftButtonDown 标记 Handled（ButtonBase 以 handledEventsToo:true 注册类处理器
			// 且置 Handled），事件不再到达 ListViewItem，选择与拖拽捕获逻辑完全不执行；
			// Avalonia 下这些控件不标记 Handled，事件继续冒泡，item 无条件 Capture 抢走指针
			// → ComboBox 模板里的 ToggleButton 收不到 PointerReleased → Click 不触发 →
			// 下拉永远打不开（交互式变基窗口"不能更改类型"根因）。
			// 修复：命中源位于内嵌交互控件内时，跳过整个按压处理（含捕获与选择）。
			if (IsPressOnEmbeddedInteractiveControl(e))
			{
				return;
			}
			_wasSelected = base.IsSelected;
			if (!base.IsSelected)
			{
				base.OnPointerPressed(e);
			}
			if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			{
				_dragStartPoint = e.GetPosition(null);
				e.Pointer.Capture(this);
			}
		}

		/// <summary>
		/// 按压命中源（e.Source）到本 item 的可视树路径上是否经过内嵌交互控件。
		/// ComboBox 的 ToggleButton / 模板内部元素都算（沿 VisualTree 向上遍历到 this）。
		/// </summary>
		private bool IsPressOnEmbeddedInteractiveControl(global::Avalonia.Input.PointerPressedEventArgs e)
		{
			for (Visual v = e.Source as Visual; v != null && v != this; v = v.GetVisualParent())
			{
				if (v is ComboBox || v is Button || v is CheckBox || v is TextBox || v is Slider)
				{
					return true;
				}
			}
			return false;
		}

		protected override void OnPointerReleased(global::Avalonia.Input.PointerReleasedEventArgs e)
		{
			if (e.Pointer.Captured == this)
			{
				e.Pointer.Capture(null);
			}
			if (_wasSelected)
			{
				IsSelected = true;
			}
		}

		protected override void OnDoubleTapped(global::Avalonia.Input.TappedEventArgs e)
		{
			e.Handled = true;
			base.OnDoubleTapped(e);
		}

		protected override void OnPointerMoved(global::Avalonia.Input.PointerEventArgs e)
		{
			if (e.Pointer.Captured != this)
			{
				return;
			}
			Point position = e.GetPosition(null);
			if (!ExceedDragDistance(_dragStartPoint - position))
			{
				return;
			}
			RevisionEntry[] array = ParentListView.SelectedItems.CompactMap((object x) => x as RevisionEntry);
			if (array.Length < 1)
			{
				return;
			}
			global::Avalonia.Controls.ListBoxItem[] array2 = array.CompactMap((RevisionEntry x) => ParentListView.ContainerFromItem(x) as global::Avalonia.Controls.ListBoxItem);
			ListBoxItem[] listBoxItems = array2;
			_adorner = new DragAndDropListBoxAdorner(this, listBoxItems, e.GetPosition(this));
			if (_adorner != null)
			{
				AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(ParentListView);
				if (adornerLayer != null)
				{
					adornerLayer.Add(_adorner);
					global::ForkPlus.UI.WpfCompat.DragDropLauncher.DoDragDrop(this, array, DragDropEffects.Move);
					adornerLayer.Remove(_adorner);
				}
			}
		}

		protected void OnGiveFeedback(GiveFeedbackEventArgs e)
		{
			if (base.IsVisible && _adorner != null)
			{
				Point position = this.PointFromScreen(MouseHelper.GetMousePosition());
				_adorner.UpdatePosition(position);
			}
		}

		private static bool ExceedDragDistance(Vector diff)
		{
			if (!(Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance))
			{
				return Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance;
			}
			return true;
		}

		protected void OnDragEnter(DragEventArgs e)
		{
			ClearDropAdorner();
			DropPosition = GetDropPositoion(e);
			ShowDropAdorner(DropPosition);
		}

		protected void OnDrop(DragEventArgs e)
		{
			ClearDropAdorner();
		}

		protected void OnDragLeave(DragEventArgs e)
		{
			ClearDropAdorner();
		}

		private DropPosition GetDropPositoion(DragEventArgs e)
		{
			double y = e.GetPosition(this).Y;
			double actualHeight = base.Bounds.Height;
			if (!(y < actualHeight / 2.0))
			{
				return DropPosition.Bottom;
			}
			return DropPosition.Top;
		}

		private void ShowDropAdorner(DropPosition dropPosition)
		{
			_dropAdorner = new DropPlaceAdorner(this, dropPosition);
			if (_dropAdorner != null)
			{
				AdornerLayer.GetAdornerLayer(ParentListView)?.Add(_dropAdorner);
			}
		}

		private void ClearDropAdorner()
		{
			if (_dropAdorner != null)
			{
				AdornerLayer.GetAdornerLayer(ParentListView)?.Remove(_dropAdorner);
			}
		}
	}
}
