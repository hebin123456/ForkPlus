// 对照 WPF 工程 src/ForkPlus/UI/UserControls/Preferences/AiSkillEntry.cs（14 行）：
//   - public class AiSkillEntry { string Name; string Content; }
//   - 纯 POCO，无 WPF 依赖，零改动迁移
//
// namespace 改为 ForkPlus.Avalonia.Views.UserControls.Preferences
namespace ForkPlus.Avalonia.Views.UserControls.Preferences
{
    public class AiSkillEntry
    {
        public string Name { get; set; }
        public string Content { get; set; }
    }
}
