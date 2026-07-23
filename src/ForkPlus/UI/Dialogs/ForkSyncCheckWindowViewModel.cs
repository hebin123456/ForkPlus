using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.Git.Commands;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="ForkSyncCheckWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接同步状态持有 + "检测中不允许提交"守卫。零 WPF using。
	/// </summary>
	/// <remarks>
	/// 极薄 VM：原 IsSubmitAllowed 仅 <c>_status.HasValue &amp;&amp; base.IsSubmitAllowed</c>。
	/// VM 持 <see cref="Status"/>（<see cref="ForkSyncStatus"/>?），View override 合并 VM.HasValue 与 base.IsSubmitAllowed。
	/// ConfigureForStatus 的 BitmapImage/TextBlock 等 UI 操作留 View。
	/// </remarks>
	public class ForkSyncCheckWindowViewModel : INotifyPropertyChanged
	{
		private ForkSyncStatus? _status;

		public ForkSyncCheckWindowViewModel(ForkSyncStatus? status)
		{
			_status = status;
		}

		/// <summary>当前同步检测结果（null 表示检测中）。</summary>
		public ForkSyncStatus? Status
		{
			get => _status;
			set
			{
				if (_status != value)
				{
					_status = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsSubmitAllowed));
				}
			}
		}

		/// <summary>检测完成（Status 有值）时允许提交。base.IsSubmitAllowed 部分由 View override 合并。</summary>
		public bool IsSubmitAllowed => _status.HasValue;

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
