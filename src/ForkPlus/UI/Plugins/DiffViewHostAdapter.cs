using System;
using System.IO;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Plugins.Abstractions;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Plugins
{
	/// <summary>
	/// v5.0.0：宿主能力桥实现（<see cref="IDiffViewHost"/>）——插件的一切外部副作用都从这里走：
	/// LFS smudge/缓存、.tga 图片解码、保存对话框、错误弹窗。封装 JobQueue/Git 命令/对话框，
	/// 插件视图保持纯 UI 逻辑。随一次对比（PluginDiffViewControl）创建，对比卸载时 Dispose 取消任务。
	/// </summary>
	public sealed class DiffViewHostAdapter : IDiffViewHost, IDisposable
	{
		[Null]
		private RepositoryUserControl _repositoryUserControl;

		private readonly JobQueue _jobQueue = new JobQueue();

		public DiffViewHostAdapter([Null] RepositoryUserControl repositoryUserControl)
		{
			_repositoryUserControl = repositoryUserControl;
		}

		/// <summary>同一插件视图被复用（侧栏切换文件）时刷新仓库上下文。</summary>
		public void UpdateRepository([Null] RepositoryUserControl repositoryUserControl)
		{
			_repositoryUserControl = repositoryUserControl;
		}

		/// <summary>图片字节就绪前的宿主侧预处理（当前为 .tga 经 biturbo 原生解码；其余原样返回）。</summary>
		public MemoryStream PrepareImageStream(string path, MemoryStream raw)
		{
			if (path == null || raw == null || Path.GetExtension(path) != ".tga")
			{
				return raw;
			}
			GitCommandResult<MemoryStream> result = BiturboImageDecoder.DecodeImageData(raw.ToArray());
			if (result.Succeeded)
			{
				return result.Result;
			}
			Log.Error(result.Error.FriendlyDescription);
			return raw;
		}

		/// <summary>取 LFS 本地缓存（.git/lfs/objects 命中时直接给字节；未命中/失败返回 null）。</summary>
		public MemoryStream GetCachedLfsData(LfsRef lfs)
		{
			GitModule gitModule = _repositoryUserControl?.GitModule;
			if (gitModule == null || lfs == null)
			{
				return null;
			}
			GitCommandResult<MemoryStream> result = new GitLfsGetCachedFileGitCommand().Execute(gitModule.CommonGitDir, lfs.Sha256);
			if (!result.Succeeded)
			{
				return null;
			}
			return result.Result;
		}

		/// <summary>
		/// 启动 LFS smudge 任务：progress/completed 均在 UI 线程回调（null 进度表示不确定）。
		/// 返回值 Dispose 即取消（对应「取消 LFS 下载」按钮）。
		/// </summary>
		public IDisposable RunLfsSmudge(LfsRef lfs, Action<double?> progress, Action<LfsSmudgeResult> completed)
		{
			GitModule gitModule = _repositoryUserControl?.GitModule;
			if (gitModule == null || lfs == null)
			{
				completed?.Invoke(LfsSmudgeResult.Failure("Repository is not available"));
				return NoOpDisposable.Instance;
			}
			LfsPointer lfsPointer = new LfsPointer(lfs.Sha256, lfs.Size);
			Job job = _jobQueue.Add(PreferencesLocalization.Current("Smudge LFS image"), delegate (JobMonitor monitor)
			{
				if (monitor.IsCanceled)
				{
					return;
				}
				monitor.SetProgressAction(delegate
				{
					global::Avalonia.Threading.Dispatcher.UIThread.Post(delegate
					{
						progress?.Invoke(monitor.Progress);
					});
				});
				GitCommandResult<MemoryStream> response = new SmudgeLfsFileCommand().Execute(gitModule, lfsPointer, monitor);
				monitor.SetProgressAction(null);
				global::Avalonia.Threading.Dispatcher.UIThread.Post(delegate
				{
					if (!monitor.IsCanceled)
					{
						if (response.Succeeded)
						{
							completed?.Invoke(LfsSmudgeResult.Success(response.Result));
						}
						else
						{
							completed?.Invoke(LfsSmudgeResult.Failure(response.Error?.FriendlyDescription ?? "Failed to smudge LFS file"));
						}
					}
				});
			});
			return new JobCancellationDisposable(job);
		}

		/// <summary>保存对话框 + 写文件（suggestedFileName 为建议名）。用户取消返回 false。</summary>
		public bool SaveFileAs(string suggestedFileName, MemoryStream data)
		{
			if (data == null)
			{
				return false;
			}
			string initialDirectory = RepositoryManager.Instance.DefaultSourceDir();
			if (!OpenDialog.SelectFileSaveLocation(null, PreferencesLocalization.Current("Select location"), initialDirectory, suggestedFileName, out var filePath))
			{
				return false;
			}
			byte[] array = data.ToArray();
			try
			{
				File.WriteAllBytes(filePath, array);
				return true;
			}
			catch (Exception ex)
			{
				Log.Error($"Cannot save file: {ex}");
				new Dialogs.ErrorWindow(ex.ToString()).ShowDialog();
				return false;
			}
		}

		/// <summary>弹出错误窗（宿主样式）。</summary>
		public void ShowError(string message)
		{
			new Dialogs.ErrorWindow(message).ShowDialog();
		}

		/// <summary>占位：任务取消由返回的 IDisposable（Dispose 即 Cancel）承担；JobQueue 无需显式释放。</summary>
		public void Dispose()
		{
		}

		private sealed class NoOpDisposable : IDisposable
		{
			public static readonly NoOpDisposable Instance = new NoOpDisposable();

			public void Dispose()
			{
			}
		}

		/// <summary>Job → IDisposable 适配：Dispose 即取消（对应用户点「取消」按钮）。</summary>
		private sealed class JobCancellationDisposable : IDisposable
		{
			[Null]
			private readonly Job _job;

			public JobCancellationDisposable([Null] Job job)
			{
				_job = job;
			}

			public void Dispose()
			{
				try
				{
					_job?.Monitor.Cancel();
				}
				catch (Exception ex)
				{
					Log.Error("Failed to cancel LFS smudge job", ex);
				}
			}
		}
	}
}
