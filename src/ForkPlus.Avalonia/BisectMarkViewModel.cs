using ForkPlus.Git;

// Avalonia spike 版 BisectMarkViewModel（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/BisectMarkViewModel.cs（22 行）：
//   - WPF: public class BisectMarkViewModel : BranchViewModel
//   - BisectMark _bisectMark
//   - Reference Reference => _bisectMark
//   - string Name => "bisect: " + _bisectMark.ShortName
//   - bool IsGood => _bisectMark.IsGood
//   - string Image { get; }（图标路径）
//   - 构造 BisectMarkViewModel(int graphColumn, BisectMark bisectMark)
//   - 依赖：ForkPlus.Git.BisectMark（Core 可用）
//
// Avalonia 版差异：
//   1. POCO 无 WPF 依赖，零改动复用
//   2. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
//
// spike 简化：
//   - 与 WPF 完全一致的 POCO 类
namespace ForkPlus.Avalonia
{
    public class BisectMarkViewModel : BranchViewModel
    {
        private readonly BisectMark _bisectMark;

        public override Reference Reference => _bisectMark;
        public string Name => "bisect: " + _bisectMark.ShortName;
        public bool IsGood => _bisectMark.IsGood;
        public string Image { get; }

        public BisectMarkViewModel(int graphColumn, BisectMark bisectMark)
            : base(graphColumn)
        {
            _bisectMark = bisectMark;
        }
    }
}
