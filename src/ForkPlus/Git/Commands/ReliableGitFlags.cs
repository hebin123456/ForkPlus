namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// v3.12.1（问题6"Windows 双击 stage 失效，点 TortoiseGit 后恢复"）：git 命令可靠化前缀（fsmonitor类四件套）。
	/// 根因：repo 开启 core.fsmonitor 且 daemon 运行时（Git for Windows 常见配置），
	/// <c>git add</c> 走 read_cache_preload 先询问 daemon；daemon 漏报工作区变更
	/// （事件合并/守护进程重启窗口期/index stat 缓存陈旧）时 add 视文件"未变"——
	/// <b>暂存静默空操作</b>，文件留在未暂存列表（"双击 stage 失效"）。
	/// 用户点 TortoiseGit 后其 status 刷新 index 的 stat 缓存，故"点了小乌龟就能用"。
	/// v3.10.2 已对齐读取侧（status/diff 四件套）；本类把写入侧（add/reset/update-index/rm）
	/// 纳入同一对齐：命令不询问 daemon，显式 pathspec 直接 stat，写入行为与 daemon 状态解耦。
	/// 外部工具不受影响：index 不带 FSMONITOR 扩展时（TortoiseGit、终端 git status 等）
	/// 按"token 缺失"全量重 stat，下一次 daemon 开启的命令会重建扩展数据，只多一次扫描。
	/// </summary>
	public static class ReliableGitFlags
	{
		/// <summary>可靠化前缀（与 GetChangedFilesGitCommand.CreateReliableStatusCommand 同源）。
		/// --no-optional-locks 只禁"机会锁"（如 status 隐式刷新 index）；add/reset 等
		/// 以写 index 为主业的命令照常取锁写入，不受影响。</summary>
		public static readonly string[] Prefix = new string[7]
		{
			"-c", "core.fsmonitor=false",
			"-c", "core.untrackedCache=false",
			"-c", "core.checkStat=default",
			"--no-optional-locks"
		};
	}
}
