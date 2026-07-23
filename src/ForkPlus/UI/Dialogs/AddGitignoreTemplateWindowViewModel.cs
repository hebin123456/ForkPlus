using System.Collections.Generic;

namespace ForkPlus.UI.Dialogs
{
	// 阶段 3：承接 .gitignore 模板选中项纯数据投影 + IsSubmitAllowed。
	// TemplateMarkers/TemplateGroups/LoadTemplates/BuildTemplateList/PreselectTemplates 全部留 View
	// （依赖 WPF CheckBox/TextBlock + 嵌入资源 + Run/Hyperlink）。
	// VM 仅持选中模板名列表，IsSubmitAllowed => Count > 0。
	internal sealed class AddGitignoreTemplateWindowViewModel
	{
		private IReadOnlyList<string> _selectedTemplateNames = System.Array.Empty<string>();

		public IReadOnlyList<string> SelectedTemplateNames
		{
			get => _selectedTemplateNames;
			set => _selectedTemplateNames = value ?? System.Array.Empty<string>();
		}

		public bool IsSubmitAllowed => _selectedTemplateNames.Count > 0;
	}
}
