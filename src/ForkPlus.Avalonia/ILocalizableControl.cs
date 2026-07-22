// Avalonia spike 版 ILocalizableControl（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/ILocalizableControl.cs（6 行）：
//   - WPF: public interface ILocalizableControl
//   - 单方法 ApplyLocalization()
//   - 无 WPF 依赖
//
// Avalonia 版差异：
//   1. 无 WPF 依赖，零改动复用
//   2. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
//
// spike 简化：与 WPF 完全一致的接口。
namespace ForkPlus.Avalonia
{
	public interface ILocalizableControl
	{
		void ApplyLocalization();
	}
}
