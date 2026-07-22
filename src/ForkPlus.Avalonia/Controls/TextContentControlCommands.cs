// Avalonia 版 TextContentControlCommands（spike 简化版）。
//
// 对照 WPF 工程 src/ForkPlus/UI/Controls/TextContentControlCommands.cs（19 行）：
//   - 继承 CommandContainer
//   - 3 个惰性命令：OpenFileInExternalEditor / Copy / HunkHistory
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF CommandContainer 是实例类 → spike 版 CommandContainer 是 static class
//      故 TextContentControlCommands 不再继承，改为独立类 + 占位属性
//   2. 3 个命令 → spike 占位属性（返回 null）
namespace ForkPlus.Avalonia.Controls
{
    public class TextContentControlCommands
    {
        private object? _openFileInExternalEditor;
        private object? _copy;
        private object? _hunkHistory;

        // spike: 命令占位（实际命令实现待后续 phase 接入）
        public object? OpenFileInExternalEditor => _openFileInExternalEditor ??= new object();
        public object? Copy => _copy ??= new object();
        public object? HunkHistory => _hunkHistory ??= new object();
    }
}
