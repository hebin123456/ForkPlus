// ReferenceItem.cs：引用条目（POCO，分支/Tag）。
//
// WPF 源对照：src/ForkPlus/UI/QuickLaunch/ReferenceItem.cs（namespace ForkPlus.UI.QuickLaunch）
//   - class ReferenceItem : CommandProviderItem
//   - override ImageSource Icon => Reference is Tag ? TagIcon : BranchIcon
//   - override ImageSource SelectedIcon => Reference is Tag ? TagSelectedIcon : BranchSelectedIcon
//   - Reference Reference { get; }
//
// Avalonia 版差异：
//   1. namespace ForkPlus.UI.QuickLaunch → ForkPlus.Avalonia.QuickLaunch
//   2. ImageSource → IImage（Avalonia.Media.IImage）
//   3. Application.Current.TryFindResource(key) as ImageSource → GetIconResource(key)
//   4. ForkPlus.Git.Reference / Tag 来自 ForkPlus.Core（零修改复用，无 WPF 依赖）

using Avalonia.Media;
using ForkPlus.Git;

namespace ForkPlus.Avalonia.QuickLaunch
{
    public class ReferenceItem : CommandProviderItem
    {
        // 对照 WPF: public override ImageSource Icon
        public override IImage Icon
        {
            get
            {
                if (Reference is Tag)
                {
                    return GetIconResource("TagIcon");
                }
                return GetIconResource("BranchIcon");
            }
        }

        // 对照 WPF: public override ImageSource SelectedIcon
        public override IImage SelectedIcon
        {
            get
            {
                if (Reference is Tag)
                {
                    return GetIconResource("TagSelectedIcon");
                }
                return GetIconResource("BranchSelectedIcon");
            }
        }

        public Reference Reference { get; }

        public ReferenceItem(Reference reference, string fuzzySearchString)
            : base(reference, reference.Name, "")
        {
            Reference = reference;
            base.FuzzySearchString = fuzzySearchString;
        }
    }
}
