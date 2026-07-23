using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.Git;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="RenameLocalBranchWindow"/> 的 ViewModel（阶段 3 复杂模式点）。
	/// 承接多重校验（空值/同名/远程重名/名称合法性/本地重名）+ 命令预览。零 WPF using。
	/// </summary>
	/// <remarks>
	/// 新模式点（最复杂）：原 <see cref="ForkPlusDialogWindow.IsSubmitAllowed"/> override 内含
	/// 多个 <c>SetStatus(Warning, ...)</c> 副作用，且存在"持久警告"（checkbox 勾选时即使校验通过
	/// 也保留 "Renaming can break tracking" 警告）。
	/// 拆分原则——VM 的 <see cref="Validate"/> 返回 (IsAllowed, WarningMessage) 元组：
	/// WarningMessage == null → View 设 SetStatus(None, "")；非 null → SetStatus(Warning, msg)。
	/// IsAllowed 与 WarningMessage 独立（allowed=true 时仍可能有持久警告）。
	/// </remarks>
	public class RenameLocalBranchWindowViewModel : INotifyPropertyChanged
	{
		private readonly LocalBranch _localBranch;
		[Null]
		private readonly RemoteBranch _remoteBranch;
		private readonly RepositoryReferences _references;

		private string _newName = string.Empty;
		private bool _renameRemoteBranch;

		public RenameLocalBranchWindowViewModel(LocalBranch localBranch, [Null] RemoteBranch remoteBranch, RepositoryReferences references)
		{
			_localBranch = localBranch;
			_remoteBranch = remoteBranch;
			_references = references;
		}

		/// <summary>新分支名输入（View 把 BranchNameTextBox.Text 推到这里）。</summary>
		public string NewName
		{
			get => _newName;
			set
			{
				if (_newName != value)
				{
					_newName = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>是否同时重命名远程分支（对应 RenameRemoteBranchCheckbox）。</summary>
		public bool RenameRemoteBranch
		{
			get => _renameRemoteBranch;
			set
			{
				if (_renameRemoteBranch != value)
				{
					_renameRemoteBranch = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>执行多重校验，返回 (是否允许提交, 警告消息)。
		/// WarningMessage == null 表示无警告（View 设 None）；非 null 表示 Warning 级别（View 设 Warning）。
		/// IsAllowed=true 时 WarningMessage 仍可能非 null（持久警告）。</summary>
		public (bool IsAllowed, string WarningMessage) Validate()
		{
			string newName = (NewName ?? string.Empty).ToLower();
			if (string.IsNullOrEmpty(newName))
			{
				return (false, null);
			}

			// checkbox 勾选时的持久警告（即使后续校验通过也保留）。
			string persistentWarning = null;
			if (RenameRemoteBranch)
			{
				persistentWarning = ServiceLocator.Localization.Current("Renaming can break tracking references for other users");
				string upstreamRemote = UpstreamRemote(_localBranch);
				RemoteBranch remoteBranch = IReadOnlyListExtensions.FirstItem(_references.RemoteBranches, (RemoteBranch x) => x.ShortName.ToLower() == newName && x.Remote == upstreamRemote);
				if (remoteBranch != null)
				{
					return (false, ServiceLocator.Localization.FormatCurrent("Branch {0} already exists", remoteBranch.Name));
				}
			}

			if (newName == _localBranch.Name.ToLower())
			{
				return (false, persistentWarning);
			}

			// ReferenceNameValidator 返回硬编码英文消息（技术约束描述，无需本地化）。
			string invalid = ReferenceNameValidator.Validate(newName);
			if (invalid != null)
			{
				return (false, invalid);
			}

			if (_references.LocalBranches.AnyItem((LocalBranch x) => x.Name.ToLower() == newName))
			{
				return (false, ServiceLocator.Localization.FormatCurrent("Branch '{0}' already exists", NewName));
			}

			return (true, persistentWarning);
		}

		/// <summary>拼接 <c>git branch -m &lt;old&gt; &lt;new&gt;</c> 预览，可选追加远程重命名命令。</summary>
		public string CommandPreview
		{
			get
			{
				string newName = NewName;
				if (string.IsNullOrWhiteSpace(newName))
				{
					return null;
				}
				string command = "git branch -m " + _localBranch.Name + " " + newName;
				if (RenameRemoteBranch && _remoteBranch != null)
				{
					command += "\ngit push " + _remoteBranch.Remote + " " + newName + " :" + _remoteBranch.ShortName;
				}
				return command;
			}
		}

		/// <summary>从 LocalBranch 的 UpstreamFullName 提取 remote 名（替换原 View 的 UpstreamRemote 静态方法）。</summary>
		public static string UpstreamRemote(LocalBranch localBranch)
		{
			string upstreamFullName = localBranch.UpstreamFullName;
			if (upstreamFullName == null)
			{
				return null;
			}
			int num = upstreamFullName.IndexOf("/");
			if (num == -1)
			{
				return null;
			}
			return upstreamFullName.Substring(0, num);
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
