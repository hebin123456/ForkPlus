using System.Linq;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.14b：Avalonia 版 GitMmCommandPreviewHelper（对照 WPF GitMmCommandPreviewHelper.cs）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/GitMmCommandPreviewHelper.cs（24 行）：
    //   - internal static class GitMmCommandPreviewHelper
    //   - public static string Format(string[] args):
    //     * return "git mm " + string.Join(" ", args.Select(QuoteIfNeeded));
    //   - private static string QuoteIfNeeded(string value):
    //     * 空字符串 → "\"\""
    //     * 不含空格/Tab → 原样
    //     * 含空格/Tab → "\"...\"...\"" (转义 \ 和 ")
    //
    // 用于 GitMmSyncWindow / GitMmStartWindow / GitMmUploadWindow 的命令预览格式化。
    // spike 阶段：与 WPF 版逻辑完全一致，仅在 Avalonia 命名空间下重新声明。
    internal static class GitMmCommandPreviewHelper
    {
        public static string Format(string[] args)
        {
            return "git mm " + string.Join(" ", args.Select(QuoteIfNeeded));
        }

        private static string QuoteIfNeeded(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }
            if (value.IndexOfAny(new char[2] { ' ', '\t' }) < 0)
            {
                return value;
            }
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
