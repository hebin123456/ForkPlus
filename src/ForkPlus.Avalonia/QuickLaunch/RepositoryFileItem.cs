// RepositoryFileItem.cs：仓库文件条目（POCO）。
//
// WPF 源对照：src/ForkPlus/UI/QuickLaunch/RepositoryFileItem.cs（namespace ForkPlus.UI.QuickLaunch）
//   - class RepositoryFileItem : CommandProviderItem
//   - override ImageSource Icon => IconTools.GetImageSourceForExtension(Path.GetExtension(FilePath))
//   - override ImageSource SelectedIcon => IconTools.GetImageSourceForExtension(Path.GetExtension(FilePath))
//   - string FilePath { get; }
//   - 构造函数：base(filePath, GetFileName(filePath), filePath)
//
// Avalonia 版差异：
//   1. namespace ForkPlus.UI.QuickLaunch → ForkPlus.Avalonia.QuickLaunch
//   2. ImageSource → IImage（Avalonia.Media.IImage）
//   3. IconTools 从 ForkPlus.UI.UserControls（WPF 工程）→ spike POCO（同命名空间，见 SpikeTypes.cs）
//      spike GetImageSourceForExtension 返回 null
//   4. System.IO.Path 来自 BCL（零修改复用）

using System.IO;
using Avalonia.Media;

namespace ForkPlus.Avalonia.QuickLaunch
{
    public class RepositoryFileItem : CommandProviderItem
    {
        // 对照 WPF: public override ImageSource Icon => IconTools.GetImageSourceForExtension(Path.GetExtension(FilePath))
        // spike: IconTools.GetImageSourceForExtension 返回 null
        public override IImage Icon => IconTools.GetImageSourceForExtension(Path.GetExtension(FilePath));

        // 对照 WPF: public override ImageSource SelectedIcon => IconTools.GetImageSourceForExtension(Path.GetExtension(FilePath))
        public override IImage SelectedIcon => IconTools.GetImageSourceForExtension(Path.GetExtension(FilePath));

        public string FilePath { get; }

        public RepositoryFileItem(string filePath)
            : base(filePath, GetFileName(filePath), filePath)
        {
            FilePath = filePath;
        }

        private static string GetFileName(string filePath)
        {
            try
            {
                return Path.GetFileName(filePath);
            }
            catch
            {
                return "";
            }
        }
    }
}
