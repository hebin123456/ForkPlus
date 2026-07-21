using Avalonia.Controls;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 SpellingPlaceholderTextBox（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/SpellingPlaceholderTextBox.cs（46 行）：
    //   - WPF SpellingPlaceholderTextBox : AutoCompleteTextBox（自定义基类）
    //   - 构造函数：
    //     - ContextMenuOpening：base.ContextMenu = GetContextMenu() +
    //       GetSpellingError(CaretIndex) + AddSpellingMenuItems(spellingError, this)
    //     - NotificationCenter.CommitSpellCheckingModeChanged 弱事件订阅 → RefreshSpellChecking
    //     - RefreshSpellChecking()
    //   - RefreshSpellChecking：
    //     - Disable → SpellCheck.IsEnabled = false
    //     - System → SpellCheck.IsEnabled = true + Language = CultureInfo.InstalledUICulture
    //     - English → SpellCheck.IsEnabled = true + Language = "en-US"
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 TextBox + Watermark + 拼写检查省略）：
    //   1. WPF AutoCompleteTextBox 基类（已迁移到 ForkPlus.Avalonia.Controls.AutoCompleteTextBox）
    //      → spike 继承 AutoCompleteTextBox（保持与 WPF 一致）
    //   2. WPF SpellCheck.IsEnabled + Language → Avalonia 无内置 SpellCheck API
    //      spike 跳过拼写检查功能（task spec 明确要求：拼写检查省略）
    //   3. WPF ContextMenuOpening + GetSpellingError + AddSpellingMenuItems
    //      → spike 跳过（依赖 WPF SpellCheck API）
    //   4. WPF NotificationCenter 弱事件订阅 → spike 跳过（NotificationCenter 在 WPF 工程）
    //   5. WPF ForkPlusSettings.Default.CommitSpellCheckingMode → spike 跳过
    //      （spike 不依赖 ForkPlusSettings 单例）
    //   6. spike 保留 RefreshSpellChecking() 公共方法签名（空实现，保留 API 形状）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 AutoCompleteTextBox（保持 WPF 继承链）
    //   - RefreshSpellChecking() 公共方法（空实现，spike 不依赖 SpellCheck）
    public class SpellingPlaceholderTextBox : AutoCompleteTextBox
    {
        public SpellingPlaceholderTextBox()
        {
            // 对照 WPF: ContextMenuOpening + GetSpellingError + AddSpellingMenuItems
            // spike 版跳过：Avalonia 无内置 SpellCheck API
            // 对照 WPF: NotificationCenter.CommitSpellCheckingModeChanged 弱事件订阅
            // spike 版跳过：NotificationCenter 在 WPF 工程，不可访问
            // 对照 WPF: RefreshSpellChecking()
            // spike 版跳过：无 SpellCheck API
        }

        // 对照 WPF: public void RefreshSpellChecking()
        //   switch (ForkPlusSettings.Default.CommitSpellCheckingMode) {
        //     case Disable: SpellCheck.IsEnabled = false; break;
        //     case System: SpellCheck.IsEnabled = true; Language = CultureInfo.InstalledUICulture; break;
        //     case English: SpellCheck.IsEnabled = true; Language = "en-US"; break; }
        // spike 版空实现：Avalonia 无 SpellCheck API，保留方法签名兼容调用方
        public void RefreshSpellChecking()
        {
            // spike 版跳过：Avalonia 11 无内置 SpellCheck API
            // 真实拼写检查由后续 Phase 通过第三方库（如 NHunspell）接入
        }
    }
}
