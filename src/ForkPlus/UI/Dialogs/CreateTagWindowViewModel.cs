using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="CreateTagWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接 tag 名校验（ReferenceNameValidator + 重名）+ 命令预览拼接（含 push 多远端）。零 WPF using。
	/// </summary>
	/// <remarks>
	/// 复用 Validate() 三元组模式：无效名/重名 → Warning。"Tag '...' already exists" 用原始大小写（非 lower）。
	/// RefreshButtonTitle / autocomplete / PushCheckBox 持久化（ForkPlusSettings）留 View。
	/// </remarks>
	public class CreateTagWindowViewModel : INotifyPropertyChanged
	{
		private readonly Tag[] _tags;
		private readonly Remote[] _remotes;
		private readonly IGitPoint _gitPoint;

		private string _tagName = string.Empty;
		private string _tagMessage = string.Empty;
		private bool _push;

		public CreateTagWindowViewModel(Tag[] tags, Remote[] remotes, IGitPoint gitPoint)
		{
			_tags = tags ?? System.Array.Empty<Tag>();
			_remotes = remotes;
			_gitPoint = gitPoint;
		}

		/// <summary>当前 tag 名输入（保留原始大小写，用于重名提示消息）。</summary>
		public string TagName
		{
			get => _tagName;
			set
			{
				if (_tagName != value)
				{
					_tagName = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		public string TagMessage
		{
			get => _tagMessage;
			set
			{
				if (_tagMessage != value)
				{
					_tagMessage = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		public bool Push
		{
			get => _push;
			set
			{
				if (_push != value)
				{
					_push = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>执行校验，返回 (是否允许提交, 状态级别, 状态消息)。空 → None，无效名/重名 → Warning。</summary>
		public (bool IsAllowed, ForkPlusDialogStatus Status, string StatusMessage) Validate()
		{
			string tagNameLower = (_tagName ?? string.Empty).ToLower();
			if (string.IsNullOrEmpty(tagNameLower))
			{
				return (false, ForkPlusDialogStatus.None, null);
			}
			string invalid = ReferenceNameValidator.Validate(tagNameLower);
			if (invalid != null)
			{
				return (false, ForkPlusDialogStatus.Warning, invalid);
			}
			if (_tags.AnyItem((Tag x) => x.Name.ToLower() == tagNameLower))
			{
				return (false, ForkPlusDialogStatus.Warning, "Tag '" + _tagName + "' already exists");
			}
			return (true, ForkPlusDialogStatus.None, null);
		}

		/// <summary>拼接 <c>git tag -a [-m msg] &lt;name&gt; [commit]</c> 预览，Push 时追加各远端 push 命令。</summary>
		public string CommandPreview
		{
			get
			{
				string tagName = _tagName ?? string.Empty;
				if (string.IsNullOrWhiteSpace(tagName))
				{
					return null;
				}
				var parts = new List<string> { "git", "tag", "-a" };
				if (!string.IsNullOrEmpty(_tagMessage))
				{
					parts.Add("-m");
					parts.Add(_tagMessage.Contains(" ") ? "\"" + _tagMessage + "\"" : _tagMessage);
				}
				parts.Add(tagName);
				string commit = _gitPoint?.FriendlyName;
				if (!string.IsNullOrEmpty(commit))
				{
					parts.Add(commit);
				}
				string command = string.Join(" ", parts);
				if (_push && _remotes != null)
				{
					foreach (Remote remote in _remotes)
					{
						command += "\ngit push " + remote.Name + " refs/tags/" + tagName;
					}
				}
				return command;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
