// spike 版 RepositoryManager stub
// 对照 WPF 工程 src/ForkPlus/RepositoryManager.cs（WPF 工程，未迁入 Core）
//
// spike 简化策略：
//   1. WPF RepositoryManager.Instance 单例 → spike 保留单例模式
//   2. SourceDirs → 直接读写 ForkPlusSettings.Default.RepositoryManager.SourceDirectories
//   3. RemoveSourceDir → 重新设置 SourceDirectories + Save
//   4. 其他 API（Repository 列表/分类/扫描）→ spike 占位空实现
using System.Linq;
using ForkPlus.Settings;

namespace ForkPlus
{
    // spike: 保留在 ForkPlus 命名空间（与 WPF 一致），让调用方零改动
    public class RepositoryManager
    {
        public static readonly RepositoryManager Instance = new RepositoryManager();

        public string[] SourceDirs => ForkPlusSettings.Default.RepositoryManager.SourceDirectories ?? new string[0];

        public void SetSourceDirs(string[] sourceDirs)
        {
            var settings = ForkPlusSettings.Default.RepositoryManager;
            ForkPlusSettings.Default.RepositoryManager =
                new ForkPlusSettings.RepositoryManagerSettings(sourceDirs, settings.Categories, settings.Repositories, settings.ScanDepth);
            ForkPlusSettings.Default.Save();
        }

        public void RemoveSourceDir(string path)
        {
            var current = SourceDirs.ToList();
            current.Remove(path);
            SetSourceDirs(current.ToArray());
        }
    }
}
