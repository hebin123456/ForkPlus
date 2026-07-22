// SpikeTypes.cs：QuickLaunch 迁移所需的 spike POCO 类型。
//
// WPF 源对照（均在 WPF 工程 src/ForkPlus 中，Avalonia 不可访问）：
//   - ArgumentType 枚举：src/ForkPlus/UI/Commands/ArgumentType.cs（namespace ForkPlus.UI.Commands）
//   - Argument 类：src/ForkPlus/UI/Commands/Argument.cs（namespace ForkPlus.UI.Commands）
//   - CommandDescriptor 类：src/ForkPlus/UI/Commands/CommandDescriptor.cs（namespace ForkPlus.UI.Commands）
//   - RepositoryManager 类：src/ForkPlus/RepositoryManager.cs（namespace ForkPlus，含 Repository 嵌套类）
//   - IconTools 类：src/ForkPlus/UI/UserControls/IconTools.cs（namespace ForkPlus.UI.UserControls）
//
// Avalonia 版差异（spike 简化策略）：
//   1. ArgumentType / Argument / CommandDescriptor 从 ForkPlus.UI.Commands（WPF 工程）
//      迁移到 ForkPlus.Avalonia.QuickLaunch 命名空间（spike POCO，零 WPF 依赖）
//   2. CommandDescriptor.CallConverter 委托的 RepositoryUserControl 参数改为 object
//      （RepositoryUserControl 为 WPF 专有类型，spike 用 object 占位）
//   3. RepositoryManager 简化为 spike：Repository 嵌套类仅保留 Name / Path / Opened 等最小字段，
//      Instance.Repositories 返回空数组（spike 不接入真实仓库管理器单例）
//   4. IconTools.GetImageSourceForExtension 返回 null（spike 不迁移文件扩展名图标解析逻辑）

using System;
using Avalonia.Media;

namespace ForkPlus.Avalonia.QuickLaunch
{
    // 对照 WPF: ForkPlus.UI.Commands.ArgumentType（namespace ForkPlus.UI.Commands）
    public enum ArgumentType
    {
        Reference,
        Tag,
        Branch,
        LocalBranch,
        RemoteBranch,
        FeatureBranch,
        HotfixBranch,
        ReleaseBranch,
        RepositoryFile,
        Remote,
        Workspace,
        Default
    }

    // 对照 WPF: ForkPlus.UI.Commands.Argument（namespace ForkPlus.UI.Commands）
    public class Argument
    {
        public string Name { get; }

        public ArgumentType Type { get; }

        public object Tag { get; }

        public Argument(ArgumentType type, string name = null, object tag = null)
        {
            Name = name ?? CreateDefaultName(type);
            Type = type;
            Tag = tag;
        }

        private static string CreateDefaultName(ArgumentType type)
        {
            return type switch
            {
                ArgumentType.Tag => "tag",
                ArgumentType.LocalBranch => "branch",
                ArgumentType.RemoteBranch => "remote branch",
                ArgumentType.Branch => "branch",
                ArgumentType.Reference => "branch or tag",
                ArgumentType.FeatureBranch => "feature branch",
                ArgumentType.HotfixBranch => "hotfix branch",
                ArgumentType.ReleaseBranch => "release branch",
                ArgumentType.Remote => "remote",
                ArgumentType.RepositoryFile => "file",
                ArgumentType.Workspace => "workspace",
                _ => throw new Exception($"Default name must be defined for argument type {type}"),
            };
        }
    }

    // 对照 WPF: ForkPlus.UI.Commands.CommandDescriptor（namespace ForkPlus.UI.Commands）
    // spike 差异：CallConverter 委托的 RepositoryUserControl 参数改为 object
    public class CommandDescriptor
    {
        // 对照 WPF: public delegate void CallConverter(object[] arguments, RepositoryUserControl repositoryUserControl)
        // spike: RepositoryUserControl → object（WPF 专有类型，spike 占位）
        public delegate void CallConverter(object[] arguments, object repositoryUserControl);

        public string Name { get; }

        public Argument[] Arguments { get; }

        public CallConverter Converter { get; }

        public CommandDescriptor(string name, Argument[] arguments, CallConverter converter)
        {
            Name = name;
            Arguments = arguments;
            Converter = converter;
        }
    }

    // 对照 WPF: ForkPlus.RepositoryManager（namespace ForkPlus，src/ForkPlus/RepositoryManager.cs）
    // spike 简化：Repository 嵌套类仅保留最小字段，Instance.Repositories 返回空数组
    public class RepositoryManager
    {
        // 对照 WPF: public class Repository（嵌套在 RepositoryManager 中）
        public class Repository
        {
            public string Name { get; set; }

            public string Path { get; set; }

            public bool? Opened { get; set; }

            public string Color { get; set; }

            public string Alias { get; set; }

            public string GetDisplayName() => Alias ?? Name;

            public Repository(string name = null, string path = null)
            {
                Name = name;
                Path = path;
            }
        }

        // 对照 WPF: public static readonly RepositoryManager Instance
        public static readonly RepositoryManager Instance = new RepositoryManager();

        // 对照 WPF: public Repository[] Repositories
        // spike: 返回空数组（不接入真实仓库管理器单例）
        public Repository[] Repositories { get; } = new Repository[0];
    }

    // 对照 WPF: ForkPlus.UI.UserControls.IconTools（namespace ForkPlus.UI.UserControls）
    // spike 简化：GetImageSourceForExtension 返回 null（不迁移文件扩展名图标解析逻辑）
    public static class IconTools
    {
        // 对照 WPF: public static ImageSource GetImageSourceForExtension(string extension)
        // spike: ImageSource → IImage，返回 null
        public static IImage GetImageSourceForExtension(string extension)
        {
            return null;
        }
    }
}
