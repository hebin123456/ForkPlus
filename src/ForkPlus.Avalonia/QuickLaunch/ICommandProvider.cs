// ICommandProvider.cs：命令提供者接口。
//
// WPF 源对照：src/ForkPlus/UI/QuickLaunch/ICommandProvider.cs（namespace ForkPlus.UI.QuickLaunch）
//   - interface ICommandProvider { ArgumentType Type; CommandProviderItem[] Items; void Refresh(string); }
//
// Avalonia 版差异：
//   1. namespace ForkPlus.UI.QuickLaunch → ForkPlus.Avalonia.QuickLaunch
//   2. ArgumentType 从 ForkPlus.UI.Commands（WPF 工程）→ spike POCO（同命名空间 ForkPlus.Avalonia.QuickLaunch）
//   3. CommandProviderItem 引用同命名空间的 spike 版（见 CommandProviderItem.cs）

namespace ForkPlus.Avalonia.QuickLaunch
{
    public interface ICommandProvider
    {
        ArgumentType Type { get; }

        CommandProviderItem[] Items { get; }

        void Refresh(string filterString);
    }
}
