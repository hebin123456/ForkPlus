using System;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using ForkPlus;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// TabControl 模板重建竞态防护（2026-09-07，"切换主题就有可能导致 UI 崩溃"）。
	///
	/// 根因：每个皮肤字典（Generic.*.axaml）独立加载，切主题 = ControlTheme 换成新实例
	///（显式 <c>Theme="{DynamicResource 具名key}"</c> 的实例立即更新）→ 模板重建。
	/// 重建期间 <c>TemplatedControl.ApplyTemplate</c> 先把旧 PART_SelectedContentHost 的
	/// TemplatedParent/Host 清 null 再丢弃，而 Avalonia TabControl 内部的内容释放逻辑
	/// <c>ClearOwningContentPresenter</c> 依赖 <b>旧 presenter 的 Host == TabControl</b>
	/// 才会释放选中内容——Host 已被清 null，条件恒假，旧 presenter 仍把选中内容
	///（如 RepositoryDetails 的 Grid）持为视觉子级。新 presenter 测量时
	/// <c>VisualChildren.Add(content)</c> 抛
	/// "The control Grid already has a visual parent ContentPresenter (PART_SelectedContentHost)"。
	///（实证：RepositoryDetailsUserControl 的 ModernTabControl，Theme="{DynamicResource
	/// RepositoryManagerTabControl}"，切主题必现；隐式类型 key 主题 / 构造函数一次性赋值的
	/// ClosableTabControl 不受影响——前者走 ItemContainerTheme→RefreshContainers→
	/// SetControlContent 的同步释放路径，后者模板从不重建。）
	///
	/// 兜底：新 PART_SelectedContentHost 注册（RegisterContentPresenter）时，只要它是与
	/// 旧 tracked 不同的实例（即模板重建产生的新 presenter），无条件强制摘除旧 presenter
	/// 的 Content/ContentTemplate/DataContext，触发内容从旧 presenter 视觉子级释放——
	/// 不依赖基类赋值 Content 的时序，也不依赖新旧内容引用相等（详见方法注释 2026-09-10 加固）。
	/// 用法：TabControl 子类覆写 RegisterContentPresenter，在 base 调用后把返回值
	/// 传给 <see cref="OnSelectedContentHostRegistered"/> 更新跟踪字段。
	/// </summary>
	internal static class TabControlContentHostGuard
	{
	/// <summary>模板重建兜底释放。tracked 为子类跟踪的上一个 PART_SelectedContentHost；
	/// registered 为刚注册的新 presenter（base.RegisterContentPresenter 已把它设为
	/// ContentPart）。返回应继续跟踪的 presenter。</summary>
		internal static ContentPresenter OnSelectedContentHostRegistered(
			[Null] ContentPresenter tracked, ContentPresenter registered)
		{
			if (registered == null || registered.Name != "PART_SelectedContentHost")
			{
				return tracked;
			}
			// 新 PART_SelectedContentHost 注册即意味着旧 tracked presenter 已被模板重建丢弃
			//（同一 TabControl 不会同时有两个活跃的 SelectedContentHost）。直接强制摘除旧
			// presenter 的 Content/ContentTemplate/DataContext，触发 ContentChanged →
			// VisualChildren.Remove(Child) → 内容从旧 presenter 视觉子级释放，新 presenter
			// 后续 measure 时 Add 才能成功。
			//
			// 加固（2026-09-10，"切主题偶发卡死：Grid already has a visual parent"）：
			// 原条件依赖 registered.Content 当时已被基类赋上选中内容且与 tracked.Content 同一引用
			// 才释放——但 base.RegisterContentPresenter 赋值 Content 的时序不稳定，实测切主题偶发
			// 到达此处时 registered.Content 仍为 null，`is Control` 短路 → 兜底未执行；随后布局阶段
			// Content 才被设上，旧 presenter 仍持有该 Grid → 新 presenter Measure 抛
			// "already has a visual parent ContentPresenter (PART_SelectedContentHost)"，异常虽被
			// DispatcherUnhandledException 吞掉（Handled=true）但抛在 Measure 阶段导致布局 pass
			// 反复重试，UI 表现为卡死。改为：只要新注册的是不同的 PART_SelectedContentHost 实例，
			// 无条件释放旧 presenter 的内容，不再依赖基类赋值时序与内容引用相等。
			if (tracked != null && !ReferenceEquals(tracked, registered))
			{
				tracked.Content = null;
				tracked.ContentTemplate = null;
				tracked.DataContext = null;
			}
			return registered;
		}
	}
}
