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
		/// <summary>
		/// 交互式变基 todo 列表拖拽 payload 的字符串格式 key（进程内直通表 RuntimePayload）。
		/// WPF 原版 DataObject 以 typeof(RevisionEntry[]) 作 key；Avalonia 兼容层
		/// WpfDataObject.GetData(Type) 对非 string 一律返回 null，自定义对象必须走字符串 key
		/// （SetData/GetData 同名 key 经直通表保留原对象引用，落点才能拿回 RevisionEntry[]）。
		/// 发起侧（OnPointerMoved）与读取侧（InteractiveRebaseWindow.RevisionListViewItem_Drop）共用。
		/// </summary>
		public static readonly string DragItemsFormat = "ForkPlusIrRows";

		private bool _wasSelected;

		private Point _dragStartPoint;

		private global::Avalonia.Input.PointerPressedEventArgs _lastPressArgs;

		private DragAndDropListBoxAdorner _adorner;

		private DropPlaceAdorner _dropAdorner;

		public MultiselectionListView ParentListView { get; internal set; }

		public DropPosition DropPosition { get; internal set; }

		public MultiselectionListViewItem()
		{
			// 修复（WS3"交互式变基列表无法拖拽排序"）：WPF 原版靠 ItemContainerStyle 里的
			// EventSetter 绑定 DragEnter/DragOver/DragLeave/Drop，迁移时 EventSetter 被删且
			// 未在别处接线——OnDragEnter/OnDrop/OnDragLeave 沦为无人调用的死代码（无插入线、
			// 无 DropPosition、不清残留 adorner）。按 DragAndDropListViewItem 同款在构造函数
			// AddHandler 接线（DragDrop 事件为 Bubble 路由，AddHandler 默认 Direct|Bubble
			// 订阅可收到行内子元素上触发的事件）。
			AddHandler(DragDrop.DragEnterEvent, (_, e) => OnDragEnter(e));
			AddHandler(DragDrop.DragOverEvent, (_, e) => OnDragEnter(e));
			AddHandler(DragDrop.DragLeaveEvent, (_, e) => OnDragLeave(e));
			AddHandler(DragDrop.DropEvent, (_, e) => OnDrop(e));
			// WPF ItemContainerStyle Setter AllowDrop=true 迁移时被删（误注"Avalonia 无该属性"——
			// 实为 inherits:true 附加属性 DragDrop.AllowDrop）。不设置则行容器不是合法落点，
			// DragEnter/Drop 根本不会路由到行上（拖拽发起后无落点、无重排）。
			DragDrop.SetAllowDrop(this, true);
		}

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
				_lastPressArgs = null;
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
				// 修复（WS3）：记录本手势的按下参数——Avalonia DragDrop.DoDragDropAsync 必须由
				// PointerPressedEventArgs 发起；走 DragDropLauncher.DoDragDrop(press,...) 直传
				// 重载，首次手势即可发起（旧 DoDragDrop(source,...) 两段式首次手势必被吞）。
				_lastPressArgs = e;
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
			// 手势结束，按下参数作废（防止下个手势在未按压状态下凭旧参数发起拖拽）。
			_lastPressArgs = null;
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
			global::Avalonia.Input.PointerPressedEventArgs pressArgs = _lastPressArgs;
			_lastPressArgs = null;
			if (pressArgs == null)
			{
				return;
			}
			// 修复（WS3"拖动有效果但没法换位置"）：原先把 RevisionEntry[] 裸交给
			// DragDropLauncher.DoDragDrop——ToTransfer 的 default 分支把数组 ToString 成类型名
			// 存进 DataTransfer，落点无论按什么 key 都拿不回原对象。改为 WpfDataObject.SetData
			// 字符串 key（DragItemsFormat）进进程内直通表（与侧边栏/文件列表/tab 拖放同款做法）。
			WpfDataObject dataObject = new WpfDataObject();
			dataObject.SetData(DragItemsFormat, array);
			global::Avalonia.Controls.ListBoxItem[] listBoxItems = array.CompactMap((RevisionEntry x) => ParentListView.ContainerFromItem(x) as global::Avalonia.Controls.ListBoxItem);
			_adorner = new DragAndDropListBoxAdorner(this, listBoxItems, e.GetPosition(this));
			if (_adorner != null)
			{
				AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(ParentListView);
				if (adornerLayer != null)
				{
					adornerLayer.Add(_adorner);
					// 发起前释放指针捕获：拖拽会话期间指针事件由拖拽管线跟踪，行容器继续持有
					// 捕获会让指针事件继续路由到本控件而非拖拽循环（拖影不动/会话状态错乱）。
					if (e.Pointer.Captured == this)
					{
						e.Pointer.Capture(null);
					}
					global::ForkPlus.UI.WpfCompat.DragDropLauncher.DoDragDrop(pressArgs, dataObject, DragDropEffects.Move);
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
		// 指针离开控件边界时兜底清一次，覆盖中断路径。
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
