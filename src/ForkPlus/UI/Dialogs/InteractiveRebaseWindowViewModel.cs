using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	// 阶段 3：承接 InteractiveRebase 的提交允许判定 + 命令预览。
	// IsSubmitAllowed 依赖 _todoList.Count 与 _closing，由 View 推入。
	// CommandPreview 依赖 _destination（IGitPoint），对应 git rebase -i <destination>。
	// IPC/ObservableCollection/Adorner/Semaphore 等重度 WPF 逻辑全留 View。
	internal sealed class InteractiveRebaseWindowViewModel
	{
		private readonly IGitPoint _destination;
		private int _todoListCount;
		private bool _closing;

		public InteractiveRebaseWindowViewModel(IGitPoint destination)
		{
			_destination = destination;
		}

		public int TodoListCount
		{
			set => _todoListCount = value;
		}

		public bool Closing
		{
			set => _closing = value;
		}

		public bool IsSubmitAllowed => _todoListCount > 0 && !_closing;

		public string CommandPreview => _destination == null ? null : "git rebase -i " + _destination.FriendlyName;
	}
}
