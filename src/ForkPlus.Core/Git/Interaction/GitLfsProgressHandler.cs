using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Interaction
{
	public class GitLfsProgressHandler : IDisposable
	{
		private static readonly Regex LfsProgressRegEx = new Regex("^(.+?)\\s{1}(\\d+)\\/(\\d+)\\s{1}(\\d+)\\/(\\d+)\\s{1}(.+)$");

		private bool _disposed;

		private readonly TailReader _tailReader;

		private readonly CancelHandler _tailHandler = new CancelHandler();

		private Dictionary<string, FileProgress> _fileProgresses;

		public string TempProgressFile { get; }

		public (string, string)[] EnvironmentVariables => new(string, string)[1] { ("GIT_LFS_PROGRESS", TempProgressFile) };

		public GitLfsProgressHandler(JobMonitor monitor)
		{
			GitLfsProgressHandler gitLfsProgressHandler = this;
			TempProgressFile = Path.GetTempFileName();
			Log.Info("Temp LFS progress file: " + TempProgressFile);
			_fileProgresses = new Dictionary<string, FileProgress>();
			_tailReader = new TailReader(TempProgressFile);
			_tailReader.Tail(delegate(string line)
			{
				if (!monitor.IsCanceled)
				{
					gitLfsProgressHandler.HandleProgressLine(line, monitor);
				}
			}, _tailHandler);
		}

		public void Dispose()
		{
			if (!_disposed)
			{
				_tailHandler.Cancel();
				try
				{
					File.Delete(TempProgressFile);
				}
				catch
				{
				}
				_disposed = true;
			}
		}

		private void HandleProgressLine(string line, JobMonitor monitor)
		{
			Match match = LfsProgressRegEx.Match(line);
			if (match.Success && match.Groups.Count == 7)
			{
				string value = match.Groups[1].Value;
				if (!int.TryParse(match.Groups[2].Value, out var result))
				{
					result = 1;
				}
				if (!int.TryParse(match.Groups[3].Value, out var result2))
				{
					result2 = 1;
				}
				if (!long.TryParse(match.Groups[4].Value, out var result3))
				{
					result3 = 1L;
				}
				if (!long.TryParse(match.Groups[5].Value, out var result4))
				{
					result4 = 1L;
				}
				if (result2 == 0)
				{
					result2 = 1;
				}
				if (result4 == 0L)
				{
					result4 = 1L;
				}
				string value2 = match.Groups[6].Value;
				_fileProgresses[value2] = new FileProgress
				{
					DownloadedBytes = result3,
					TotalBytes = result4
				};
				double num = 0.0;
				double num2 = 1.0 / (double)result2;
				foreach (FileProgress value3 in _fileProgresses.Values)
				{
					num += value3.Ratio * num2;
				}
				double progress = 100.0 * num;
				string text = FileSizeFormatter.Format(result3);
				string text2 = FileSizeFormatter.Format(result4);
				monitor.Update(progress, value + " " + value2 + " (" + text + " of " + text2 + ")");
			}
			else
			{
				monitor.AppendOutputLine(line);
			}
		}
	}
}
