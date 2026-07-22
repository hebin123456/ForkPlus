using System.Collections.Generic;
using ForkPlus.Git;

// Avalonia spike 版 RevisionContextSearch（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/RevisionContextSearch.cs（26 行）：
//   - WPF: public struct RevisionContextSearch
//   - HashSet<Sha> _matches
//   - int MatchCount => _matches.Count
//   - string SearchString { get; }
//   - HashSet<Sha> Matches => _matches
//   - 构造 RevisionContextSearch(string searchString, HashSet<Sha> matches)
//   - bool IsMatch(Sha sha) => _matches.Contains(sha)
//   - 依赖：ForkPlus.Git.Sha（Core 可用）
//
// Avalonia 版差异：
//   1. struct 无 WPF 依赖，零改动复用
//   2. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
//
// spike 简化：
//   - 与 WPF 完全一致的 struct
namespace ForkPlus.Avalonia
{
    public struct RevisionContextSearch
    {
        private readonly HashSet<Sha> _matches;

        public int MatchCount => _matches.Count;
        public string SearchString { get; }
        public HashSet<Sha> Matches => _matches;

        public RevisionContextSearch(string searchString, HashSet<Sha> matches)
        {
            SearchString = searchString;
            _matches = matches;
        }

        public bool IsMatch(Sha sha)
        {
            return _matches.Contains(sha);
        }
    }
}
