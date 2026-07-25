using ForkPlus.UI.Helpers;
using System.Windows.Media;
using ForkPlus.UI;

// ══════════════════════════════════════════════════════�?//  UI 层对 Remote 的扩�?—�?提供 WPF 特定的图标属性�?//  这些属性通过 IconKey / IconGeometryKey �?Theme 资源中解析�?//  迁移�?Avalonia 时，将此类替换为相应的平台图标绑定�?// ══════════════════════════════════════════════════════�?
namespace ForkPlus.Git
{
	public partial class Remote
	{
		/// <summary>
		/// 远程�?WPF IImage 图标�?		/// 数据绑定友好（XAML �?<see cref="Binding.Path"/>="Remote.Icon" 仍可工作）�?		/// </summary>
		public IImage Icon => Theme.FindImage(IconKey) ?? Theme.RemoteIcon;

		/// <summary>
		/// 远程�?WPF Geometry 图标（用�?Path/Content 绑定）�?		/// </summary>
		public Geometry IconGeometry => Theme.FindGeometry(IconGeometryKey) ?? Theme.RemoteGeometry;
	}
}
