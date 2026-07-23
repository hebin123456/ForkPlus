using System;
using System.Text;
using ForkPlus.Git;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI
{
	/// <summary>
	/// 阶段 3 里程碑 3.15：MainWindow 的 ViewModel。
	/// 承载纯业务状态字段 + 纯逻辑方法（零 WPF 依赖）。
	/// View 保留 WPF 事件处理（KeyBinding/OnDrop/OnKeyDown）、Manager 字段（含 DispatcherTimer）、模板部件，
	/// 通过本 VM 转发纯逻辑部分。
	/// </summary>
	public class MainWindowViewModel
	{
		private bool _startUpFinished;

		private bool _preventRefreshAfterChildDialogClose;

		private string _preventRefreshAfterChildDialogCloseReason;

		private DateTime _lastActivationStatusRefreshTime = DateTime.MinValue;

		private string _lastActivationStatusRefreshRepositoryPath;

		/// <summary>启动完成标志。Window_Activated 首次触发时置 true 并跳过刷新。</summary>
		public bool StartUpFinished
		{
			get { return _startUpFinished; }
			set { _startUpFinished = value; }
		}

		/// <summary>是否阻止子对话框关闭后的刷新（避免子对话框关闭触发多余刷新）。</summary>
		public bool PreventRefreshAfterChildDialogClose
		{
			get { return _preventRefreshAfterChildDialogClose; }
			set { _preventRefreshAfterChildDialogClose = value; }
		}

		/// <summary>阻止刷新的原因（用于日志）。</summary>
		public string PreventRefreshAfterChildDialogCloseReason
		{
			get { return _preventRefreshAfterChildDialogCloseReason; }
			set { _preventRefreshAfterChildDialogCloseReason = value; }
		}

		/// <summary>标记"因子对话框关闭而跳过下一次激活刷新"，并记录原因。</summary>
		public void PreventRefreshAfterChildDialogCloseWithReason(string reason)
		{
			_preventRefreshAfterChildDialogClose = true;
			_preventRefreshAfterChildDialogCloseReason = reason;
		}

		/// <summary>清除"阻止刷新"标记（消费后调用）。</summary>
		public void ClearPreventRefreshAfterChildDialogClose()
		{
			_preventRefreshAfterChildDialogCloseReason = null;
			_preventRefreshAfterChildDialogClose = false;
		}

		/// <summary>
		/// 激活刷新节流：同仓库 10 秒内不重复刷新。纯时间戳逻辑。
		/// 原方法在 View 第 537-547 行。
		/// </summary>
		public bool ShouldSkipActivationRefresh(string repositoryPath)
		{
			DateTime now = DateTime.UtcNow;
			if (string.Equals(repositoryPath, _lastActivationStatusRefreshRepositoryPath, StringComparison.OrdinalIgnoreCase) && now - _lastActivationStatusRefreshTime < TimeSpan.FromSeconds(10.0))
			{
				return true;
			}
			_lastActivationStatusRefreshRepositoryPath = repositoryPath;
			_lastActivationStatusRefreshTime = now;
			return false;
		}

		/// <summary>
		/// 尝试从剪贴板应用 patch：检测剪贴板文本是否以 "diff " 或 "From " 开头，
		/// 是则编码为 UTF8 字节并返回，供 View 调用 ShowApplyPatchWindowCommand。
		/// 原逻辑在 View OnKeyDown 第 244-250 行。纯逻辑（ServiceLocator.Clipboard 已是抽象）。
		/// </summary>
		/// <returns>若剪贴板含 patch 文本则返回 UTF8 字节；否则返回 null。</returns>
		public static byte[] TryGetClipboardPatchBytes()
		{
			string text = ServiceLocator.Clipboard.GetText();
			if (text != null && (text.StartsWith("diff ") || text.StartsWith("From ")))
			{
				return Encoding.UTF8.GetBytes(text);
			}
			return null;
		}

		/// <summary>
		/// 判断指定路径是否为 patch 文件（.patch 扩展名，忽略大小写）。
		/// 原 RepositoryUserControl.OnDrop 第 128 行的判断逻辑。
		/// </summary>
		public static bool IsPatchFile(string path)
		{
			return path != null && path.EndsWith(Consts.Git.PatchFileExtension, StringComparison.CurrentCultureIgnoreCase);
		}
	}
}
