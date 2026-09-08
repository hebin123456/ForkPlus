using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// 自动补全建议列表项数据模板分发器：按建议运行时类型选择对应模板构建视觉树。
	/// Migration note（2026-09-05 修复"补全建议浮层显示类名"）：原 WPF 把三个按 DataType 的
	/// DataTemplate（AutoCompleteSuggestion / UserIdentityAutoCompleteSuggestion / GitmojiAutoCompleteSuggestion）
	/// 放在 ListBoxItem ControlTemplate.Resources 内，AutoCompleteTextBox.OpenPopup 取复合模板
	/// "AutocompleteListBoxItemTemplate"；迁移时 ControlTemplate.Resources 无 Avalonia 等价物，模板被注释化
	/// 但代码侧查找键保留 → ItemTemplate=null → 列表按 ToString() 渲染出类名。
	/// 修复：三个模板恢复为 Listview.axaml 顶层资源，本分发器按类型路由，行为对齐原版。
	/// </summary>
	public class AutoCompleteSuggestionTemplateSelector : IDataTemplate
	{
		private const string PlainTemplateKey = "AutoCompleteSuggestionTemplate";

		private const string UserIdentityTemplateKey = "UserIdentityAutoCompleteSuggestionTemplate";

		private const string GitmojiTemplateKey = "GitmojiAutoCompleteSuggestionTemplate";

		public bool Match(object? data)
		{
			return data is AutoCompleteSuggestion;
		}

		public Control? Build(object? param)
		{
			string key = PlainTemplateKey;
			if (param is UserIdentityAutoCompleteSuggestion)
			{
				key = UserIdentityTemplateKey;
			}
			else if (param is GitmojiAutoCompleteSuggestion)
			{
				key = GitmojiTemplateKey;
			}
			return (Application.Current?.TryFindResource(key) as IDataTemplate)?.Build(param);
		}
	}
}
