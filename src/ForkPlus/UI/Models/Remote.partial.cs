// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.Media → using Avalonia.Media
// - ImageSource → IImage（Avalonia.Media）
// - Geometry → Avalonia.Media.Geometry（命名空间迁移后自动解析）
// 注：Theme.FindImage 已返回 IImage，Theme.FindGeometry 已返回 Avalonia.Media.Geometry。
using Avalonia.Media;
using ForkPlus.UI;
using ForkPlus.UI.Helpers;

// ──────────────────────────────────────────────────────────────────────────
//  UI 层对 Remote 的扩展 —— 提供 Avalonia 平台特定的图标属性。
//  这些属性通过 IconKey / IconGeometryKey 从 Theme 资源中解析。
// ──────────────────────────────────────────────────────────────────────────
namespace ForkPlus.Git
{
	public partial class Remote
	{
		/// <summary>
		/// 远程的 Avalonia IImage 图标。
		/// 数据绑定友好（XAML 中 Binding.Path="Remote.Icon" 仍可工作）。
		/// </summary>
		public IImage Icon => Theme.FindImage(IconKey) ?? Theme.RemoteIcon;

		/// <summary>
		/// 远程的 Avalonia Geometry 图标（用于 Path/Content 绑定）。
		/// </summary>
		public Geometry IconGeometry => Theme.FindGeometry(IconGeometryKey) ?? Theme.RemoteGeometry;
	}
}
