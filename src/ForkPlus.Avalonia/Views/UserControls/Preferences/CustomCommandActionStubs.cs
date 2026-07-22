// 对照 WPF 工程 src/ForkPlus/UI/CustomCommands/ 下的 4 个 CustomCommandAction 子类：
//   - ProcessCustomCommandAction.cs（139 行）：TypeKey="process"，Path/Parameters/ShowOutput/WaitForExit
//   - ShCustomCommandAction.cs（139 行）：TypeKey="sh"，Script/ShowOutput/WaitForExit，Path=App.BashPath
//   - UrlCustomCommandAction.cs（44 行）：TypeKey="url"，Url 属性
//   - CancelCustomCommandAction.cs（30 行）：TypeKey="cancel"，无属性
//
// 这 4 个子类在 WPF 工程中，有重 WPF 依赖（RepositoryUserControl / ErrorWindow /
// CustomActionResultWindow / Process / App.BashPath 等），未迁入 Core。
// Avalonia 工程的 Preferences ViewModel 需要引用这些类型（is 类型判断 + new 创建默认实例），
// spike 阶段创建最小 stub（继承 CustomCommandAction + 保留构造函数和属性 + Execute 空实现）。
//
// spike 简化策略：
//   - TypeKey / WriteProperties / CustomCommandEquals：保留完整实现（纯 C#，无 WPF 依赖）
//   - Execute：空实现 + 注释（WPF 版依赖 RepositoryUserControl / ErrorWindow /
//     CustomActionResultWindow / JobQueue / Process，spike 阶段不接入）
//   - 属性 + 构造函数：保留完整签名（ViewModel 需要读取 Path/Parameters/Url 等属性）
//   - ShCustomCommandAction.Path：WPF 版返回 App.BashPath，spike 版返回 "" （App 在 WPF 工程）
//   - ShCustomCommandAction.DefaultScript：保留完整实现（纯 C#，无 WPF 依赖）
//
// namespace 保持 ForkPlus.UI.CustomCommands（与 Core 基类同命名空间，ViewModel 的
// using ForkPlus.UI.CustomCommands 可直接找到 stub 类型）
using Newtonsoft.Json.Linq;

namespace ForkPlus.UI.CustomCommands
{
    // 对照 WPF: public class UrlCustomCommandAction : CustomCommandAction
    // spike stub：Execute 空实现（WPF 版用 Uri.OpenInBrowser，spike 不接入）
    public class UrlCustomCommandAction : CustomCommandAction
    {
        public new static class Keys
        {
            public const string Type = "url";
            public const string Url = "url";
        }

        public override string TypeKey => Keys.Type;

        public override void WriteProperties(JObject jObject)
        {
            jObject.Add(Keys.Url, new JValue(Url));
        }

        public string Url { get; }

        public UrlCustomCommandAction(string url)
        {
            Url = url;
        }

        public override bool CustomCommandEquals(CustomCommandAction other)
        {
            return other is UrlCustomCommandAction u && Url == u.Url;
        }

        public override void Execute(object repositoryView, string customCommandName, CustomCommandEnvironment env)
        {
            // spike 空实现：WPF 版调 env.ReplaceVariablesWithValues(Url, urlEncode: true) + Uri.OpenInBrowser
        }
    }

    // 对照 WPF: public class CancelCustomCommandAction : CustomCommandAction
    // spike stub：Execute 空实现（WPF 版本身就是空实现）
    public class CancelCustomCommandAction : CustomCommandAction
    {
        public new static class Keys
        {
            public const string Type = "cancel";
        }

        public override string TypeKey => Keys.Type;

        public override void WriteProperties(JObject jObject)
        {
        }

        public override bool CustomCommandEquals(CustomCommandAction other)
        {
            return other is CancelCustomCommandAction;
        }

        public override void Execute(object repositoryView, string customCommandName, CustomCommandEnvironment env)
        {
        }
    }

    // 对照 WPF: public class ProcessCustomCommandAction : CustomCommandAction
    // spike stub：Execute 空实现（WPF 版依赖 RepositoryUserControl / ErrorWindow / JobQueue / Process）
    public class ProcessCustomCommandAction : CustomCommandAction
    {
        public new static class Keys
        {
            public const string Type = "process";
            public const string Path = "path";
            public const string Arguments = "args";
            public const string ShowOutput = "showOutput";
            public const string WaitForExit = "waitForExit";
        }

        public override string TypeKey => Keys.Type;

        public override void WriteProperties(JObject jObject)
        {
            jObject.Add(Keys.Path, new JValue(Path));
            jObject.Add(Keys.Arguments, new JValue(Parameters));
            jObject.Add(Keys.ShowOutput, new JValue(ShowOutput));
            jObject.Add(Keys.WaitForExit, new JValue(WaitForExit));
        }

        public string Path { get; }
        public string Parameters { get; }
        public bool ShowOutput { get; }
        public bool WaitForExit { get; }

        public ProcessCustomCommandAction(string path, string parameters, bool showOutput, bool waitForExit)
        {
            Path = path;
            Parameters = parameters;
            ShowOutput = showOutput;
            WaitForExit = waitForExit;
        }

        public override bool CustomCommandEquals(CustomCommandAction other)
        {
            return other is ProcessCustomCommandAction p
                && Path == p.Path
                && Parameters == p.Parameters
                && ShowOutput == p.ShowOutput
                && WaitForExit == p.WaitForExit;
        }

        public override void Execute(object repositoryView, string customCommandName, CustomCommandEnvironment env)
        {
            // spike 空实现：WPF 版用 Process.Start + JobQueue + ErrorWindow + CustomActionResultWindow
        }
    }

    // 对照 WPF: public class ShCustomCommandAction : CustomCommandAction
    // spike stub：Execute 空实现（WPF 版依赖 RepositoryUserControl / ErrorWindow / JobQueue / Process）
    public class ShCustomCommandAction : CustomCommandAction
    {
        public new static class Keys
        {
            public const string Type = "sh";
            public const string Script = "script";
            public const string ShowOutput = "showOutput";
            public const string WaitForExit = "waitForExit";
        }

        public override string TypeKey => Keys.Type;

        public override void WriteProperties(JObject jObject)
        {
            jObject.Add(Keys.Script, new JValue(Script));
            jObject.Add(Keys.ShowOutput, new JValue(ShowOutput));
            jObject.Add(Keys.WaitForExit, new JValue(WaitForExit));
        }

        public string Script { get; }
        public bool ShowOutput { get; }
        public bool WaitForExit { get; }

        // 对照 WPF: public string Path => App.BashPath;
        // spike 版：App 在 WPF 工程，spike 返回 "" （spike 阶段不执行 sh 命令）
        public string Path => "";

        public ShCustomCommandAction(string script, bool showOutput, bool waitForExit)
        {
            Script = script;
            ShowOutput = showOutput;
            WaitForExit = waitForExit;
        }

        public override bool CustomCommandEquals(CustomCommandAction other)
        {
            return other is ShCustomCommandAction s
                && Script == s.Script
                && ShowOutput == s.ShowOutput
                && WaitForExit == s.WaitForExit;
        }

        public override void Execute(object repositoryView, string customCommandName, CustomCommandEnvironment env)
        {
            // spike 空实现：WPF 版用 Process.Start(bash, "-c " + script) + JobQueue + ErrorWindow
        }

        // 对照 WPF: public static string DefaultScript(CustomCommandTarget target)
        // 纯 C# 静态方法，无 WPF 依赖，完整保留
        public static string DefaultScript(CustomCommandTarget target)
        {
            string FileDefaultScript = "count=$(git log --oneline -- ${file} | wc -l)\n\necho ${file:name} changes count: $count";
            string ReferenceDefaultScript = "count=$(git log --oneline ${ref:full} | wc -l)\n\necho ${ref} commits count: $count";
            string RevisionDefaultScript = "echo ${sha:abbr} changed files:\n\ngit diff --name-only ${sha}~1 ${sha}";
            string RepositoryDefaultScript = "echo ${repo:name} status:\n\ngit status --porcelain";
            string SubmoduleDefaultScript = "echo ${submodule} status:\n\ngit submodule update --remote -- ${submodule}";

            return target switch
            {
                CustomCommandTarget.Revision => RevisionDefaultScript,
                CustomCommandTarget.Repository => RepositoryDefaultScript,
                CustomCommandTarget.RepositoryFile => FileDefaultScript,
                CustomCommandTarget.Reference => ReferenceDefaultScript,
                CustomCommandTarget.Submodule => SubmoduleDefaultScript,
                _ => "",
            };
        }
    }
}
