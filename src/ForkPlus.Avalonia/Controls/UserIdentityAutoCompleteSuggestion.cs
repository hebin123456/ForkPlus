using ForkPlus.Git;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 UserIdentityAutoCompleteSuggestion（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/UserIdentityAutoCompleteSuggestion.cs（15 行）：
    //   - WPF UserIdentityAutoCompleteSuggestion : AutoCompleteSuggestion
    //   - UserIdentity UserIdentity { get; }
    //   - 构造函数传 userIdentity.Name + " <" + userIdentity.Email + ">" 作为 base.Suggestion
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. POCO 类无 WPF / Avalonia UI 依赖，零改动复用
    //   2. 命名空间 ForkPlus.UI.Controls → ForkPlus.Avalonia.Controls
    //   3. UserIdentity 类型来自 ForkPlus.Core.Git
    //   4. AutoCompleteSuggestion 基类来自本 spike 命名空间
    //
    // spike 简化：
    //   - 与 WPF 完全一致的 POCO 类
    public class UserIdentityAutoCompleteSuggestion : AutoCompleteSuggestion
    {
        // 对照 WPF: public UserIdentity UserIdentity { get; }
        public UserIdentity UserIdentity { get; }

        // 对照 WPF: public UserIdentityAutoCompleteSuggestion(Range range, UserIdentity userIdentity)
        //   : base(range, userIdentity.Name + " <" + userIdentity.Email + ">")
        public UserIdentityAutoCompleteSuggestion(Range range, UserIdentity userIdentity)
            : base(range, userIdentity.Name + " <" + userIdentity.Email + ">")
        {
            UserIdentity = userIdentity;
        }
    }
}
