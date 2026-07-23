using System;
using System.IO;
using ForkPlus.Git.Commands;

namespace ForkPlus.UI.Dialogs
{
	// 阶段 3：承接 git 可执行路径合法性校验（IsSubmitAllowed）。
	// 校验：路径非空 + File.Exists + 文件名 == git.exe + GetGitVersionGitCommand 执行成功。
	// BrowseButton/OpenFileDialog/GetGitCandidates/ListBox 选择同步留 View。
	// showError=true 时的 ErrorWindow 弹窗留 View（VM 只暴露静默校验）。
	internal sealed class ConfigureGitInstanceWindowViewModel
	{
		private string _gitPath = string.Empty;

		public string GitPath
		{
			get => _gitPath;
			set => _gitPath = value ?? string.Empty;
		}

		/// <summary>静默校验 git 路径合法性（不弹窗）。</summary>
		public bool IsSubmitAllowed => IsGitPathValid(_gitPath.Trim());

		/// <summary>暴露给 View 用于 OnSubmit 时显式校验（showError=true 时由 View 弹窗）。</summary>
		public bool IsGitPathValid(string gitPath)
		{
			if (string.IsNullOrWhiteSpace(gitPath) || !File.Exists(gitPath))
			{
				return false;
			}
			if (!string.Equals(Path.GetFileName(gitPath), "git.exe", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			return new GetGitVersionGitCommand().Execute(gitPath).Succeeded;
		}
	}
}
