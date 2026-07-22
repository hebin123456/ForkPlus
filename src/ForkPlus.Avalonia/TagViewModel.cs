using ForkPlus.Git;

// Avalonia spike 版 TagViewModel（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/TagViewModel.cs（18 行）：
//   - WPF: public class TagViewModel : ReferenceViewModel
//   - Reference Reference => _tag
//   - string Name => _tag.Name
//   - 构造 TagViewModel(int graphColumn, Tag tag)
//   - 依赖：ForkPlus.Git.Tag（Core 可用）
//
// Avalonia 版差异：
//   1. POCO 无 WPF 依赖，零改动复用
//   2. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
//
// spike 简化：
//   - 与 WPF 完全一致的 POCO 类
namespace ForkPlus.Avalonia
{
    public class TagViewModel : ReferenceViewModel
    {
        private Tag _tag;

        public override Reference Reference => _tag;
        public string Name => _tag.Name;

        public TagViewModel(int graphColumn, Tag tag)
            : base(graphColumn)
        {
            _tag = tag;
        }
    }
}
