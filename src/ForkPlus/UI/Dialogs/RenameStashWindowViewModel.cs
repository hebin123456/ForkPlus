using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="RenameStashWindow"/> 的 ViewModel（阶段 3 横向复用 CloneWindow 模式）。
	/// 承接原 View 中的纯业务逻辑：stash 名输入校验 + git stash rename 命令预览拼接。
	/// 不依赖任何 WPF 类型（无 <c>System.Windows.*</c> using）。
	/// </summary>
	/// <remarks>
	/// 新验证的模式点（CloneWindow 未涉及）：<see cref="ForkPlusDialogWindow.IsSubmitAllowed"/>
	/// override 内含副作用 <c>SetStatus(...)</c>。拆分原则——副作用留 View 的 override，
	/// 纯判断委托 VM；故 VM 的 <see cref="IsSubmitAllowed"/> 无副作用，可重复求值。
	/// </remarks>
	public class RenameStashWindowViewModel : INotifyPropertyChanged
	{
		private readonly string _originalMessage;
		private readonly string _reflogName;

		private string _stashName = string.Empty;

		/// <summary>构造 VM。</summary>
		/// <param name="originalMessage">stash 原始消息（用于判断是否有变更）。</param>
		/// <param name="reflogName">stash 的 reflog 名（用于命令预览拼接，可为 null/空）。</param>
		public RenameStashWindowViewModel(string originalMessage, string reflogName)
		{
			_originalMessage = originalMessage ?? string.Empty;
			_reflogName = reflogName;
		}

		/// <summary>当前 stash 名输入（已 trim 后参与校验/预览）。</summary>
		public string StashName
		{
			get => _stashName;
			set
			{
				if (_stashName != value)
				{
					_stashName = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsSubmitAllowed));
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>允许提交：非空且与原消息不同（替换原 View 的 <see cref="ForkPlusDialogWindow.IsSubmitAllowed"/> override 纯判断部分）。</summary>
		/// <remarks>
		/// 原 override 内含 <c>SetStatus(ForkPlusDialogStatus.None, string.Empty)</c> 副作用，
		/// 该副作用留在 View 的 override（调用本属性前执行），本属性保持纯函数无副作用。
		/// </remarks>
		public bool IsSubmitAllowed
		{
			get
			{
				string text = StashName;
				if (string.IsNullOrWhiteSpace(text))
				{
					return false;
				}
				return text != _originalMessage;
			}
		}

		/// <summary>拼接 <c>git stash rename &lt;reflog&gt; &lt;message&gt;</c> 预览（替换原 View 的 <see cref="ForkPlusDialogWindow.GetCommandPreview"/> override）。</summary>
		public string CommandPreview
		{
			get
			{
				if (string.IsNullOrEmpty(_reflogName))
				{
					return null;
				}
				string newMessage = StashName;
				if (string.IsNullOrWhiteSpace(newMessage))
				{
					return null;
				}
				string quoted = newMessage.IndexOf(' ') >= 0 ? ("\"" + newMessage + "\"") : newMessage;
				return "git stash rename " + _reflogName + " " + quoted;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
