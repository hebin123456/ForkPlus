// DummyCommandProvider.cs：占位命令提供者。
//
// WPF 源对照：src/ForkPlus/UI/QuickLaunch/DummyCommandProvider.cs（namespace ForkPlus.UI.QuickLaunch）
//   - class DummyCommandProvider : ICommandProvider
//   - ArgumentType Type => ArgumentType.Default
//   - CommandProviderItem[] Items => new CommandProviderItem[0]
//   - void Refresh(string filter) { }
//
// Avalonia 版差异：
//   1. namespace ForkPlus.UI.QuickLaunch → ForkPlus.Avalonia.QuickLaunch
//   2. ArgumentType 从 ForkPlus.UI.Commands（WPF）→ spike POCO（同命名空间）
//   3. 其余零修改（无 WPF 依赖）

namespace ForkPlus.Avalonia.QuickLaunch
{
    public class DummyCommandProvider : ICommandProvider
    {
        public ArgumentType Type => ArgumentType.Default;

        public CommandProviderItem[] Items => new CommandProviderItem[0];

        public void Refresh(string filter)
        {
        }
    }
}
