// 对照 WPF 工程 src/ForkPlus/UI/UserControls/Preferences/ActionType.cs（8 行）：
//   - public enum ActionType { Action, UI }
//   - 纯枚举，无 WPF 依赖，零改动迁移
//
// namespace 改为 ForkPlus.Avalonia.Views.UserControls.Preferences
namespace ForkPlus.Avalonia.Views.UserControls.Preferences
{
    public enum ActionType
    {
        Action,
        UI
    }
}
