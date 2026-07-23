using System.Collections.Generic;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	// 阶段 3：承接 EditRemote 的远端名/URL 校验 + 命令预览。
	// 复用 Validate() 三元组：name/url 非空 + (name/url 至少一项变化) + ReferenceNameValidator(name) + 不重名 → Warning。
	// 重名消息 "Remote '...' already exists" 用原文（不翻译）。
	// AccountItem 嵌套类（含 WPF ImageSource）/TestButton/NetworkProtocolContextMenu/AccountsComboBox 留 View。
	internal sealed class EditRemoteWindowViewModel
	{
		private readonly Remote[] _remotes;
		private readonly Remote _remoteToEdit;

		private string _remoteName = string.Empty;
		private string _repositoryUrl = string.Empty;

		public EditRemoteWindowViewModel(Remote[] remotes, Remote remoteToEdit)
		{
			_remotes = remotes ?? System.Array.Empty<Remote>();
			_remoteToEdit = remoteToEdit;
		}

		public string RemoteName
		{
			get => _remoteName;
			set => _remoteName = value ?? string.Empty;
		}

		public string RepositoryUrl
		{
			get => _repositoryUrl;
			set => _repositoryUrl = value ?? string.Empty;
		}

		public (bool IsAllowed, ForkPlusDialogStatus Status, string StatusMessage) Validate()
		{
			string remoteName = _remoteName.Trim();
			string text = _repositoryUrl.Trim();
			if (string.IsNullOrWhiteSpace(remoteName) || string.IsNullOrWhiteSpace(text))
			{
				return (false, ForkPlusDialogStatus.None, string.Empty);
			}
			if (_remoteToEdit?.Name == remoteName && _remoteToEdit?.Url == text)
			{
				return (false, ForkPlusDialogStatus.None, string.Empty);
			}
			string invalid = ReferenceNameValidator.Validate(remoteName);
			if (invalid != null)
			{
				return (false, ForkPlusDialogStatus.Warning, invalid);
			}
			if (_remoteToEdit?.Name != remoteName && _remotes.AnyItem((Remote x) => x.Name == remoteName))
			{
				return (false, ForkPlusDialogStatus.Warning, "Remote '" + remoteName + "' already exists");
			}
			return (true, ForkPlusDialogStatus.None, string.Empty);
		}

		public string CommandPreview
		{
			get
			{
				string newName = _remoteName.Trim();
				string newUrl = _repositoryUrl.Trim();
				if (string.IsNullOrWhiteSpace(newName) || string.IsNullOrWhiteSpace(newUrl))
				{
					return null;
				}
				string Quote(string s) => s.Contains(" ") ? "\"" + s + "\"" : s;
				if (_remoteToEdit == null)
				{
					return "git remote add " + Quote(newName) + " " + Quote(newUrl);
				}
				if (_remoteToEdit.Url != newUrl)
				{
					return "git remote set-url " + Quote(newName) + " " + Quote(newUrl);
				}
				if (_remoteToEdit.Name != newName)
				{
					return "git remote rename " + Quote(_remoteToEdit.Name) + " " + Quote(newName);
				}
				return null;
			}
		}
	}
}
