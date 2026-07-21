using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 BinaryFileContentControl（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/BinaryFileContentControl.cs（191 行）：
    //   - WPF BinaryFileContentControl : Grid
    //   - 内嵌 BinaryContentUserControl（WPF UserControl，显示二进制文件信息）
    //   - SetContent(GitModule gitModule, BinaryContent binaryContent)：
    //     LfsContent → 加载 LfsPointer + 文件名
    //     LfsImage → GitLfsGetCachedFileGitCommand 加载缓存图片
    //     .tga → BinaryDiffUserControl.DecodeImageData 解码
    //   - ShowLfsImageButtonClick → StartSmudgeLfsImageJob 异步 smudge LFS 图片
    //   - CancelLfsButtonClick → _activeSmudgeJob.Monitor.Cancel()
    //   - SaveAsMenuItemClick → File.WriteAllBytes 保存 LFS 文件
    //   - JobQueue + Job + JobMonitor 异步任务管理
    //
    // Avalonia 版差异（spike 简化策略，task spec：用 TextBlock 显示 "Binary file"）：
    //   1. WPF Grid 基类 → spike 直接继承 UserControl
    //   2. WPF BinaryContentUserControl → spike 用 TextBlock 显示 "Binary file" 占位
    //   3. spike 跳过 LFS smudge / 图片解码 / .tga 处理
    //   4. spike 跳过 JobQueue 异步任务管理
    //   5. spike 跳过 SaveAsMenuItemClick 文件保存
    //   6. WPF ShowLfsImageButtonClick / CancelLfsButtonClick → spike 跳过
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 UserControl
    //   - 内嵌 1 个 TextBlock 显示 "Binary file"
    //   - SetContent(string fileName) 公共方法设置文件名
    public class BinaryFileContentControl : UserControl
    {
        // spike: 内嵌 TextBlock 显示 "Binary file"
        // 对照 WPF: BinaryContentUserControl（复杂 UserControl）
        private readonly TextBlock _textBlock;

        public BinaryFileContentControl()
        {
            // spike: 用 TextBlock 显示 "Binary file"（task spec 关键 API）
            _textBlock = new TextBlock
            {
                Text = "Binary file",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88))
            };
            Content = _textBlock;
        }

        // spike 公共方法：设置二进制文件名（显示在 TextBlock）
        // 对照 WPF: SetContent(GitModule gitModule, BinaryContent binaryContent)
        // spike: 仅接收文件名字符串，显示 "Binary file: <fileName>"
        public void SetContent(string fileName)
        {
            _textBlock.Text = string.IsNullOrEmpty(fileName) ? "Binary file" : $"Binary file: {fileName}";
        }
    }
}
