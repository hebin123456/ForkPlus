namespace ForkPlus.Avalonia.Controls.Editor
{
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/HighlightingType.cs（26 行）：
    //   - public enum HighlightingType（21 个枚举值：Add/Remove/ExactAdd/ExactRemove/Service/
    //     MergeAdd/MergeRemove/MergeLocal/MergeRemote/MergeUnresolved/Alignment/
    //     SyntaxComment/SyntaxString/SyntaxKeyword/SyntaxType/SyntaxCommand/SyntaxAttribute/
    //     SyntaxVariable/SyntaxValue/SyntaxNumber/None）
    //   - 用于 diff 行背景色 / 语法高亮类型分类
    //
    // Avalonia 版差异：无（纯枚举，跨平台零改动，仅 namespace 改为
    // ForkPlus.Avalonia.Controls.Editor）。
    public enum HighlightingType
    {
        Add,
        Remove,
        ExactAdd,
        ExactRemove,
        Service,
        MergeAdd,
        MergeRemove,
        MergeLocal,
        MergeRemote,
        MergeUnresolved,
        Alignment,
        SyntaxComment,
        SyntaxString,
        SyntaxKeyword,
        SyntaxType,
        SyntaxCommand,
        SyntaxAttribute,
        SyntaxVariable,
        SyntaxValue,
        SyntaxNumber,
        None
    }
}
