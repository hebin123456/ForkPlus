using System.Collections.Generic;
using ForkPlus.Plugins.Abstractions;

namespace ForkPlus.Plugins.BuiltIn.HexDiff
{
	/// <summary>
	/// v5.0.0：内置 Hex 对比插件（独立 DLL：ForkPlus.Plugins.Hex.dll，经 plugins/ 动态加载）。
	/// v5.0.0 起兼二进制文件通配兜底（原 forkplus.binary 职责并入，与 4.3.2「一个图片对比器 +
	/// 一个十六进制对比器」的心智模型一致）：任何未被专属插件认领的文件都落这里——默认
	/// side-by-side 文件卡片（图标 + 扩展名 + 大小 + LFS 徽章），宿主预载字节（≤50MB）时
	/// 可切 Hex 对比。图片扩展名仍由图片插件（forkplus.image，100）优先认领；第三方插件
	/// 以更高优先级或用户绑定（DiffViewPluginRegistry.BindExtension）覆盖本兜底。
	/// 渲染内核与图片插件的「Hex」模式页共用同一 ForkPlus.Plugins.Ui 组件
	///（HexDiffUserControl / BinaryContentPanel）。
	/// </summary>
	public sealed class HexDiffPlugin : IDiffViewPlugin
	{
		public string Id => "forkplus.hex";

		public string DisplayNameKey => "Hex";

		/// <summary>通配兜底：取最低优先级，任何精确扩展名插件（如图片）都先于它。</summary>
		public int Priority => 0;

		public IReadOnlyList<string> FileExtensions => new string[1] { "*" };

		/// <summary>扩展名已由路由/绑定命中；字节未预载（超大文件）时视图只显示文件卡片。</summary>
		public bool CanHandle(DiffViewRequest request)
		{
			return true;
		}

		public IDiffView CreateView()
		{
			return new HexDiffView();
		}
	}
}
