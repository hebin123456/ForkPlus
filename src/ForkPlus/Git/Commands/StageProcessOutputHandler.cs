using System.Collections.Generic;
using System.Text;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class StageProcessOutputHandler
	{
		public enum OutputKind
		{
			Stdout,
			Stderr
		}

		private int _totalFiles;

		private readonly StringBuilder _fullOutput = new StringBuilder();

		private readonly StringBuilder _stderr = new StringBuilder();

		private readonly HashSet<string> _files;

		private readonly JobMonitor _monitor;

		public StageProcessOutputHandler(JobMonitor monitor, HashSet<string> files)
		{
			_monitor = monitor;
			_files = files;
			_totalFiles = files.Count;
		}

		public void AppendTrimmedLine(OutputKind outputKind, string line)
		{
			switch (outputKind)
			{
			case OutputKind.Stdout:
				_monitor.Append(line);
				if (line.StartsWith("add "))
				{
					string item = line.Trim().TrimStart("add ").TrimStart("'")
						.TrimEnd("'");
					if (_files.Remove(item))
					{
						int num = _totalFiles - _files.Count;
						double progress = 100.0 * (double)num / (double)_totalFiles;
						_monitor.Update(progress, $"Staging {num}/{_totalFiles}...");
					}
				}
				lock (_fullOutput)
				{
					_fullOutput.Append(line);
					break;
				}
			case OutputKind.Stderr:
				if (_monitor.HandleGitProgress(line))
				{
					break;
				}
				_monitor.Append(line);
				lock (_fullOutput)
				{
					_fullOutput.Append(line);
				}
				lock (_stderr)
				{
					_stderr.Append(line);
					break;
				}
			}
		}

		public void StdoutHandler(string line)
		{
			AppendTrimmedLine(OutputKind.Stdout, line);
		}

		public void StderrHandler(string line)
		{
			AppendTrimmedLine(OutputKind.Stderr, line);
		}

		public string FullOutput()
		{
			lock (_fullOutput)
			{
				return _fullOutput.ToString();
			}
		}

		public string Stderr()
		{
			lock (_stderr)
			{
				return _stderr.ToString();
			}
		}
	}
}
