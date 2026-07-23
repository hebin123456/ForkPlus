using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="ApplyPatchWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接"文件路径存在"校验 + 命令预览拼接（git am / git apply）。零 WPF using，零 Git 依赖（纯 BCL）。
	/// </summary>
	/// <remarks>
	/// _patchData 非空（剪贴板补丁）时恒允许提交；否则要求文件路径存在。
	/// CreateCommits 由 View 据 Checkbox 可见性+勾选状态计算后传入（纯 bool）。
	/// TestForConflicts（git 预检 + SetStatus）/ RefreshCreateCommitsCheckBoxVisibility / PatchContainsCommitHeader 留 View。
	/// </remarks>
	public class ApplyPatchWindowViewModel : INotifyPropertyChanged
	{
		private readonly byte[] _patchData;

		private string _patchPath = string.Empty;
		private bool _createCommits;

		public ApplyPatchWindowViewModel(byte[] patchData)
		{
			_patchData = patchData;
		}

		/// <summary>当前补丁文件路径输入。</summary>
		public string PatchPath
		{
			get => _patchPath;
			set
			{
				if (_patchPath != value)
				{
					_patchPath = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsSubmitAllowed));
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>是否用 git am（创建提交）；false 则 git apply。</summary>
		public bool CreateCommits
		{
			get => _createCommits;
			set
			{
				if (_createCommits != value)
				{
					_createCommits = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>剪贴板补丁恒允许；文件补丁要求路径存在（纯判断）。</summary>
		public bool IsSubmitAllowed
			=> _patchData != null || File.Exists((_patchPath ?? string.Empty).Trim());

		/// <summary>拼接 <c>git am</c> / <c>git apply [path]</c> 预览。剪贴板补丁仅返回命令名。</summary>
		public string CommandPreview
		{
			get
			{
				string command = _createCommits ? "git am" : "git apply";
				if (_patchData != null)
				{
					return command;
				}
				string filePath = (_patchPath ?? string.Empty).Trim();
				if (string.IsNullOrEmpty(filePath))
				{
					return null;
				}
				string quotedPath = filePath.IndexOf(' ') >= 0 ? ("\"" + filePath + "\"") : filePath;
				return command + " " + quotedPath;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
