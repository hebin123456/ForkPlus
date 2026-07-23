using System;

namespace ForkPlus.UI.Dialogs
{
	// 阶段 3：承接 5 个文本输入非空校验 + git mm init 命令预览。
	// 默认值填充（dependency.xml/master/default）+ GitMmCommandPreviewHelper.Format 留 VM。
	// SaveDefaults 持久化、剪贴板 URL 探测、目录存在性校验留 View。
	internal sealed class InitGitMmRepositoryWindowViewModel
	{
		private const string DefaultManifest = "dependency.xml";
		private const string DefaultBranch = "master";
		private const string DefaultGroup = "default";

		private string _manifestUrl = string.Empty;
		private string _parentDirectory = string.Empty;
		private string _repositoryName = string.Empty;
		private string _manifestFile = string.Empty;
		private string _manifestBranch = string.Empty;
		private string _manifestGroup = string.Empty;

		public string ManifestUrl
		{
			get => _manifestUrl;
			set => _manifestUrl = value ?? string.Empty;
		}

		public string ParentDirectory
		{
			get => _parentDirectory;
			set => _parentDirectory = value ?? string.Empty;
		}

		public string RepositoryName
		{
			get => _repositoryName;
			set => _repositoryName = value ?? string.Empty;
		}

		public string ManifestFile
		{
			get => _manifestFile;
			set => _manifestFile = value ?? string.Empty;
		}

		public string ManifestBranch
		{
			get => _manifestBranch;
			set => _manifestBranch = value ?? string.Empty;
		}

		public string ManifestGroup
		{
			get => _manifestGroup;
			set => _manifestGroup = value ?? string.Empty;
		}

		public bool IsSubmitAllowed
		{
			get
			{
				if (string.IsNullOrWhiteSpace(_manifestUrl.Trim()))
				{
					return false;
				}
				if (string.IsNullOrWhiteSpace(_parentDirectory.Trim()))
				{
					return false;
				}
				if (string.IsNullOrWhiteSpace(_repositoryName.Trim()))
				{
					return false;
				}
				return true;
			}
		}

		public string[] CreateInitArgs()
		{
			string url = _manifestUrl.Trim();
			string manifest = string.IsNullOrWhiteSpace(_manifestFile) ? DefaultManifest : _manifestFile.Trim();
			string branch = string.IsNullOrWhiteSpace(_manifestBranch) ? DefaultBranch : _manifestBranch.Trim();
			string group = string.IsNullOrWhiteSpace(_manifestGroup) ? DefaultGroup : _manifestGroup.Trim();
			return new string[9] { "init", "-u", url, "-m", manifest, "-b", branch, "-g", group };
		}

		public string CommandPreview => GitMmCommandPreviewHelper.Format(CreateInitArgs());
	}
}
