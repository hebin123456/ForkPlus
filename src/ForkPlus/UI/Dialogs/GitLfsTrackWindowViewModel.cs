using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="GitLfsTrackWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接 pattern 非空校验 + <c>git lfs track</c> 命令预览。零 WPF using。
	/// </summary>
	/// <remarks>
	/// 新模式点：IsSubmitAllowed 为表达式体（无 SetStatus 副作用），GetCommandPreview 按 pattern
	/// 多行 split 后拼接。View 的 <c>UpdatePreview</c>（Task/TaskScheduler 异步预览）留 View，
	/// 因其直接操作 <c>PreviewTextBox</c>/<c>PreviewLabelTextBlock</c> 等 WPF 控件。
	/// </remarks>
	public class GitLfsTrackWindowViewModel : INotifyPropertyChanged
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

		/// <summary>拼接 <c>git lfs track &lt;pattern&gt;...</c> 预览（每行一个 pattern，替换原 View 的 GetCommandPreview override）。</summary>
		public string CommandPreview
		{
			get
			{
				string text = _pattern;
				if (string.IsNullOrWhiteSpace(text))
				{
					return null;
				}
				string[] patterns = text.Trim().Split(new string[1] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
				return "git lfs track " + string.Join(" ", patterns);
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
