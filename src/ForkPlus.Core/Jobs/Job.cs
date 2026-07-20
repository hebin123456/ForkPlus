using System;
using System.Diagnostics;

namespace ForkPlus.Jobs
{
	[DebuggerDisplay("{Id}: {Name}")]
	public class Job
	{
		private static object _nextIdLock = new object();

		private static int _nextId = 0;

		private object _statusLock = new object();

		private JobStatus _status;

		private Action<JobMonitor> _action;

		public int Id { get; }

		public string Name { get; }

		public JobFlags Flags { get; }

		public bool ShowMessageWhenDone { get; }

		public JobMonitor Monitor { get; }

		public JobStatus Status
		{
			get
			{
				lock (_statusLock)
				{
					return _status;
				}
			}
			set
			{
				lock (_statusLock)
				{
					if (_status != value)
					{
						switch (value)
						{
						case JobStatus.Running:
							StartTime = DateTime.UtcNow;
							break;
						case JobStatus.Pending:
							PendingTime = DateTime.UtcNow;
							break;
						case JobStatus.Finished:
							FinishTime = DateTime.UtcNow;
							break;
						}
						_status = value;
					}
				}
			}
		}

		public DateTime? PendingTime { get; private set; }

		public DateTime? StartTime { get; private set; }

		public DateTime? FinishTime { get; private set; }

		private static int GetNextId()
		{
			lock (_nextIdLock)
			{
				return _nextId = (_nextId + 1) % 1000;
			}
		}

		public Job(string name, Action<JobMonitor> action, JobFlags flags = JobFlags.SaveToLog | JobFlags.ShowOnToolbar, bool showMessageWhenDone = false)
		{
			Id = GetNextId();
			Name = name;
			Flags = flags;
			ShowMessageWhenDone = showMessageWhenDone;
			Monitor = new JobMonitor();
			Status = JobStatus.Pending;
			_action = action;
		}

		public void Run()
		{
			if (_action != null)
			{
				_action(Monitor);
				_action = null;
				Monitor.SetCancellationAction(null);
				Monitor.SetProgressAction(null);
			}
		}
	}
}
