// spike: 对照 WPF 工程 src/ForkPlus/UI/Commands/IPaletteCommand.cs
//   - WPF: public interface IPaletteCommand { string Title { get; } }
//     （命令面板接口，仅用于标记可出现在 Quick Launch palette）
//   - spike: 继承 spike 版 IForkPlusCommand，复用 Header（替代 WPF Title）。
//   - 调用方：ShowQuickLaunchWindowCommand 通过 IPaletteCommand 过滤可出现在 palette 的命令。
namespace ForkPlus.Avalonia.Commands
{
    /// <summary>
    /// spike 版命令面板接口。对照 WPF IPaletteCommand，
    /// 继承 IForkPlusCommand（Header 即 palette 显示文本）。
    /// </summary>
    public interface IPaletteCommand : IForkPlusCommand
    {
    }
}
