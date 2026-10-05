using System.Collections.Generic;
using ForkPlus.Plugins.Abstractions;

namespace ForkPlus.Plugins.BuiltIn.ImageDiff
{
	/// <summary>
	/// v5.0.0：内置图片对比插件。声明图片扩展名（与宿主 PathHelper.IsImagePath 同表），
	/// 命中后由 <see cref="BinaryDiffView"/> 渲染（并排/Swipe/洋葱皮/Hex，含 GIF/动态 WebP/APNG
	/// 播放与 LFS smudge）。第三方可注册更高优先级的同名扩展名插件覆盖之。
	/// v5.0.0：二进制通配兜底职责移交 Hex 插件（forkplus.hex，见 ForkPlus.Plugins.Hex）——
	/// 本插件只认图片扩展名，不再兜底非图片二进制。
	/// v5.0.1：实现 <see cref="IPluginMetadata"/>，向偏好设置 → 插件页提供名称/版本/描述。
	/// </summary>
	public sealed class ImageDiffPlugin : IDiffViewPlugin, IPluginMetadata
	{
		/// <summary>与宿主 PathHelper.IsImagePath 一致的扩展名表（小写含点）。</summary>
		public static readonly string[] ImageExtensions = new string[8]
		{
			".bmp", ".gif", ".png", ".jpg", ".jpeg", ".ico", ".tga", ".webp"
		};

		public string Id => "forkplus.image";

		public string DisplayNameKey => "Image";

		/// <summary>v5.0.1：插件元数据——名称/版本/描述（固定中文，国际化后续版本实现）。</summary>
		public string Version => "1.0.0";

		public string DisplayName => "图片对比";

		public string Description => "并排 / 滑动 / 洋葱皮三种图片对比视图，支持 GIF、动态 WebP、APNG 播放与 LFS 内容解析。";

		/// <summary>高于通配兜底（forkplus.hex，0），低于用户自定义绑定。</summary>
		public int Priority => 100;

		public IReadOnlyList<string> FileExtensions => ImageExtensions;

		/// <summary>扩展名已由宿主路由命中；图片视图对不可解码内容自带文件卡片兜底。</summary>
		public bool CanHandle(DiffViewRequest request)
		{
			return true;
		}

		public IDiffView CreateView()
		{
			return new BinaryDiffView();
		}
	}
}
