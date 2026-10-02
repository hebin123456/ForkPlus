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
	///
	/// 判定用 <see cref="PluginManager.ClaimsRenderablePath"/>（含通配 "*" 兜底），并要求
	/// <c>Data</c> 非空：通配视图声称"什么都能看"，但拿不到字节时插件其实渲染不出任何东西，
	/// 交给它只会得到空白；此时应退回内置的文件卡片，而不是让插件空转。
	///
	/// 通配的两条边界（避免把图片插件/内置图片查看器的活抢走）：
	///   1) 具体扩展名声明优先——<see cref="PluginManager.ClaimsPath"/> 命中即接管，
	///      所以图片插件列的 .png 仍归图片插件；
	///   2) 宿主已按图片处理的格式（<see cref="PathHelper.IsImagePath"/>，含内置后缀与
	///      插件显式声明）通配不再兜底——否则"什么都能看"的二进制插件会顶掉内置静态图，
	///      把 .ico / .bmp 这类没人显式声明的图片渲成二进制结构图。
	/// </summary>
	public sealed class PluginBinaryViewer : IBinaryViewer
	{
		private readonly PluginManager _manager;

		/// <param name="manager">提供认领判定与优先级的插件管理器（由它的 Initialize 注册本查看器）。</param>
		public PluginBinaryViewer(PluginManager manager)
		{
			_manager = manager ?? PluginManager.Instance;
		}

		public BinaryViewerKind Kind => BinaryViewerKind.Plugin;

		public int Priority => _manager.MaxViewerPriority;

		public bool CanHandle([Null] BinaryViewerRequest request)
		{
			if (request?.Path == null || request.Data == null)
			{
				return false;
			}
			// 具体扩展名声明的格式一律归插件——哪怕内置查看器也认它（图片插件要覆盖内置图片
			// 渲染就是这个场景）。
			if (_manager.ClaimsPath(request.Path))
			{
				return true;
			}
			// 通配只是"没人认领的二进制"的兜底，不能去抢宿主已按图片处理的格式：
			// .ico / .bmp 等内置图片没有插件显式声明，放任通配就会把内置图片查看器顶掉，
			// 交回给内置查看器才是对的。
			if (PathHelper.IsImagePath(request.Path))
			{
				return false;
			}
			// 走到这里只会是通配命中（具体扩展名在上面已返回）。
			return _manager.ClaimsRenderablePath(request.Path);
		}
	}
}