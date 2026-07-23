using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="GitFlowInitWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接 6 个分支名/前缀的"非空 + ReferenceNameValidator"校验 + 命令预览。零 WPF using。
	/// </summary>
	/// <remarks>
	/// 新模式点（第 9 个）：原 IsSubmitAllowed 对"非空"消息调 <c>PreferencesLocalization.Translate</c>
	/// （WPF 类型，VM 不可用），对校验器输出则原样透传。为精确保留该差异，
	/// Validate() 返回四元组 (IsAllowed, Status, StatusMessage, RequiresTranslation)：
	/// 非空失败 → StatusMessage 为翻译键、RequiresTranslation=true；校验失败 → StatusMessage 为校验器原文、
	/// RequiresTranslation=false。View override 据 RequiresTranslation 决定是否 Translate。
	/// </remarks>
	public class GitFlowInitWindowViewModel : INotifyPropertyChanged
	{
		private string _masterBranch = string.Empty;
		private string _developBranch = string.Empty;
		private string _featurePrefix = string.Empty;
		private string _releasePrefix = string.Empty;
		private string _hotfixPrefix = string.Empty;
		private string _versionTagPrefix = string.Empty;

		public string MasterBranch { get => _masterBranch; set => Set(ref _masterBranch, value); }
		public string DevelopBranch { get => _developBranch; set => Set(ref _developBranch, value); }
		public string FeaturePrefix { get => _featurePrefix; set => Set(ref _featurePrefix, value); }
		public string ReleasePrefix { get => _releasePrefix; set => Set(ref _releasePrefix, value); }
		public string HotfixPrefix { get => _hotfixPrefix; set => Set(ref _hotfixPrefix, value); }
		public string VersionTagPrefix { get => _versionTagPrefix; set => Set(ref _versionTagPrefix, value); }

		/// <summary>执行校验，返回 (是否允许提交, 状态级别, 状态消息, 是否需 View 翻译)。
		/// 非空失败 → Warning + 翻译键 + RequiresTranslation=true；
		/// 校验失败 → Warning + 校验器原文 + RequiresTranslation=false；通过 → None。</summary>
		public (bool IsAllowed, ForkPlusDialogStatus Status, string StatusMessage, bool RequiresTranslation) Validate()
		{
			if (string.IsNullOrEmpty(_masterBranch))
			{
				return (false, ForkPlusDialogStatus.Warning, "Production branch name can't be empty", true);
			}
			if (string.IsNullOrEmpty(_developBranch))
			{
				return (false, ForkPlusDialogStatus.Warning, "Development branch name can't be empty", true);
			}
			if (string.IsNullOrEmpty(_featurePrefix))
			{
				return (false, ForkPlusDialogStatus.Warning, "Feature branch prefix can't be empty", true);
			}
			if (string.IsNullOrEmpty(_releasePrefix))
			{
				return (false, ForkPlusDialogStatus.Warning, "Release branch prefix can't be empty", true);
			}
			if (string.IsNullOrEmpty(_hotfixPrefix))
			{
				return (false, ForkPlusDialogStatus.Warning, "Hotfix branch prefix can't be empty", true);
			}
			string invalidMaster = ReferenceNameValidator.Validate(_masterBranch);
			if (invalidMaster != null)
			{
				return (false, ForkPlusDialogStatus.Warning, invalidMaster, false);
			}
			string invalidDevelop = ReferenceNameValidator.Validate(_developBranch);
			if (invalidDevelop != null)
			{
				return (false, ForkPlusDialogStatus.Warning, invalidDevelop, false);
			}
			string invalidFeature = ReferenceNameValidator.ValidateGitFlow(_featurePrefix);
			if (invalidFeature != null)
			{
				return (false, ForkPlusDialogStatus.Warning, invalidFeature, false);
			}
			string invalidRelease = ReferenceNameValidator.ValidateGitFlow(_releasePrefix);
			if (invalidRelease != null)
			{
				return (false, ForkPlusDialogStatus.Warning, invalidRelease, false);
			}
			string invalidHotfix = ReferenceNameValidator.ValidateGitFlow(_hotfixPrefix);
			if (invalidHotfix != null)
			{
				return (false, ForkPlusDialogStatus.Warning, invalidHotfix, false);
			}
			string invalidVersionTag = ReferenceNameValidator.ValidateGitFlow(_versionTagPrefix);
			if (invalidVersionTag != null)
			{
				return (false, ForkPlusDialogStatus.Warning, invalidVersionTag, false);
			}
			return (true, ForkPlusDialogStatus.None, null, false);
		}

		/// <summary>master/develop 均非空时返回 "git flow init"，否则 null。</summary>
		public string CommandPreview
			=> (string.IsNullOrEmpty(_masterBranch) || string.IsNullOrEmpty(_developBranch))
				? null : "git flow init";

		public event PropertyChangedEventHandler PropertyChanged;

		private void Set(ref string field, string value, [CallerMemberName] string name = null)
		{
			if (field != value)
			{
				field = value;
				OnPropertyChanged(name);
			}
		}

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
