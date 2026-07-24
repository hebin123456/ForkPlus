using System.Globalization;
using Avalonia.Controls;
using ForkPlus.Settings;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.Controls
{
	// TODO(4.5-h): WPF SpellCheck.IsEnabled + SpellingError + GetSpellingError 在 Avalonia 中无内置等价物。
	// 当前为空实现；阶段 6 引入第三方拼写检查库（如 WeCantSpell.Hunspell）后恢复。
	// ContextMenu 的拼写建议菜单也已通过 MenuExtensions.AddSpellingMenuItems 改为空实现。
	public class SpellingPlaceholderTextBox : AutoCompleteTextBox
	{
		public SpellingPlaceholderTextBox()
		{
			base.ContextMenuOpening += delegate
			{
				base.ContextMenu = GetContextMenu();
				// 阶段 4.5：Avalonia TextBox 无 GetSpellingError；传 null 给 AddSpellingMenuItems（空实现）。
				base.ContextMenu.AddSpellingMenuItems(null, this);
			};
			NotificationCenter.Current.CommitSpellCheckingModeChanged += delegate
			{
				RefreshSpellChecking();
			};
			RefreshSpellChecking();
		}

		public void RefreshSpellChecking()
		{
			// 阶段 4.5：Avalonia TextBox 无 SpellCheck.IsEnabled 属性。
			// 保留方法以兼容现有调用方；CommitSpellCheckingMode 设置暂存为 no-op。
			switch (ForkPlusSettings.Default.CommitSpellCheckingMode)
			{
			case CommitSpellCheckingMode.Disable:
				// no-op
				break;
			case CommitSpellCheckingMode.System:
				// TODO(4.5-h): 接入第三方拼写检查库后启用系统语言。
				// base.Language = XmlLanguage.GetLanguage(CultureInfo.InstalledUICulture.Name);
				break;
			case CommitSpellCheckingMode.English:
				// TODO(4.5-h): 接入第三方拼写检查库后启用英语。
				// base.Language = XmlLanguage.GetLanguage("en-US");
				break;
			}
		}
	}
}
