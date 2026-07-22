using System;
using ForkPlus.Git;

// Avalonia spike 版 StashReferenceViewModel（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/StashReferenceViewModel.cs（25 行）：
//   - WPF: public class StashReferenceViewModel : ReferenceViewModel
//   - StashRevision _stash
//   - Reference Reference => throw new NotImplementedException()
//   - string ReflogName => _stash.ReflogName
//   - 构造 StashReferenceViewModel(int graphColumn, StashRevision stash)
//   - 依赖：ForkPlus.Git.StashRevision（Core 可用）
//
// Avalonia 版差异：
//   1. POCO 无 WPF 依赖，零改动复用
//   2. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
//
// spike 简化：
//   - 与 WPF 完全一致的 POCO 类（Reference 仍 throw NotImplementedException）
namespace ForkPlus.Avalonia
{
    public class StashReferenceViewModel : ReferenceViewModel
    {
        public StashRevision _stash;

        public override Reference Reference
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public string ReflogName => _stash.ReflogName;

        public StashReferenceViewModel(int graphColumn, StashRevision stash)
            : base(graphColumn)
        {
            _stash = stash;
        }
    }
}
