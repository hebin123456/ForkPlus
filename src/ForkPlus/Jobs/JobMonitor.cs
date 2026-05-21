using System;
using System.Text;

namespace ForkPlus.Jobs
{
	public class JobMonitor
	{
		public readonly int TotalProgress = 100;

		private object _updateLock = new object();

		private double? _progress;

		[Null]
		private string _progressMessage;

		private JobMonitorState _state;

		[Null]
		private Action _cancellationAction;

		[Null]
		private Action _progressAction;

		private StringBuilder _outputStringBuilder = new StringBuilder(1024);

		public double? Progress
		{
			get
			{
				lock (_updateLock)
				{
					return _progress;
				}
			}
		}

		[Null]
		public string ProgressMessage
		{
			get
			{
				lock (_updateLock)
				{
					return _progressMessage;
				}
			}
		}

		public JobMonitorState State
		{
			get
			{
				lock (_updateLock)
				{
					return _state;
				}
			}
		}

		public bool IsCanceled
		{
			get
			{
				lock (_updateLock)
				{
					return _state == JobMonitorState.Canceled;
				}
			}
		}

		public string Output
		{
			get
			{
				lock (_outputStringBuilder)
				{
					return _outputStringBuilder.ToString();
				}
			}
		}

		public int OutputLength
		{
			get
			{
				lock (_outputStringBuilder)
				{
					return _outputStringBuilder.Length;
				}
			}
		}

		public void SetCancellationAction([Null] Action cancellationAction)
		{
			lock (_updateLock)
			{
				_cancellationAction = cancellationAction;
			}
		}

		public void SetProgressAction([Null] Action progressAction)
		{
			lock (_updateLock)
			{
				_progressAction = progressAction;
			}
		}

		public void Cancel()
		{
			lock (_updateLock)
			{
				if (_state != JobMonitorState.Canceled)
				{
					_progressMessage = "canceled";
					_state = JobMonitorState.Canceled;
					_cancellationAction?.Invoke();
				}
			}
		}

		public void Success([Null] string resultMessage)
		{
			lock (_updateLock)
			{
				_progress = TotalProgress;
				_progressMessage = resultMessage;
				_state = JobMonitorState.Succeeded;
			}
		}

		public void Fail(string resultErrorMessage)
		{
			lock (_updateLock)
			{
				_progress = TotalProgress;
				_progressMessage = resultErrorMessage;
				_state = JobMonitorState.Failed;
			}
		}

		public void Update(double progress, string message, JobMonitorState state = JobMonitorState.InProgress)
		{
			lock (_updateLock)
			{
				_progress = progress;
				_progressMessage = message;
				_state = state;
			}
			_progressAction?.Invoke();
		}

		public void SetState(JobMonitorState state)
		{
			lock (_updateLock)
			{
				_state = state;
			}
		}

		public void Append(string str)
		{
			lock (_outputStringBuilder)
			{
				_outputStringBuilder.Append(str);
			}
		}

		public void AppendOutputLine(string line)
		{
			if (line.Length > 0 && line[line.Length - 1] == '\n')
			{
				lock (_outputStringBuilder)
				{
					_outputStringBuilder.Append(line);
					return;
				}
			}
			lock (_outputStringBuilder)
			{
				_outputStringBuilder.AppendLine(line);
			}
		}

		public void AppendCommandHeader(string line)
		{
			lock (_outputStringBuilder)
			{
				if (_outputStringBuilder.Length > 4)
				{
					int index = _outputStringBuilder.Length - 1;
					index = GetEolStart(_outputStringBuilder, index);
					if (index > 0)
					{
						index = GetEolStart(_outputStringBuilder, index);
						if (index > 0)
						{
							_outputStringBuilder.Append(line);
							return;
						}
						_outputStringBuilder.Append('\n');
					}
				}
				_outputStringBuilder.Append(line);
			}
		}

		private int GetEolStart(StringBuilder sb, int index)
		{
			if (sb[index] == '\n')
			{
				if (sb[index - 1] == '\r')
				{
					return index - 2;
				}
				return index - 1;
			}
			return -1;
		}
	}
}
