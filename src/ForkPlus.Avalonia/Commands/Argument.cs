// spike: 对照 WPF 工程 src/ForkPlus/UI/Commands/Argument.cs
//   - WPF: public class Argument { string Name; ArgumentType Type; object Tag; }
//     + CreateDefaultName(ArgumentType) switch 表达式（tag/branch/remote branch/...）
//   - spike: 1:1 移植（无 WPF 依赖，纯数据类，零改动）。
//   - 调用方：命令描述符的参数列表，spike 阶段大部分命令空参数。
using System;

namespace ForkPlus.Avalonia.Commands
{
    /// <summary>
    /// spike 版命令参数。对照 WPF Argument 1:1 移植（无 WPF 依赖）。
    /// </summary>
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
}
