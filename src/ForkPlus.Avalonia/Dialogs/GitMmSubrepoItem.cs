using System;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.16b：Avalonia 端 GitMmSubrepoItem stub（spike 临时类型，仅包含 GitMmStartWindow 所需的 Path / Name）。
    //
    // 背景：WPF 工程 src/ForkPlus/UI/UserControls/GitMmUserControl.xaml.cs 中的
    // GitMmSubrepoItem 类是 sealed class，依赖 FrameworkElement、RepositoryManager、
    // PreferencesLocalization 等 WPF-only 类型，无法直接迁移到 Avalonia。
    //
    // spike 策略：在 Avalonia 工程内创建一个最小 POCO，只暴露 GitMmStartWindow 真正
    // 使用的 Path / Name 两个只读属性。当 Phase 5 迁移 GitMmUserControl 时，真正的
    // GitMmSubrepoItem 类型会被移到 Core 工程，并替换掉这个 stub。
    //
    // 对照 WPF: public sealed class GitMmSubrepoItem
    //   public string Path { get; }            // ← 保留
    //   public string Name { get; }            // ← 保留
    //   public bool IsRootRepository { get; }  // ← stub 省略
    //   public bool IsSubmodule { get; }       // ← stub 省略
    //   ... 其余属性 GitMmStartWindow 不使用
    public sealed class GitMmSubrepoItem
    {
        public string Path { get; }

        public string Name { get; }

        public GitMmSubrepoItem(string path, string name)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }
}
