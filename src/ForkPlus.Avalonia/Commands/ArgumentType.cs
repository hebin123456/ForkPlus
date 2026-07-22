// spike: 对照 WPF 工程 src/ForkPlus/UI/Commands/ArgumentType.cs
//   - WPF: public enum ArgumentType { Reference, Tag, Branch, LocalBranch, RemoteBranch,
//     FeatureBranch, HotfixBranch, ReleaseBranch, RepositoryFile, Remote, Workspace, Default }
//   - spike: 1:1 移植（纯枚举，零改动）。
namespace ForkPlus.Avalonia.Commands
{
    /// <summary>
    /// spike 版命令参数类型枚举。对照 WPF ArgumentType 1:1 移植。
    /// </summary>
    public enum ArgumentType
    {
        Reference,
        Tag,
        Branch,
        LocalBranch,
        RemoteBranch,
        FeatureBranch,
        HotfixBranch,
        ReleaseBranch,
        RepositoryFile,
        Remote,
        Workspace,
        Default
    }
}
