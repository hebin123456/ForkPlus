using System.Text;
using System.Threading;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Interaction
{
	public class ProcessOutputHandler
	{
		public enum OutputKind
		{
			Stdout,
			Stderr
		}

		private readonly StringBuilder _fullOutput = new StringBuilder();

		private readonly StringBuilder _stderr = new StringBuilder();

		private readonly JobMonitor _monitor;

		private readonly bool _isBt;

		public ProcessOutputHandler(JobMonitor monitor, bool isBt = true)
		{
			_monitor = monitor;
			_isBt = isBt;
		}

		public void AppendTrimmedLine(OutputKind outputKind, string line)
		{
			switch (outputKind)
			{
			case OutputKind.Stdout:
				_monitor.Append(line);
				if (!_isBt)
				{
					_monitor.Append("\n");
				}
				lock (_fullOutput)
				{
					_fullOutput.Append(line);
					if (!_isBt)
					{
						_fullOutput.Append('\n');
					}
					break;
				}
			case OutputKind.Stderr:
				if (_monitor.HandleGitProgress(line))
				{
					break;
				}
				if (line.Contains("bash: /dev/tty: No such device or address"))
				{
					_monitor.AppendOutputLine("Cancel...");
					Thread.Sleep(100);
					_monitor.Cancel();
				}
				_monitor.Append(line);
				if (!_isBt)
				{
					_monitor.Append("\n");
				}
				lock (_fullOutput)
				{
					_fullOutput.Append(line);
					if (!_isBt)
					{
						_fullOutput.Append('\n');
					}
				}
				lock (_stderr)
				{
					_stderr.Append(line);
					if (!_isBt)
					{
						_stderr.Append('\n');
					}
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
