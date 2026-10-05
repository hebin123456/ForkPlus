using System.IO;
using Avalonia.Media;

namespace ForkPlus.Plugins.Abstractions
{
	/// <summary>对比视图一侧的角色（决定标题文案与标题颜色）。</summary>
	public enum DiffSideRole
	{
		/// <summary>旧版本（双侧对比的左侧，标题 "old"）。</summary>
		Old,

		/// <summary>新版本（双侧对比的右侧，标题 "new"）。</summary>
		New,

		/// <summary>新增文件（仅右侧存在，标题 "created"）。</summary>
		Created,

		/// <summary>删除文件（仅左侧存在，标题 "removed"）。</summary>
		Removed
	}

	/// <summary>视图切换按钮的一个模式（如 Side-by-Side / Swipe / Onion Skin / Hex）。</summary>
	public sealed class DiffViewMode
	{
		public string Id { get; }

		/// <summary>按钮文案的翻译 key（插件经 PluginEnvironment.Translate 取译文）。</summary>
		public string TitleKey { get; }

		public DiffViewMode(string id, string titleKey)
		{
			Id = id;
			TitleKey = titleKey;
		}
	}

	/// <summary>
	/// 一次对比的完整上下文（v5.0.0 插件化架构）：宿主构造后经 IDiffView.SetContent 下发给视图。
	/// </summary>
	public sealed class DiffViewContext
	{
		/// <summary>文件路径（含扩展名，供路由/图标/保存建议名使用）。</summary>
		public string FilePath { get; }

		/// <summary>是否显示 old/new 等标题栏（merge 子控件等场景隐藏）。</summary>
		public bool ShowTitle { get; }

		/// <summary>左侧（旧）内容；不存在（新增文件）时为 null。</summary>
		public DiffSideContent Src { get; }

		/// <summary>右侧（新）内容；不存在（删除文件）时为 null。</summary>
		public DiffSideContent Dst { get; }

		public DiffSideRole SrcRole { get; }

		public DiffSideRole DstRole { get; }

		/// <summary>左标题画刷（宿主按主题注入，old/removed 一侧）。</summary>
		public IBrush SrcTitleBrush { get; }

		/// <summary>右标题画刷（宿主按主题注入，new/created 一侧）。</summary>
		public IBrush DstTitleBrush { get; }

		/// <summary>
		/// 宿主预载的 Hex 源字节（小体积二进制时 FileDiffControl 提前加载，供 Hex 模式增量渲染）。
		/// 未预载为 null，视图按需自行处理（不预载 Hex）。
		/// </summary>
		public MemoryStream HexSrc { get; }

		/// <summary>同 <see cref="HexSrc"/>，右侧。</summary>
		public MemoryStream HexDst { get; }

		public DiffViewContext(string filePath, bool showTitle, DiffSideContent src, DiffSideContent dst, DiffSideRole srcRole, DiffSideRole dstRole, IBrush srcTitleBrush, IBrush dstTitleBrush, MemoryStream hexSrc = null, MemoryStream hexDst = null)
		{
			FilePath = filePath;
			ShowTitle = showTitle;
			Src = src;
			Dst = dst;
			SrcRole = srcRole;
			DstRole = dstRole;
			SrcTitleBrush = srcTitleBrush;
			DstTitleBrush = dstTitleBrush;
			HexSrc = hexSrc;
			HexDst = hexDst;
		}
	}
}
