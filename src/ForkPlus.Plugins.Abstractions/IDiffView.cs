using System.Collections.Generic;
using Avalonia.Controls;

namespace ForkPlus.Plugins.Abstractions
{
	/// <summary>
	/// 单次对比的视图实例契约（v5.0.0 插件化架构）。
	/// 生命周期：CreateView → SetContent →（SetMode/Activate/Deactivate/ApplyLocalization 循环）→ Release。
	/// </summary>
	public interface IDiffView
	{
		/// <summary>视图根控件（宿主直接挂载到自己的容器里）。</summary>
		Control View { get; }

		/// <summary>
		/// 视图模式列表（供宿主渲染模式切换工具条）；内置视图自带工具条，返回空列表即可。
		/// </summary>
		IReadOnlyList<DiffViewMode> Modes { get; }

		/// <summary>「高亮差异像素」能力变化（图片像素级差异可用性）；无此能力不触发。</summary>
		event System.EventHandler<bool> HighlightPixelsAvailableChanged;

		/// <summary>下发内容：两侧数据 + 宿主能力桥。每次刷新对比都会调用（复用实例）。</summary>
		void SetContent(DiffViewContext context, IDiffViewHost host);

		/// <summary>切换视图模式（对应用户绑定/宿主工具条；不支持时忽略）。</summary>
		void SetMode(string modeId);

		/// <summary>切入显示（如恢复动图播放）。</summary>
		void Activate();

		/// <summary>切走/失活（如暂停动图播放，省电防闪）。</summary>
		void Deactivate();

		/// <summary>应用当前语言（宿主在语言切换时广播）。</summary>
		void ApplyLocalization();

		/// <summary>释放：停后台任务、退订事件、释放位图。之后实例不再复用。</summary>
		void Release();
	}
}
