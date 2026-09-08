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
	/// 兜底：新 PART_SelectedContentHost 注册（RegisterContentPresenter）时，若被跟踪的
	/// 旧 presenter 还持有同一 Control 内容，先显式释放——不依赖任何时序假设。
	/// 用法：TabControl 子类覆写 RegisterContentPresenter，在 base 调用后把返回值
	/// 传给 <see cref="OnSelectedContentHostRegistered"/> 更新跟踪字段。
	/// </summary>
	internal static class TabControlContentHostGuard
	{
		/// <summary>模板重建兜底释放。tracked 为子类跟踪的上一个 PART_SelectedContentHost；
		/// registered 为刚注册的新 presenter（base.RegisterContentPresenter 已把它设为
		/// ContentPart 并赋值 Content=SelectedContent）。返回应继续跟踪的 presenter。</summary>
		internal static ContentPresenter OnSelectedContentHostRegistered(
			[Null] ContentPresenter tracked, ContentPresenter registered)
		{
			if (registered == null || registered.Name != "PART_SelectedContentHost")
			{
				return tracked;
			}
			if (tracked != null && !ReferenceEquals(tracked, registered)
				&& registered.Content is Control content
				&& ReferenceEquals(tracked.Content, content))
			{
				// 旧 presenter 已脱离逻辑树（模板重建丢弃）：置 null Content 触发
				// ContentChanged → VisualChildren.Remove(Child) → 内容从旧 presenter 释放，
				// 新 presenter measure 时 Add 才能成功。模板里 ContentTemplate/DataContext
				// 一并清理，与 TabControl.ClearPresenterContent 语义对齐。
				tracked.Content = null;
				tracked.ContentTemplate = null;
				tracked.DataContext = null;
			}
			return registered;
		}
	}
}
