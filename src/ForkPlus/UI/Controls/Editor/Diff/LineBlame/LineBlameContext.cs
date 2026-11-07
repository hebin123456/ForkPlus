using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Controls.Editor.Diff.LineBlame
{
	/// <summary>
	/// 单个 FileDiffControl 内共享的 blame 查询上下文：持有仓库/路径/版本信息，
	/// 按（revision spec, path）缓存整文件 blame 结果，避免悬停逐行重复跑 git blame。
	/// 所有查询都在后台线程执行，回调统一回到 UI 线程。
	/// </summary>
	public sealed class LineBlameContext
	{
		private const string WorktreeCacheKey = "\0worktree";

		private readonly object _cacheLock = new object();

		private readonly Dictionary<string, GitCommandResult<GetBlameGitCommand.BlameChunk[]>> _blameCache =
			new Dictionary<string, GitCommandResult<GetBlameGitCommand.BlameChunk[]>>();

		private readonly Dictionary<string, List<Action<GitCommandResult<GetBlameGitCommand.BlameChunk[]>>>> _blameWaiters =
			new Dictionary<string, List<Action<GitCommandResult<GetBlameGitCommand.BlameChunk[]>>>>();

		private readonly Dictionary<string, GitCommandResult<(RevisionWithFiles[], string[])>> _historyCache =
			new Dictionary<string, GitCommandResult<(RevisionWithFiles[], string[])>>();

		private readonly Dictionary<string, List<Action<GitCommandResult<(RevisionWithFiles[], string[])>>>> _historyWaiters =
			new Dictionary<string, List<Action<GitCommandResult<(RevisionWithFiles[], string[])>>>>();

		private bool _disposed;

		public GitModule GitModule { get; }

		[Null]
		public RepositoryUserControl RepositoryUserControl { get; }

		public string Path { get; }

		[Null]
		public string OldPath { get; }

		public LineBlameInfo Info { get; }

		public LineBlameContext(GitModule gitModule, [Null] RepositoryUserControl repositoryUserControl, string path, [Null] string oldPath, LineBlameInfo info)
		{
			GitModule = gitModule;
			RepositoryUserControl = repositoryUserControl;
			Path = path;
			OldPath = oldPath;
			Info = info;
		}

		public void Dispose()
		{
			lock (_cacheLock)
			{
				_disposed = true;
				_blameWaiters.Clear();
				_historyWaiters.Clear();
				_blameCache.Clear();
				_historyCache.Clear();
			}
		}

		public string GetBlamePath(bool oldSide)
		{
			if (oldSide && !string.IsNullOrEmpty(OldPath))
			{
				return OldPath;
			}
			return Path;
		}

		/// <summary>旧一侧的 blame 版本：Revision 模式回退到新版本的第一个父提交，工作区模式回退到 HEAD。</summary>
		public string GetOldRevisionSpec()
		{
			if (!string.IsNullOrEmpty(Info.OldRevisionSpec))
			{
				return Info.OldRevisionSpec;
			}
			if (Info.Mode == LineBlameMode.Revision && !string.IsNullOrEmpty(Info.NewRevisionSpec))
			{
				return Info.NewRevisionSpec + "~1";
			}
			return "HEAD";
		}

		/// <summary>
		/// 查询整文件 blame。<paramref name="spec"/> 为 null 时查询工作区。
		/// 缓存命中立即回调；否则后台执行，期间同一 key 的调用排队等待同一结果。
		/// </summary>
		public void RequestBlame([Null] string spec, bool oldSide, Action<GitCommandResult<GetBlameGitCommand.BlameChunk[]>> callback)
		{
			string path = GetBlamePath(oldSide);
			string key = (spec ?? WorktreeCacheKey) + "\n" + path;
			bool startRequest = false;
			lock (_cacheLock)
			{
				if (_disposed)
				{
					return;
				}
				if (_blameCache.TryGetValue(key, out GitCommandResult<GetBlameGitCommand.BlameChunk[]> cached))
				{
					Dispatcher.UIThread.Post(delegate
					{
						callback(cached);
					});
					return;
				}
				if (!_blameWaiters.TryGetValue(key, out List<Action<GitCommandResult<GetBlameGitCommand.BlameChunk[]>>> waiters))
				{
					waiters = new List<Action<GitCommandResult<GetBlameGitCommand.BlameChunk[]>>>();
					_blameWaiters[key] = waiters;
					startRequest = true;
				}
				waiters.Add(callback);
			}
			if (!startRequest)
			{
				return;
			}
			Task.Run(delegate
			{
				GitCommandResult<GetBlameGitCommand.BlameChunk[]> result;
				try
				{
					result = new GetBlameGitCommand().Execute(GitModule, path, spec);
				}
				catch (Exception ex)
				{
					Log.Error("LineBlame blame failed: " + ex);
					result = GitCommandResult<GetBlameGitCommand.BlameChunk[]>.Failure(new GitCommandError.GenericError(ex.Message));
				}
				List<Action<GitCommandResult<GetBlameGitCommand.BlameChunk[]>>> pending;
				lock (_cacheLock)
				{
					if (_disposed)
					{
						return;
					}
					_blameCache[key] = result;
					pending = _blameWaiters.TryGetValue(key, out List<Action<GitCommandResult<GetBlameGitCommand.BlameChunk[]>>> list) ? list : null;
					_blameWaiters.Remove(key);
				}
				if (pending != null)
				{
					Dispatcher.UIThread.Post(delegate
					{
						foreach (Action<GitCommandResult<GetBlameGitCommand.BlameChunk[]>> item in pending)
						{
							try
							{
								item(result);
							}
							catch (Exception ex)
							{
								Log.Error("LineBlame callback failed: " + ex);
							}
						}
					});
				}
			});
		}

		/// <summary>查询单行历史（git log -L line,line:path），按行号缓存。</summary>
		public void RequestHistory(string path, int line, [Null] Sha? startSha, Action<GitCommandResult<(RevisionWithFiles[], string[])>> callback)
		{
			string key = path + "\n" + line + "\n" + (startSha.HasValue ? startSha.Value.ToString() : "HEAD");
			bool startRequest = false;
			lock (_cacheLock)
			{
				if (_disposed)
				{
					return;
				}
				if (_historyCache.TryGetValue(key, out GitCommandResult<(RevisionWithFiles[], string[])> cached))
				{
					Dispatcher.UIThread.Post(delegate
					{
						callback(cached);
					});
					return;
				}
				if (!_historyWaiters.TryGetValue(key, out List<Action<GitCommandResult<(RevisionWithFiles[], string[])>>> waiters))
				{
					waiters = new List<Action<GitCommandResult<(RevisionWithFiles[], string[])>>>();
					_historyWaiters[key] = waiters;
					startRequest = true;
				}
				waiters.Add(callback);
			}
			if (!startRequest)
			{
				return;
			}
			Task.Run(delegate
			{
				GitCommandResult<(RevisionWithFiles[], string[])> result;
				try
				{
					result = new GetFileHistoryGitCommand().Execute(GitModule, path, new Range(line, line), startSha);
				}
				catch (Exception ex)
				{
					Log.Error("LineBlame history failed: " + ex);
					result = GitCommandResult<(RevisionWithFiles[], string[])>.Failure(new GitCommandError.GenericError(ex.Message));
				}
				List<Action<GitCommandResult<(RevisionWithFiles[], string[])>>> pending;
				lock (_cacheLock)
				{
					if (_disposed)
					{
						return;
					}
					_historyCache[key] = result;
					pending = _historyWaiters.TryGetValue(key, out List<Action<GitCommandResult<(RevisionWithFiles[], string[])>>> list) ? list : null;
					_historyWaiters.Remove(key);
				}
				if (pending != null)
				{
					Dispatcher.UIThread.Post(delegate
					{
						foreach (Action<GitCommandResult<(RevisionWithFiles[], string[])>> item in pending)
						{
							try
							{
								item(result);
							}
							catch (Exception ex)
							{
								Log.Error("LineBlame history callback failed: " + ex);
							}
						}
					});
				}
			});
		}

		/// <summary>在按行号升序排列的 blame 块数组中定位包含指定行的块（二分查找）。</summary>
		public static bool TryFindChunk([Null] GetBlameGitCommand.BlameChunk[] chunks, int line, out GetBlameGitCommand.BlameChunk chunk)
		{
			chunk = null;
			if (chunks == null || chunks.Length == 0 || line < 1)
			{
				return false;
			}
			int low = 0;
			int high = chunks.Length - 1;
			while (low <= high)
			{
				int mid = low + (high - low) / 2;
				GetBlameGitCommand.BlameChunk current = chunks[mid];
				if (line < current.LineNumber)
				{
					high = mid - 1;
				}
				else if (line >= current.LineNumber + current.LineCount)
				{
					low = mid + 1;
				}
				else
				{
					chunk = current;
					return true;
				}
			}
			return false;
		}
	}
}
