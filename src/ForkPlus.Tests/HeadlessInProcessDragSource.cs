// E2E 拖拽基建（E2e29，2026-09-10）：headless 平台的进程内拖拽源。
// 背景：Avalonia.Headless 平台不注册 IPlatformDragSource——DragDrop.DoDragDropAsync 在
// AvaloniaLocator 里查不到该服务时直接 Dispose 数据并返回 None（Avalonia 12.1.1
// src/Avalonia.Base/Input/DragDrop.cs），拖放循环完全不跑，DragOver/Drop 永不触发，
// tab 拖拽类用例无从验证。
// 本类补上进程内拖拽源：DoDragDropAsync 时对 TopLevel 挂指针移动/释放监听，把后续真实
// 鼠标事件（测试在拖源控件上 RaiseEvent 的指针路由事件，冒泡到 TopLevel）转换成
// DragEnter/DragOver/DragLeave/Drop 路由事件。路由语义对齐 Avalonia 真实管线的
// DragDropDevice（src/Avalonia.Base/Input/DragDropDevice.cs）与 Win32 OLE：
//   1) 目标 = hit-test 取第一个 Interactive 祖先（AllowDrop 是 inherits:true 附加属性，
//      TabItem 上 SetAllowDrop(true) → 整个 header 子树可放）；
//   2) DragEventArgs.DragEffects 初始 = allowedEffects（目标不处理 DragOver 时保持非 None，
//      与 WPF 行为一致，Drop 允许触发）；
//   3) 最近一次 DragOver 返回 None 的目标不接收 Drop（对齐 Win32 OLE：
//      OleDropTarget.DragOver 后 *pdwEffect=None 则系统不放）。
// PrivateApi 封锁（2026-09-10 实证）：Avalonia 12 的 IPlatformDragSource 标了
// Avalonia.Metadata.PrivateApiAttribute——.NET 10 编译器把它当不可访问（CS0122/CS9044），
// 用户代码不能实现也不能调用其成员（Avalonia 自家 Win32/X11 实现不受限）。Avalonia 11 的
// 公开 InProcessDragSource 在 12 里已移除。故本类不实现该接口，改经 DispatchProxy 在
// 运行时生成实现（Emit 不走编译器检查），见 HeadlessDragSourceProxy。
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace ForkPlus.Tests
{
	internal sealed class HeadlessInProcessDragSource
	{
		/// <summary>代理共享的单例（headless 测试串行单 UI 线程，进程内拖拽一次一个会话）。</summary>
		internal static readonly HeadlessInProcessDragSource Instance = new HeadlessInProcessDragSource();

		private IDataTransfer _data;
		private DragDropEffects _allowedEffects;
		private TopLevel _topLevel;
		private Interactive _lastTarget;
		private DragDropEffects _lastEffects = DragDropEffects.None;

		internal bool IsActive { get; private set; }

		/// <summary>生产管线经代理实际调用的入口（Avalonia DragDrop.DoDragDropAsync →
		/// IPlatformDragSource.DoDragDropAsync → DispatchProxy.Invoke → 本方法）。</summary>
		public Task<DragDropEffects> DoDragDropAsync(PointerPressedEventArgs triggerEvent, IDataTransfer dataTransfer, DragDropEffects allowedEffects)
		{
			if (triggerEvent == null || IsActive)
			{
				dataTransfer?.Dispose();
				return Task.FromResult(DragDropEffects.None);
			}
			IsActive = true;
			_data = dataTransfer;
			_allowedEffects = allowedEffects;
			// Avalonia 12 移除了 VisualExtensions.GetVisualRoot / Visual.VisualRoot（探针实证
			// VisualExtensions 方法表），改经视觉祖先链取最近 TopLevel——窗口场景即根；
			// PopupRoot:TopLevel 的弹层场景同样正确（源在弹层内时锚定弹层根）。
			_topLevel = (triggerEvent.Source as Visual)?.GetVisualAncestors().OfType<TopLevel>().FirstOrDefault();
			if (_topLevel == null)
			{
				FinishSession();
				dataTransfer?.Dispose();
				return Task.FromResult(DragDropEffects.None);
			}
			// handledEventsToo：拖拽期间 TabItem 的 PointerMoved handler（拖拽发起逻辑）会继续
			// 触发并被防重短路，不阻碍事件继续路由到 TopLevel 的拖拽会话监听。
			_topLevel.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
			_topLevel.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
			return Task.FromResult(DragDropEffects.None);
		}

		private void OnPointerMoved(object sender, PointerEventArgs e)
		{
			if (!IsActive)
			{
				return;
			}
			Point position = e.GetPosition(_topLevel);
			Interactive target = GetDropTarget(position);
			if (target == _lastTarget)
			{
				_lastEffects = RaiseDragEvent(target, position, DragDrop.DragOverEvent, e.KeyModifiers);
				return;
			}
			if (_lastTarget != null)
			{
				RaiseDragEvent(_lastTarget, position, DragDrop.DragLeaveEvent, e.KeyModifiers);
			}
			_lastTarget = target;
			_lastEffects = RaiseDragEvent(target, position, DragDrop.DragEnterEvent, e.KeyModifiers);
		}

		private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
		{
			if (!IsActive)
			{
				return;
			}
			try
			{
				if (_lastTarget != null && _lastEffects != DragDropEffects.None)
				{
					RaiseDragEvent(_lastTarget, e.GetPosition(_topLevel), DragDrop.DropEvent, e.KeyModifiers);
				}
			}
			finally
			{
				FinishSession();
				_data?.Dispose();
				_data = null;
			}
		}

		private void FinishSession()
		{
			if (_topLevel != null)
			{
				_topLevel.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
				_topLevel.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
			}
			_lastTarget = null;
			_lastEffects = DragDropEffects.None;
			IsActive = false;
		}

		private Interactive GetDropTarget(Point position)
		{
			// 对齐 DragDropDevice.GetTarget：hit-test 后取第一个 Interactive；
			// AllowDrop 由视觉树继承（TabItem 上设置的 true 覆盖 header 子树）。
			var hit = _topLevel.InputHitTest(position) as Visual;
			var target = hit?.GetSelfAndVisualAncestors()?.OfType<Interactive>()?.FirstOrDefault();
			if (target != null && DragDrop.GetAllowDrop(target))
			{
				return target;
			}
			return null;
		}

		private DragDropEffects RaiseDragEvent(Interactive target, Point topLevelPosition, RoutedEvent<DragEventArgs> routedEvent, KeyModifiers modifiers)
		{
			if (target == null)
			{
				return DragDropEffects.None;
			}
			Point? p = _topLevel.TranslatePoint(topLevelPosition, target);
			if (!p.HasValue)
			{
				return DragDropEffects.None;
			}
			var args = new DragEventArgs(routedEvent, _data, target, p.Value, modifiers)
			{
				RoutedEvent = routedEvent,
				// 对齐 DragDropDevice.RaiseDragEvent：DragEffects 初始 = allowedEffects，
				// 目标不处理 DragOver 时保持可放（WPF 同语义）。
				DragEffects = _allowedEffects,
			};
			target.RaiseEvent(args);
			return args.DragEffects;
		}
	}

	/// <summary>
	/// IPlatformDragSource 的运行时代理（PrivateApi 接口的唯一可行实现途径）。
	/// DispatchProxy.Create&lt;接口, 代理类&gt; 在运行时 Emit 生成实现类型，绕过编译器对
	/// PrivateApi 接口的实现封锁（.NET 10 Roslyn 直接拒绝用户代码实现/访问）。
	/// 生成的代理实现 IPlatformDragSource，可安全塞进 AvaloniaLocator——生产管线
	/// DragDrop.DoDragDropAsync 内部的服务调用发生在 Avalonia.Base 自身 IL 里，
	/// 运行时绑定到代理 → Invoke → HeadlessInProcessDragSource.Instance。
	/// </summary>
	internal sealed class HeadlessDragSourceProxy : DispatchProxy
	{
		protected override object Invoke(MethodInfo targetMethod, object[] args)
		{
			if (targetMethod != null && targetMethod.Name == "DoDragDropAsync" && args != null && args.Length == 3)
			{
				return HeadlessInProcessDragSource.Instance.DoDragDropAsync(
					(PointerPressedEventArgs)args[0], (IDataTransfer)args[1], (DragDropEffects)args[2]);
			}
			return Task.FromResult(DragDropEffects.None);
		}

		/// <summary>生成可注册进 AvaloniaLocator 的拖拽源实例。
		/// 返回 object：调用方仅作类型引用（cast 到 IPlatformDragSource 也合法——类型
		/// 引用不触发成员封锁，仅成员访问受限），AvaloniaLocator.ToConstant 运行时按
		/// Bind&lt;T&gt; 的接口类型取服务。</summary>
		internal static object CreatePlatformDragSource()
		{
			return DispatchProxy.Create<IPlatformDragSource, HeadlessDragSourceProxy>();
		}
	}
}
