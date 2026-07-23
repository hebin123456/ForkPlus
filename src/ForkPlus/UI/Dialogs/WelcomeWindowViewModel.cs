using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="WelcomeWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接默认克隆目录校验（非空 + 非 c:\ + 目录存在）。零 WPF using。
	/// </summary>
	/// <remarks>
	/// IsSubmitAllowed 无 SetStatus 副作用（原 View 也无），纯校验逻辑直接进 VM。
	/// 无 GetCommandPreview。Log.Error 保留（Log 在 root ForkPlus 命名空间，WPF 无关）。
	/// </remarks>
	public class WelcomeWindowViewModel : INotifyPropertyChanged
	{
		private string _defaultCloneDirectory = string.Empty;

		/// <summary>默认克隆目录输入（View 把 TextBox.Text 推到这里）。</summary>
		public string DefaultCloneDirectory
		{
			get => _defaultCloneDirectory;
			set
			{
				if (_defaultCloneDirectory != value)
				{
					_defaultCloneDirectory = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsSubmitAllowed));
				}
			}
		}

		/// <summary>目录非空、非 c:\、且存在时允许提交（替换原 View 的 IsSubmitAllowed override）。</summary>
		public bool IsSubmitAllowed
		{
			get
			{
				string text = (_defaultCloneDirectory ?? string.Empty).Trim();
				if (text == "" || text.Equals("c:\\", StringComparison.OrdinalIgnoreCase))
				{
					return false;
				}
				try
				{
					if (!Directory.Exists(text))
					{
						return false;
					}
				}
				catch (Exception ex)
				{
					Log.Error("Failed to check '" + text + "' existence", ex);
					return false;
				}
				return true;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
