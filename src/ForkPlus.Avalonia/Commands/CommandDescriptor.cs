// spike: 对照 WPF 工程 src/ForkPlus/UI/Commands/CommandDescriptor.cs
//   - WPF: public class CommandDescriptor { string Name; Argument[] Arguments; CallConverter Converter; }
//     （命令描述符，用于 RepositoryUserControl.Commands 字典按名查找命令并转换参数）
//   - spike: 简化为纯数据 DTO（Id / Header / Icon / ShortcutText），
//     用于菜单 / 工具栏 / 命令面板展示命令元数据。
//     去掉 WPF CallConverter delegate（参数转换逻辑迁移到调用方）。
namespace ForkPlus.Avalonia.Commands
{
    /// <summary>
    /// spike 版命令描述符。对照 WPF CommandDescriptor，
    /// 简化为纯元数据 DTO，用于菜单 / 工具栏 / 命令面板展示。
    /// </summary>
    public class CommandDescriptor
    {
        public string Id { get; }
        public string Header { get; }
        public string Icon { get; }
        public string ShortcutText { get; }

        public CommandDescriptor(string id, string header, string icon = "", string shortcutText = "")
        {
            Id = id;
            Header = header;
            Icon = icon;
            ShortcutText = shortcutText;
        }
    }
}
