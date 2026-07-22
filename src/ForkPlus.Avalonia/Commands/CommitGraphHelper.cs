// spike: 对照 WPF 工程 src/ForkPlus/UI/Commands/CommitGraphHelper.cs
//   - WPF: public static class CommitGraphHelper
//     - GitCommandResult<Sha?> FindBranchTip(GitModule, CommitGraphCache, Sha, ReferenceStorage, Sha?)
//     - GitCommandResult<List<Sha>> GetReachableRevisionsToHead(GitModule, CommitGraphCache, Sha, ReferenceStorage, Sha?)
//     （基于 Biturbo commit-graph 缓存的分支 tip 查找，依赖 ForkPlus.Biturbo + ForkPlus.Git.Commands）
//   - spike: 暂不迁移实际逻辑（Biturbo P/Invoke + CommitGraphCache 在 Phase 2.7b 才接入），
//     保留静态类骨架，方法返回 nullable / 空列表占位，避免下游编译断裂。
using System.Collections.Generic;
using ForkPlus.Git;

namespace ForkPlus.Avalonia.Commands
{
    /// <summary>
    /// spike 版 CommitGraphHelper 占位。对照 WPF CommitGraphHelper，
    /// 实际 Biturbo commit-graph 查找逻辑在 Phase 2.7b 接入后补全。
    /// </summary>
    public static class CommitGraphHelper
    {
        public static Sha? FindBranchTip(GitModule gitModule, object commitGraphCache, Sha head, object references, Sha? shaToSelect)
        {
            // spike: 占位返回 null，实际逻辑见 WPF src/ForkPlus/UI/Commands/CommitGraphHelper.cs
            return null;
        }

        public static List<Sha> GetReachableRevisionsToHead(GitModule gitModule, object commitGraphCache, Sha head, object references, Sha? shaToSelect)
        {
            // spike: 占位返回空列表
            return new List<Sha>();
        }
    }
}
