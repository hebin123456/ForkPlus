using ForkPlus.UI.UserControls.BinaryDiff;

namespace ForkPlus.Plugins
{
	/// <summary>
	/// v4.5.0：把"插件声明的视图"接进宿主查看器注册表的桥接查看器。
	///
	/// 它只负责分类——该文件是否由某个插件认领（<see cref="PluginManager.ClaimsPath"/>）；
	/// 真正的渲染在 <see cref="PluginManager.Render"/> → 插件进程里完成，宿主拿到的是 PNG 帧。
	/// 这样插件视图与内置查看器共用同一套判定/解析流程（<see cref="BinaryViewerRegistry.Resolve"/>），
	/// 宿主不需要为插件新增任何类型判断分支。
	///
	/// 优先级取所有插件视图里的最高者（<see cref="PluginManager.MaxViewerPriority"/>）：
	/// 插件声明 priority 250 即可排到内置动图（200）之前，从而接管 GIF / 动态 WebP 这类
	/// 会被内置高优先级挡住的格式。未装插件时回落 150（内置静态图 100 与动图 200 之间），
	/// 且此时 <see cref="CanHandle"/> 恒为 false，档位不影响任何判定。
	/// </summary>
	public sealed class PluginBinaryViewer : IBinaryViewer
	{
		public BinaryViewerKind Kind => BinaryViewerKind.Plugin;

		public int Priority => PluginManager.Instance.MaxViewerPriority;

		public bool CanHandle([Null] BinaryViewerRequest request)
		{
			return request?.Path != null && PluginManager.Instance.ClaimsPath(request.Path);
		}
	}
}