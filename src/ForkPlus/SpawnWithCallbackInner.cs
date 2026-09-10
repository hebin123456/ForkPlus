using System;
using ForkPlus.Biturbo;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;

namespace ForkPlus
{
	internal class SpawnWithCallbackInner : IDisposable
	{
		private bool _disposed;

		private readonly string _path;

		[Null]
		private readonly string _workingDirectory;

		private readonly string[] _arguments;

		private readonly string[] _env;

		[Null]
		private readonly byte[] _stdin;

		private Action<string> _stdoutLineHandler;

		private Action<string> _stderrLineHandler;

		[Null]
		private readonly JobMonitor _monitor;

		public SpawnWithCallbackInner(string path, [Null] string workingDirectory, string[] arguments, string[] env, [Null] byte[] stdin, Action<string> stdoutLineHandler, Action<string> stderrLineHandler, [Null] JobMonitor monitor)
		{
			_path = path;
			_workingDirectory = workingDirectory;
			_arguments = arguments;
			_env = env;
			_stdin = stdin;
			_stdoutLineHandler = stdoutLineHandler;
			_stderrLineHandler = stderrLineHandler;
			_monitor = monitor;
		}

		public void Dispose()
		{
			if (!_disposed)
			{
				_stdoutLineHandler = null;
				_stderrLineHandler = null;
				GC.SuppressFinalize(this);
				_disposed = true;
			}
		}

		~SpawnWithCallbackInner()
		{
			Dispose();
		}

		public Result<int, ISpawnError> Spawn()
		{
			using GCHandleProvider gCHandleProvider = new GCHandleProvider(this);
			BtProcessCancellationToken cancellationToken = Bt.bt_new_process_cancellation_token();
			BtSpawnWithCallbackResult out_result = default(BtSpawnWithCallbackResult);
			_monitor?.SetCancellationAction(delegate
			{
				BtResult btResult2 = Bt.bt_kill_process_cancellation_token(ref cancellationToken);
				if (btResult2 != 0)
				{
					GitCommandError gitCommandError = btResult2.ToGitCommandError();
					Log.Warn("Failed to kill process:\n" + gitCommandError.FriendlyDescription);
				}
			});
			string path = _path;
			string workingDirectory = _workingDirectory;
			string[] arguments = _arguments;
			long args_len = _arguments.Length;
			string[] env = _env;
			long evn_len = _env.Length;
			byte[] stdin = _stdin;
			byte[] stdin2 = _stdin;
			BtResult btResult = Bt.bt_spawn_with_callback(path, workingDirectory, arguments, args_len, env, evn_len, stdin, (stdin2 != null) ? stdin2.Length : 0, gCHandleProvider.Pointer, HandleCallback, ref cancellationToken, ref out_result);
			_monitor?.SetCancellationAction(null);
			Bt.bt_release_process_cancellation_token(ref cancellationToken);
			if (btResult != 0)
			{
				return Result<int, ISpawnError>.Err(new GenericError(btResult.ToGitCommandError().FriendlyDescription));
			}
			return Result<int, ISpawnError>.Ok(out_result.status);
		}

		private static void HandleCallback(IntPtr cbTargetPtr, byte kind, IntPtr dataPtr, long dataLen)
		{
			// v4.0.6：本方法由 biturbo 的读管道线程反向调用（reverse P/Invoke）。异常一旦
			// 从这里抛出，会试图展开穿过 native（Rust）栈帧——CLR 检测到后直接 FailFast
			// 终止进程，任何 App 级兜底（Dispatcher/AppDomain/TaskScheduler 三路）都拦不住，
			// 表象为"跑着带输出的 git 命令（fetch/push/stage）时进程无声消失"。因此边界处
			// 就地拦截：解析/handler 抛出的任何托管异常吞噬并落崩溃转储（kind=SpawnCallback），
			// 最坏丢一行输出日志，进程存活。
			string commandPath = null;
			try
			{
				if (cbTargetPtr.AsManagedObject() is SpawnWithCallbackInner spawnWithCallbackInner)
				{
					commandPath = spawnWithCallbackInner._path;
					string utf8String = dataPtr.GetUtf8String(dataLen);
					((kind == 0) ? spawnWithCallbackInner._stdoutLineHandler : spawnWithCallbackInner._stderrLineHandler)?.Invoke(utf8String);
				}
			}
			catch (Exception exception)
			{
				CrashDumper.Dump("SpawnCallback", exception, "path=" + (commandPath ?? "<null>") + " kind=" + ((kind == 0) ? "stdout" : "stderr") + " len=" + dataLen);
			}
		}
	}
}
