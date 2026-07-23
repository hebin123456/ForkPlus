using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs.Accounts
{
	/// <summary>
	/// <see cref="AddAccountWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接"已选服务"校验。零 WPF using。
	/// </summary>
	/// <remarks>
	/// 新模式点：原 View 的嵌套 <c>ServiceViewModel</c> 持有 <c>ImageSource Icon</c>（WPF 类型），
	/// 不能进 VM。拆分原则——<c>ServiceViewModel</c>（含 Icon）留在 View 作列表项展示，
	/// VM 只跟踪 <see cref="SelectedServiceType"/>（<see cref="RemoteType"/> 枚举，WPF 无关）。
	/// IsSubmitAllowed override 内含 <c>SetStatus(None,"")</c> 副作用，副作用留 View。
	/// 无 GetCommandPreview。
	/// </remarks>
	public class AddAccountWindowViewModel : INotifyPropertyChanged
	{
		private RemoteType? _selectedServiceType;

		/// <summary>当前选中的服务类型（null 表示未选）。</summary>
		public RemoteType? SelectedServiceType
		{
			get => _selectedServiceType;
			set
			{
				if (_selectedServiceType != value)
				{
					_selectedServiceType = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsSubmitAllowed));
				}
			}
		}

		/// <summary>已选服务时允许提交（纯判断，副作用留 View override）。</summary>
		public bool IsSubmitAllowed => _selectedServiceType.HasValue;

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
