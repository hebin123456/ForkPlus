using System.Text;
using Avalonia.Input;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 ReferenceTextBox（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/ReferenceTextBox.cs（74 行）：
    //   - WPF ReferenceTextBox : AutoCompleteTextBox
    //   - DataObject.AddPastingHandler(this, OnPaste)
    //   - OnPreviewKeyDown(Key.Space)：在 CaretIndex 处插入 ReferenceSpaceCharacterReplacement
    //     （WPF PreviewKeyDown 是 tunneling 事件，Avalonia 无对应 tunneling，用 bubble KeyDown）
    //   - OnPaste：ReplaceInvalidCharactersWithSpace 替换粘贴文本中的非法字符
    //   - ReplaceInvalidCharactersWithSpace：替换 git 引用非法字符
    //     （空格/..//@{/\.lock~^:?*[ 等）为 ReferenceSpaceCharacterReplacement
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 AutoCompleteTextBox）：
    //   1. WPF AutoCompleteTextBox 基类（已迁移到 ForkPlus.Avalonia.Controls.AutoCompleteTextBox）
    //      → spike 继承 AutoCompleteTextBox（保持与 WPF 一致）
    //   2. WPF PreviewKeyDown (tunneling) → Avalonia KeyDown (bubble)
    //   3. WPF DataObject.AddPastingHandler → spike 跳过
    //      （Avalonia 无对应 paste hook API；改为公共 SanitizeText(string) 方法，
    //       调用方在粘贴前主动调用）
    //   4. WPF ForkPlusSettings.Default.ReferenceSpaceCharacterReplacement → spike 硬编码 "-"
    //      （spike 不依赖 ForkPlusSettings 单例；可通过 ReferenceSpaceReplacement 属性覆盖）
    //   5. WPF SystemSounds.Exclamation.Play → spike 跳过（无对应 Avalonia API）
    //   6. spike 保留 ReplaceInvalidCharactersWithSpace 算法（纯 C# 逻辑）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 AutoCompleteTextBox（保持 WPF 继承链）
    //   - KeyDown 监听 Space → 插入 ReferenceSpaceReplacement
    //   - SanitizeText 公共方法（替代 OnPaste 私有方法）
    //   - ReferenceSpaceReplacement 属性（默认 "-"，替代 ForkPlusSettings 单例）
    public class ReferenceTextBox : AutoCompleteTextBox
    {
        // 对照 WPF: ForkPlusSettings.Default.ReferenceSpaceCharacterReplacement
        // spike 版：暴露为公共属性，默认 "-"，调用方可覆盖
        public string ReferenceSpaceReplacement { get; set; } = "-";

        public ReferenceTextBox()
        {
            // 对照 WPF: protected override void OnPreviewKeyDown(KeyEventArgs e)
            //   if (e.Key == Key.Space) {
            //     int caretIndex = base.CaretIndex;
            //     base.Text = base.Text.Insert(caretIndex, ForkPlusSettings.Default.ReferenceSpaceCharacterReplacement);
            //     base.CaretIndex = caretIndex + 1;
            //     e.Handled = true; }
            // spike 版：KeyDown (bubble) 替代 PreviewKeyDown (tunneling)
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Space)
                {
                    int caretIndex = CaretIndex;
                    Text = (Text ?? string.Empty).Insert(caretIndex, ReferenceSpaceReplacement);
                    CaretIndex = caretIndex + 1;
                    e.Handled = true;
                }
            };

            // 对照 WPF: DataObject.AddPastingHandler(this, OnPaste);
            // spike 跳过：Avalonia 无对应 paste hook API，提供 SanitizeText 公共方法
            // 调用方在粘贴前主动调用 SanitizeText 清理文本
        }

        // spike 新增：替代 WPF OnPaste 私有方法
        // 对照 WPF: private void OnPaste(object sender, DataObjectPastingEventArgs e)
        //   string text = (string)e.DataObject.GetData(typeof(string));
        //   string data = ReplaceInvalidCharactersWithSpace(text);
        // spike 版：调用方在粘贴前主动调用 SanitizeText 清理文本
        public string SanitizeText(string text)
        {
            return ReplaceInvalidCharactersWithSpace(text);
        }

        // 对照 WPF: private string ReplaceInvalidCharactersWithSpace(string text)
        // 替换 git 引用非法字符为 ReferenceSpaceReplacement
        // spike 版完整移植，无 WPF 依赖
        private string ReplaceInvalidCharactersWithSpace(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text ?? string.Empty;
            }
            string replacement = ReferenceSpaceReplacement ?? "-";
            if (text == "@")
            {
                return replacement;
            }
            StringBuilder stringBuilder = new StringBuilder(text);
            stringBuilder.Replace(" ", replacement);
            stringBuilder.Replace("\n", replacement);
            stringBuilder.Replace("..", replacement);
            stringBuilder.Replace("//", replacement);
            stringBuilder.Replace("@{", replacement);
            stringBuilder.Replace("\\", replacement);
            stringBuilder.Replace("/.", replacement);
            stringBuilder.Replace(".lock", replacement);
            stringBuilder.Replace("~", replacement);
            stringBuilder.Replace("^", replacement);
            stringBuilder.Replace(":", replacement);
            stringBuilder.Replace("?", replacement);
            stringBuilder.Replace("*", replacement);
            stringBuilder.Replace("[", replacement);
            stringBuilder.Replace(System.Environment.NewLine, replacement);
            return stringBuilder.ToString();
        }
    }
}
