// ⚠ 临时桥接类型 ─ 阶段 4.5 编译过渡用。
// WPF System.Windows.Visibility 枚举（Visible/Collapsed/Hidden）在 Avalonia 中无直接对应：
// Avalonia 控件用 bool IsVisible 属性。
//
// 此枚举仅用于让大量 ViewModel/code-behind 代码先通过编译，保持 Visibility.Visible/Collapsed
// 写法不变。真正的迁移（阶段 4 XAML 绑定收尾）会把这些属性改为 bool，XAML 的
// Visibility="{Binding X}" 改为 IsVisible="{Binding X}"，届时删除本文件。
//
// 命名空间 ForkPlus.UI：ForkPlus.UI.* 子命名空间内的代码可直接引用（C# 沿命名空间链查找）。
namespace ForkPlus.UI
{
	/// <summary>WPF Visibility 枚举的 Avalonia 兼容占位。</summary>
	public enum Visibility
	{
		/// <summary>可见（对应 Avalonia IsVisible = true）。</summary>
		Visible = 0,

		/// <summary>折叠不占空间（对应 IsVisible = false）。</summary>
		Collapsed = 1,

		/// <summary>隐藏但占空间（Avalonia 无直接对应，暂映射为不占空间）。</summary>
		Hidden = 2
	}
}
