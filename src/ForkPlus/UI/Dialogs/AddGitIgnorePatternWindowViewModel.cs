using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="AddGitIgnorePatternWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接 pattern 非空校验 + .gitignore 写入命令预览。零 WPF using。
	/// </summary>
	/// <remarks>
	/// 复用 GitLfsTrack 模式：IsSubmitAllowed 表达式体无副作用，GetCommandPreview 返回固定预览。
	/// View 的 <c>UpdatePreview</c>（Task/TaskScheduler 异步预览）留 View。
	/// </remarks>
	public class AddGitIgnorePatternWindowViewModel : INotifyPropertyChanged
	{
		private string _pattern = string.Empty;

		/// <summary>当前 pattern 输入（多行，每行一个 pattern）。</summary>
		public string Pattern
		{
			get => _pattern;
			set
			{
				if (_pattern != value)
				{
					_pattern = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsSubmitAllowed));
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>pattern 非空白时允许提交（替换原 View 的 IsSubmitAllowed 表达式体）。</summary>
		public bool IsSubmitAllowed => !string.IsNullOrWhiteSpace(_pattern);

		/// <summary>.gitignore 写入预览（与 IgnoreFilesGitCommand 对应，替换原 View 的 GetCommandPreview override）。</summary>
		public string CommandPreview
		{
			get
			{
				if (string.IsNullOrWhiteSpace(_pattern))
				{
					return null;
				}
				return "# .gitignore\ngit rm --cached -r .";
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
