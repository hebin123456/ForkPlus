using System.Globalization;
using ForkPlus.UI.WpfCompat;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using ForkPlus.Settings;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class SpellingPlaceholderTextBox : AutoCompleteTextBox
	{
		public SpellingPlaceholderTextBox()
		{
			// Migration note：WPF 用 base 关键字把自身传给 ContextMenuOpening 安装器；C# 不允许把 base 当作
			// 值传参（CS0175），Avalonia 侧 this 与 base 指向同一实例，改传 this。
			global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AddContextMenuOpeningHandler(this,delegate
			{
				base.ContextMenu = GetContextMenu();
				// Migration note：WPF TextBox.GetSpellingError(caretIndex) 返回光标处拼写错误以生成"更正"菜单项；
				// Avalonia 无 SpellCheck/GetSpellingError API，拼写纠错菜单项暂不可用（传 null，仅保留常规菜单）。
				SpellingError spellingError = null;
				base.ContextMenu.AddSpellingMenuItems(spellingError, this);
			});
			if (!global::ForkPlus.DesignTimeHelper.IsInDesignMode())
			{
				WeakEventManager<NotificationCenter, EventArgs<CommitSpellCheckingMode>>.AddHandler(NotificationCenter.Current,"CommitSpellCheckingModeChanged",delegate
(object sender, global::System.EventArgs e)				{
					RefreshSpellChecking();
				});
			}
			RefreshSpellChecking();
		}

		public void RefreshSpellChecking()
		{
			// Migration note：WPF 通过 SpellCheck.IsEnabled + Language(XmlLanguage) 切换系统/英文拼写检查；
			// Avalonia TextBox 无这两个属性，拼写检查暂整体禁用，待引入跨平台拼写检查方案后恢复。
			switch (ForkPlusSettings.Default.CommitSpellCheckingMode)
			{
			case CommitSpellCheckingMode.Disable:
			case CommitSpellCheckingMode.System:
			case CommitSpellCheckingMode.English:
				break;
			}
		}
	}
}
