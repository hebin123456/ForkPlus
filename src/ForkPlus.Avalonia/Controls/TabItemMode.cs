namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 TabItemMode（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/TabItemMode.cs（8 行）：
    //   - WPF TabItemMode : enum
    //     - Repository = 0
    //     - RepositoryManager = 1
    //     - GitMm = 2
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 枚举无 WPF / Avalonia UI 依赖，零改动复用
    //   2. 命名空间 ForkPlus.UI.Controls → ForkPlus.Avalonia.Controls
    //   3. WPF 之前 spike 在 ClosableTabItem.cs 内联了同名枚举，
    //      现按 task spec 拆出独立文件，保持与 WPF 一致的成员顺序
    //      （Repository / RepositoryManager / GitMm）
    //
    // spike 简化：
    //   - 与 WPF 完全一致的枚举（成员顺序也对齐 WPF 源）
    public enum TabItemMode
    {
        Repository,
        RepositoryManager,
        GitMm,
    }
}
