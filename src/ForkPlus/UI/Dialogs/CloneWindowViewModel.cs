using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using ForkPlus.Git;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="CloneWindow"/> 的 ViewModel（阶段 3 首个 VM 抽取）。
	/// 承接原 View 中的纯业务逻辑：表单输入状态、命令预览拼接、仓库名/协议推导、剪贴板 URL 解析。
	/// 不依赖任何 WPF 类型（无 <c>System.Windows.*</c> using），可被 Avalonia 版 View 复用。
	/// </summary>
	/// <remarks>
	/// 当前范围：View 仍负责把 TextBox.Text 推送到本 VM 的属性（后续可改为双向绑定）。
	/// OnSubmit / TestButton / AccountItem.Icon 等 WPF 强耦合逻辑暂留 View，留待后续迭代。
	/// </remarks>
	public class CloneWindowViewModel : INotifyPropertyChanged
	{
		private string _repositoryUrl = string.Empty;
		private string _repositoryName = string.Empty;
		private string _parentDirectory = string.Empty;

		/// <summary>远程仓库 URL（已 trim）。</summary>
		public string RepositoryUrl
		{
			get => _repositoryUrl;
			set
			{
				if (_repositoryUrl != value)
				{
					_repositoryUrl = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsSubmitAllowed));
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>本地仓库名（已 trim）。</summary>
		public string RepositoryName
		{
			get => _repositoryName;
			set
			{
				if (_repositoryName != value)
				{
					_repositoryName = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsSubmitAllowed));
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>目标父目录（已 trim）。</summary>
		public string ParentDirectory
		{
			get => _parentDirectory;
			set
			{
				if (_parentDirectory != value)
				{
					_parentDirectory = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsSubmitAllowed));
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>三项输入都非空时允许提交（替换原 View 的 <see cref="ForkPlusDialogWindow.IsSubmitAllowed"/> override）。</summary>
		public bool IsSubmitAllowed =>
			!string.IsNullOrWhiteSpace(RepositoryUrl) &&
			!string.IsNullOrWhiteSpace(RepositoryName) &&
			!string.IsNullOrWhiteSpace(ParentDirectory);

		/// <summary>拼接 <c>git clone</c> 命令预览（替换原 View 的 <see cref="ForkPlusDialogWindow.GetCommandPreview"/> override）。</summary>
		public string CommandPreview
		{
			get
			{
				string url = (RepositoryUrl ?? string.Empty).Trim();
				if (string.IsNullOrWhiteSpace(url))
				{
					return null;
				}
				List<string> parts = new List<string> { "git", "clone" };
				if (ForkPlusSettings.Default.UpdateSubmodulesOnCheckout)
				{
					parts.Add("--recurse-submodules");
				}
				parts.Add(Quote(url));
				string parentDir = (ParentDirectory ?? string.Empty).Trim();
				string repoName = (RepositoryName ?? string.Empty).Trim();
				if (!string.IsNullOrWhiteSpace(parentDir) && !string.IsNullOrWhiteSpace(repoName))
				{
					parts.Add(Quote(Path.Combine(parentDir, repoName)));
				}
				return string.Join(" ", parts);
			}
		}

		/// <summary>从 URL 推导仓库名（替换原 <c>RefreshRepositoryNameTextBox</c> 核心逻辑）。无名称时返回 null。</summary>
		public static string DeriveRepositoryName(string url)
		{
			return new GitUrl((url ?? string.Empty).Trim()).RepositoryName;
		}

		/// <summary>获取 URL 的网络协议（替换原 <c>RefreshNetworkProtocolButton</c> 核心逻辑）。
		/// 返回 null 表示无效 URL（View 据此隐藏协议下拉按钮）。</summary>
		public static GitUrl.NetworkProtocol? GetNetworkProtocol(string url)
		{
			GitUrl gitUrl = new GitUrl((url ?? string.Empty).Trim());
			if (!gitUrl.IsValid)
			{
				return null;
			}
			return gitUrl.Protocol;
		}

		/// <summary>尝试从剪贴板解析 git clone URL（替换原 <c>TryParseUrlFromClipboard</c>）。
		/// 剪贴板无有效 URL 时返回 null。</summary>
		public static string TryGetUrlFromClipboard()
		{
			string text = ServiceLocator.Clipboard.GetText();
			if (text == null)
			{
				return null;
			}
			text = RemoveGitClonePrefix(text).Trim().Trim('"');
			if (new GitUrl(text).IsValid)
			{
				return text;
			}
			return null;
		}

		/// <summary>去掉 <c>git clone </c> 前缀（替换原 <c>RemoveGitClonePrefix</c>）。</summary>
		public static string RemoveGitClonePrefix(string clipboardUrl)
		{
			const string prefix = "git clone ";
			if (clipboardUrl == null || !clipboardUrl.StartsWith(prefix))
			{
				return clipboardUrl ?? string.Empty;
			}
			return clipboardUrl.Remove(0, prefix.Length);
		}

		private static string Quote(string s) => s.Contains(" ") ? "\"" + s + "\"" : s;

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
