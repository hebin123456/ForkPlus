using System.Collections.Generic;
using System.Linq;

namespace ForkPlus.UI.Dialogs
{
	// 阶段 3：承接 partial stash 文件选中项纯数据投影 + stash message + 命令预览。
	// PartialStashFileViewModel（含 Selected 状态）作为列表项留 View；
	// VM 仅持选中文件路径列表 + stash message，IsSubmitAllowed => 至少一个选中。
	// AI 生成 stash name 留 View（依赖 OpenAiService + GitModule）。
	internal sealed class CreatePartialStashWindowViewModel
	{
		private IReadOnlyList<string> _selectedFilePaths = System.Array.Empty<string>();
		private string _stashMessage = string.Empty;

		public IReadOnlyList<string> SelectedFilePaths
		{
			get => _selectedFilePaths;
			set => _selectedFilePaths = value ?? System.Array.Empty<string>();
		}

		public string StashMessage
		{
			get => _stashMessage;
			set => _stashMessage = value ?? string.Empty;
		}

		public bool IsSubmitAllowed => _selectedFilePaths.Count > 0;

		public string CommandPreview
		{
			get
			{
				if (_selectedFilePaths.Count == 0)
				{
					return null;
				}
				var parts = new List<string> { "git", "stash", "push" };
				if (!string.IsNullOrWhiteSpace(_stashMessage))
				{
					string quoted = _stashMessage.IndexOf(' ') >= 0 ? ("\"" + _stashMessage + "\"") : _stashMessage;
					parts.Add("-m");
					parts.Add(quoted);
				}
				parts.Add("--");
				foreach (string f in _selectedFilePaths)
				{
					parts.Add(f.IndexOf(' ') >= 0 ? ("\"" + f + "\"") : f);
				}
				return string.Join(" ", parts);
			}
		}
	}
}
