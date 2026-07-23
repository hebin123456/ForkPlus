using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="CheckoutBranchAsWorktreeWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接 worktree 路径非空校验 + "分支已有 worktree"重名校验 + 命令预览拼接。零 WPF using。
	/// </summary>
	/// <remarks>
	/// <c>RefreshPath</c>（Path.Combine 拼装 + 写 PathTextBox）与 <c>BrowseButton_Click</c> 留 View；
	/// VM 仅持 <see cref="LocalBranch"/> + <see cref="RepositoryWorktrees"/>（WPF 无关 Git 类型）
	/// + <see cref="WorktreePath"/> 输入，据此计算 IsSubmitAllowed / CommandPreview。
	/// </remarks>
	public class CheckoutBranchAsWorktreeWindowViewModel : INotifyPropertyChanged
	{
		private readonly LocalBranch _branch;
		private readonly RepositoryWorktrees _worktrees;

		private string _worktreePath = string.Empty;

		public CheckoutBranchAsWorktreeWindowViewModel(LocalBranch branch, RepositoryWorktrees worktrees)
		{
			_branch = branch;
			_worktrees = worktrees;
		}

		/// <summary>当前 worktree 路径输入。</summary>
		public string WorktreePath
		{
			get => _worktreePath;
			set
			{
				if (_worktreePath != value)
				{
					_worktreePath = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsSubmitAllowed));
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>路径非空白且分支尚无 worktree 时允许提交（纯判断）。</summary>
		public bool IsSubmitAllowed
			=> !string.IsNullOrWhiteSpace(_worktreePath?.Trim())
				&& !_worktrees.WorktreesByFullReference.ContainsKey(_branch.FullReference);

		/// <summary>拼接 <c>git worktree add &lt;path&gt; &lt;branch&gt;</c> 预览。分支/路径缺失时返回 null。</summary>
		public string CommandPreview
		{
			get
			{
				if (_branch == null || string.IsNullOrEmpty(_branch.Name))
				{
					return null;
				}
				string worktreePath = _worktreePath?.Trim() ?? string.Empty;
				if (string.IsNullOrEmpty(worktreePath))
				{
					return null;
				}
				string quotedPath = worktreePath.IndexOf(' ') >= 0 ? ("\"" + worktreePath + "\"") : worktreePath;
				return "git worktree add " + quotedPath + " " + _branch.Name;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
