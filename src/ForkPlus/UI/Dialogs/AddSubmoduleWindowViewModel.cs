using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="AddSubmoduleWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接 url+path 非空校验 + 路径合法性校验（gitModule.MakePath）+ 命令预览拼接。零 WPF using。
	/// </summary>
	/// <remarks>
	/// IsPathValid 由 VM 在 Path 设置时通过 try MakePath 计算（原 View 在 TextChanged 里做）。
	/// NormalizedPathHint 为 MakePath 原始结果，View 用 PathHelper.Normalize 渲染 FinalPathHintTextBlock。
	/// 剪贴板 URL 探测（ServiceLocator.Clipboard）/ GitUrl 解析留 View（ctor 一次性）。
	/// </remarks>
	public class AddSubmoduleWindowViewModel : INotifyPropertyChanged
	{
		private readonly GitModule _gitModule;

		private string _repositoryUrl = string.Empty;
		private string _path = string.Empty;
		private bool _isPathValid = true;
		private string _normalizedPathHint;

		public AddSubmoduleWindowViewModel(GitModule gitModule)
		{
			_gitModule = gitModule;
		}

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

		/// <summary>子模块路径输入。设置时重算 IsPathValid / NormalizedPathHint（try MakePath）。</summary>
		public string Path
		{
			get => _path;
			set
			{
				if (_path != value)
				{
					_path = value;
					RecomputePathValidity();
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsPathValid));
					OnPropertyChanged(nameof(NormalizedPathHint));
					OnPropertyChanged(nameof(IsSubmitAllowed));
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>路径是否合法（MakePath 未抛异常）。空路径视为合法。</summary>
		public bool IsPathValid => _isPathValid;

		/// <summary>MakePath 的原始结果（供 View 做 PathHelper.Normalize 显示）。非法时为 null。</summary>
		public string NormalizedPathHint => _normalizedPathHint;

		/// <summary>url、path 均非空白且路径合法时允许提交（纯判断）。</summary>
		public bool IsSubmitAllowed
			=> !string.IsNullOrWhiteSpace(_repositoryUrl)
				&& !string.IsNullOrWhiteSpace(_path)
				&& _isPathValid;

		/// <summary>拼接 <c>git submodule add [url] &lt;path&gt;</c> 预览。path 为空时返回 null。</summary>
		public string CommandPreview
		{
			get
			{
				string url = (_repositoryUrl ?? string.Empty).Trim();
				string path = (_path ?? string.Empty).Trim();
				if (string.IsNullOrWhiteSpace(path))
				{
					return null;
				}
				string Quote(string s) => s.Contains(" ") ? "\"" + s + "\"" : s;
				string normalizedPath = PathHelper.NormalizeUnix(path);
				if (string.IsNullOrWhiteSpace(url))
				{
					return "git submodule add " + Quote(normalizedPath);
				}
				return "git submodule add " + Quote(url) + " " + Quote(normalizedPath);
			}
		}

		private void RecomputePathValidity()
		{
			if (string.IsNullOrEmpty(_path))
			{
				_isPathValid = true;
				_normalizedPathHint = null;
				return;
			}
			try
			{
				_normalizedPathHint = _gitModule.MakePath(_path);
				_isPathValid = true;
			}
			catch
			{
				_isPathValid = false;
				_normalizedPathHint = null;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
