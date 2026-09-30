using System;
using ForkPlus.UI.WpfCompat;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.Helpers;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.UserControls.Preferences
{
	public class DragAndDropListBoxItem : ListBoxItem
	{
		/// <summary>进程内拖放 payload 的字符串 key（object[] 无法跨进程序列化，走
		/// WpfDataObject.RuntimePayload 直通表；与 MultiselectionListViewItem.DragItemsFormat 同款，
		/// 见 EditCustomCommandUIControlsWindow.ListBoxItem_Drop 读取侧）。</summary>
		public static readonly string DragItemsFormat = "ForkPlusCiRows";

		private bool _wasSelected;

		private Point _dragStartPoint;

		private DragAndDropListBoxAdorner _adorner;

		private DropPlaceAdorner _dropAdorner;

		// 修复（2026-09-30，"拖拽排序完全无反应"）：WPF 原版行样式里 AllowDrop Setter + OnDragEnter/
		// OnDragLeave/OnDrop 虚方法重写（框架调用）；迁移时 Setter 被删（axaml 注释声称"Avalonia 无该
		// 属性"——实为 DragDrop 附加属性）、虚方法降级成无人调用的普通方法——行容器既不接受放置、
		// 拖放指示线也永不显示。参照 DragAndDropListViewItem 构造函数补齐 AllowDrop 与类级接线。
		public DragAndDropListBoxItem()
		{
			DragDrop.SetAllowDrop(this, true);
			AddHandler(DragDrop.DragEnterEvent, (_, e) => OnDragEnter(e));
			AddHandler(DragDrop.DragOverEvent, (_, e) => OnDragEnter(e));
			AddHandler(DragDrop.DragLeaveEvent, (_, e) => OnDragLeave(e));
			AddHandler(DragDrop.DropEvent, (_, e) => OnDrop(e));
		}

		// 修复（2026-09-30，同上）：记录本手势的按下参数，供 OnPointerMoved 用
		// DragDropLauncher.DoDragDrop(press, ...) 重载一次发起（同 ClosableTabItem 修复模式，
		// 避开 ConditionalWeakTable 两段式"首次手势被吞"的问题）。
		private global::Avalonia.Input.PointerPressedEventArgs _lastPressArgs;

		public DragAndDropListBox ParentListBox { get; internal set; }

		public DropPosition DropPosition { get; internal set; }

		protected override void OnPointerPressed(global::Avalonia.Input.PointerPressedEventArgs e)
		{
			_wasSelected = base.IsSelected;
			if (!base.IsSelected)
			{
				base.OnPointerPressed(e);
			}
			if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			{
				_dragStartPoint = e.GetPosition(null);
				_lastPressArgs = e;
				e.Pointer.Capture(this);
			}
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
			object[] array = ParentListBox.SelectedItems.CompactMap((object x) => x);
			if (array.Length < 1)
			{
				return;
			}
			ListBoxItem[] listBoxItems = array.CompactMap((object x) => ParentListBox.ContainerFromItem(x) as ListBoxItem);
			_adorner = new DragAndDropListBoxAdorner(this, listBoxItems, e.GetPosition(this));
			if (_adorner != null)
			{
				AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(ParentListBox);
				if (adornerLayer != null)
				{
					adornerLayer.Add(_adorner);
					// 修复（2026-09-30，"拖起后落点拿不到数据、重排永不发生"）：原版把 object[] 直接交给
					// DragDropLauncher.DoDragDrop(source, ...)，ToTransfer 的 default 分支把它 ToString 成
					// 类型名字符串存进 DataTransfer——落点 GetData 拿到的是字符串，GetData(typeof(object[]))
					// 恒为 null。改为 WpfDataObject.SetData 进进程内直通表（RuntimePayload）保留原始引用
					// （与侧边栏/文件列表/ClosableTabItem 拖放同款），并用本手势按下参数的 press 重载一次发起。
					global::Avalonia.Input.PointerPressedEventArgs pressArgs = _lastPressArgs;
					_lastPressArgs = null;
					if (pressArgs != null)
					{
						WpfDataObject dataObject = new WpfDataObject();
						dataObject.SetData(DragItemsFormat, array);
						global::ForkPlus.UI.WpfCompat.DragDropLauncher.DoDragDrop(pressArgs, dataObject, DragDropEffects.Move);
					}
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

		// 修复（2026-09-10，"拖动后残留矩形挡界面"）：拖动被中断时 OnDrop/OnDragLeave 可能都不触发，
		// 指针离开控件边界时兜底清一次。
		protected override void OnPointerExited(global::Avalonia.Input.PointerEventArgs e)
		{
			base.OnPointerExited(e);
			ClearDropAdorner();
		}

		// 修复（2026-09-10，"首次启动也偶发矩形挡界面"）：控件加载完成时兜底清一次残留。
		protected override void OnLoaded(global::Avalonia.Interactivity.RoutedEventArgs e)
		{
			base.OnLoaded(e);
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
			// 修复（2026-09-10）：同 DragAndDropListViewItem——先清旧 adorner 再加新的，避免堆积残留挡界面。
			ClearDropAdorner();
			_dropAdorner = new DropPlaceAdorner(this, dropPosition);
			if (_dropAdorner != null)
			{
				AdornerLayer.GetAdornerLayer(ParentListBox)?.Add(_dropAdorner);
			}
		}

		private void ClearDropAdorner()
		{
			if (_dropAdorner != null)
			{
				AdornerLayer.GetAdornerLayer(ParentListBox)?.Remove(_dropAdorner);
			}
		}
	}
}
