using System;

namespace ForkPlus.AutoUpdater
{
	/// <summary>
	/// AutoUpdater 与主程序之间的进度消息协议（命名管道字符串消息）。
	/// 主程序侧（ForkPlus.exe 的 AutoUpdateRunner）与本文件保持同一格式，
	/// 双方各持一份常量副本——ForkPlus.AutoUpdaterTests 与 ForkPlus.Tests 各有
	/// 协议一致性用例锁定字段名/格式不漂移。
	///
	/// 消息格式（单行字符串，冒号分段）：
	///   download:{received}:{total}   下载进度（字节，int64 十进制）
	///   phase:{name}                  阶段推进：downloading / extracting /
	///                                 waiting-exit / replacing / restarting / done
	///   error:{message}               失败（message 内换行已清洗为空格）
	/// 主程序侧取消 = 直接 Kill updater 进程（仅允许 download 阶段，UI 侧守卫），
	/// 不走协议回传——被杀进程的管道写入失败即自然退出，无需优雅取消握手。
	/// </summary>
	internal static class UpdateMessageProtocol
	{
		/// <summary>下载进度消息前缀（"download:{received}:{total}"）。</summary>
		public const string DownloadPrefix = "download:";

		/// <summary>阶段消息前缀（"phase:{name}"）。</summary>
		public const string PhasePrefix = "phase:";

		/// <summary>失败消息前缀（"error:{message}"）。</summary>
		public const string ErrorPrefix = "error:";

		/// <summary>阶段：下载中（进度由 download 消息承载）。</summary>
		public const string PhaseDownloading = "downloading";

		/// <summary>阶段：下载完成，正在解压。</summary>
		public const string PhaseExtracting = "extracting";

		/// <summary>阶段：解压完成，等待主程序退出（主程序收到后自行关闭）。</summary>
		public const string PhaseWaitingExit = "waiting-exit";

		/// <summary>阶段：主程序已退出，正在替换安装目录。</summary>
		public const string PhaseReplacing = "replacing";

		/// <summary>阶段：替换完成，正在重启主程序。</summary>
		public const string PhaseRestarting = "restarting";

		/// <summary>阶段：全部完成（updater 即将退出）。</summary>
		public const string PhaseDone = "done";

		/// <summary>构造下载进度消息。</summary>
		public static string Download(long received, long total)
		{
			return DownloadPrefix + received + ":" + total;
		}

		/// <summary>构造阶段消息。</summary>
		public static string Phase(string phaseName)
		{
			return PhasePrefix + phaseName;
		}

		/// <summary>构造失败消息（换行/回车清洗为空格，保证单行；\r\n 折叠为单个空格）。</summary>
		public static string Error(string message)
		{
			return ErrorPrefix + (message ?? "")
				.Replace("\r\n", " ")
				.Replace("\r", " ")
				.Replace("\n", " ");
		}

		/// <summary>
		/// 解析下载进度消息。输入非 download 前缀或数字段格式非法时返回 false
		///（out 参数保持 0——对齐 BCL TryParse 失败即默认值的约定）。供主程序侧
		/// 与测试共用解析语义。
		/// </summary>
		public static bool TryParseDownload(string message, out long received, out long total)
		{
			received = 0L;
			total = 0L;
			if (message == null || !message.StartsWith(DownloadPrefix, StringComparison.Ordinal))
			{
				return false;
			}
			string[] parts = message.Substring(DownloadPrefix.Length).Split(':');
			if (parts.Length != 2)
			{
				return false;
			}
			if (long.TryParse(parts[0], out long parsedReceived) && long.TryParse(parts[1], out long parsedTotal))
			{
				received = parsedReceived;
				total = parsedTotal;
				return true;
			}
			return false;
		}

		/// <summary>解析阶段消息名。输入非 phase 前缀时返回 null。</summary>
		public static string TryParsePhase(string message)
		{
			if (message == null || !message.StartsWith(PhasePrefix, StringComparison.Ordinal))
			{
				return null;
			}
			return message.Substring(PhasePrefix.Length);
		}

		/// <summary>解析失败消息内容。输入非 error 前缀时返回 null。</summary>
		public static string TryParseError(string message)
		{
			if (message == null || !message.StartsWith(ErrorPrefix, StringComparison.Ordinal))
			{
				return null;
			}
			return message.Substring(ErrorPrefix.Length);
		}
	}
}
