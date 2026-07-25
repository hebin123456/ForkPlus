// ⚠ 临时桥接类型 ─ 阶段 4.5 编译过渡用。
// WPF System.Windows.ResizeMode 枚举（CanResize/CanMinimize/NoResize）在 Avalonia 中无直接对应：
// Avalonia Window 使用 bool CanResize 属性。
//
// 此枚举仅用于让原 WPF 代码通过编译。CustomWindow.ResizeMode 属性的 setter 会将
// NoResize 映射为 CanResize=false，其他值映射为 CanResize=true。
namespace ForkPlus.UI
{
	public enum ResizeMode
	{
		CanResize = 0,
		CanMinimize = 1,
		NoResize = 2,
		CanResizeWithGrip = 3
	}
}
