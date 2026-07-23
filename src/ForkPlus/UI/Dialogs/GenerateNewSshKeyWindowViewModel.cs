using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using ForkPlus.Services;
using ForkPlus.Shell;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="GenerateNewSshKeyWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接 key 名/邮箱校验（含重名警告 + 异常错误）+ ssh-keygen 命令预览。零 WPF using。
	/// </summary>
	/// <remarks>
	/// 新模式点：原 IsSubmitAllowed override 内含多个不同级别的 <c>SetStatus</c> 副作用——
	/// 重名 → Warning，异常 → Error。拆分原则——VM 的 <see cref="Validate"/> 返回
	/// (IsAllowed, Status, StatusMessage) 三元组，View override 据此调 SetStatus。
	/// <see cref="ForkPlusDialogStatus"/> 枚举本身无 WPF 依赖，可安全在 VM 中引用。
	/// </remarks>
	public class GenerateNewSshKeyWindowViewModel : INotifyPropertyChanged
	{
		private readonly SshKey[] _existingSshKeys;

		private string _keyFileName = string.Empty;
		private string _email = string.Empty;

		/// <summary>构造 VM。</summary>
		/// <param name="existingSshKeys">已存在的 SSH key 列表（View 通过 GetLocalSshKeysCommand 获取后传入）。</param>
		public GenerateNewSshKeyWindowViewModel(SshKey[] existingSshKeys)
		{
			_existingSshKeys = existingSshKeys ?? Array.Empty<SshKey>();
		}

		/// <summary>当前 key 文件名输入。</summary>
		public string KeyFileName
		{
			get => _keyFileName;
			set
			{
				if (_keyFileName != value)
				{
					_keyFileName = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>当前邮箱输入。</summary>
		public string Email
		{
			get => _email;
			set
			{
				if (_email != value)
				{
					_email = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>执行校验，返回 (是否允许提交, 状态级别, 状态消息)。
		/// StatusMessage == null 表示无状态消息（View 设 None）；非 null 时按 Status 级别设置。</summary>
		public (bool IsAllowed, ForkPlusDialogStatus Status, string StatusMessage) Validate()
		{
			if (string.IsNullOrEmpty(_keyFileName))
			{
				return (false, ForkPlusDialogStatus.None, null);
			}
			try
			{
				if (_existingSshKeys.AnyItem((SshKey x) => Path.GetFileNameWithoutExtension(x.FilePath) == _keyFileName))
				{
					return (false, ForkPlusDialogStatus.Warning, ServiceLocator.Localization.FormatCurrent("Ssh key '{0}' already exists", _keyFileName));
				}
			}
			catch (Exception ex)
			{
				return (false, ForkPlusDialogStatus.Error, ex.ToString());
			}
			if (string.IsNullOrEmpty(_email))
			{
				return (false, ForkPlusDialogStatus.None, null);
			}
			return (true, ForkPlusDialogStatus.None, null);
		}

		/// <summary>拼接 <c>ssh-keygen -t ed25519 -C &lt;email&gt; -f &lt;path&gt;</c> 预览（替换原 View 的 GetCommandPreview override）。
		/// path 含空格时加引号；拼接 SSH 目录。</summary>
		public string CommandPreview
		{
			get
			{
				string keyName = _keyFileName;
				string email = _email;
				if (string.IsNullOrEmpty(keyName) || string.IsNullOrEmpty(email))
				{
					return null;
				}
				string sshDir = SystemEnvironment.LocalSSHDirectory;
				string path = (sshDir != null) ? Path.Combine(sshDir, keyName) : keyName;
				if (path.IndexOf(' ') >= 0)
				{
					path = "\"" + path + "\"";
				}
				return "ssh-keygen -t ed25519 -C \"" + email + "\" -f " + path;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
