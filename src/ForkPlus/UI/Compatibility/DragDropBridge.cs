// ⚠ 临时桥接 ─ 阶段 5 编译过渡用。
// WPF DragDrop.DoDragDrop(DependencyObject dragSource, object data, DragDropEffects allowedEffects)
// → Avalonia DragDrop.DoDragDrop(PointerEventArgs triggerEvent, IDataObject data, DragDropEffects allowedEffects)
//
// 差异：
// 1. WPF 第一参数为 dragSource（任意 DependencyObject），Avalonia 必须为触发拖拽的 PointerEventArgs。
// 2. WPF data 为 object（自动包装），Avalonia 必须为 IDataObject。
//
// 本桥接提供：
// - 静态字段缓存最近一次 OnPointerMoved 的 PointerEventArgs（由调用方在拖拽发起前设置）。
// - DoDragDrop(AvaloniaObject, object, DragDropEffects) 扩展方法：自动包装 data 为 DataObject，
//   使用缓存的 PointerEventArgs 调用原生 DragDrop.DoDragDrop。
// 阶段 6 需重构为捕获 PointerEventArgs 后异步调用原生 API（移除本桥接）。
using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ForkPlus.UI
{
	/// <summary>
	/// WPF DragDrop.DoDragDrop 签名兼容桥接。
	/// 通过静态字段缓存最近一次 PointerEventArgs，让 StartDrag(AvaloniaObject, ...) 等
	/// 无法直接访问 PointerEventArgs 的旧 API 仍能调用 Avalonia 异步 DoDragDrop。
	/// </summary>
	public static class DragDropBridge
	{
		/// <summary>
		/// 缓存最近一次 OnPointerMoved 的 PointerEventArgs，供 StartDrag 调用 DoDragDrop 时使用。
		/// 调用方（TreeViewControlItem.OnPointerMoved 等）在调用 StartDrag 前必须先设置此字段。
		/// </summary>
		[ThreadStatic]
		private static PointerEventArgs t_lastPointerEvent;

		/// <summary>记录最近一次 PointerEventArgs，供后续 DoDragDrop 使用。</summary>
		public static void CapturePointerEvent(PointerEventArgs e)
		{
			t_lastPointerEvent = e;
		}

		/// <summary>WPF DragDrop.DoDragDrop(AvaloniaObject, object, DragDropEffects) 兼容。</summary>
		/// <remarks>
		/// 使用 CapturePointerEvent 缓存的 PointerEventArgs 作为触发事件；
		/// 将 data 包装为 DataObject（若 data 已实现 IDataObject 则直接使用）。
		/// </remarks>
		public static async Task<DragDropEffects> DoDragDrop(AvaloniaObject dragSource, object data, DragDropEffects allowedEffects)
		{
			PointerEventArgs evt = t_lastPointerEvent;
			t_lastPointerEvent = null;
			if (evt == null)
			{
				// 无缓存的 PointerEventArgs，无法发起拖拽（阶段 5 占位）。
				return DragDropEffects.None;
			}
			IDataObject dataObject = data as IDataObject;
			if (dataObject == null)
			{
				var d = new DataObject();
				if (data != null)
				{
					// WPF 自动用 Type.FullName 作为格式名。WeakReference<T>、数组等均按此规则注册。
					d.Set(data.GetType().FullName, data);
				}
				dataObject = d;
			}
			return await DragDrop.DoDragDrop(evt, dataObject, allowedEffects);
		}
	}
}
