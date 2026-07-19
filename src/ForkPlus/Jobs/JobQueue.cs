using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ForkPlus.Jobs.Impl;

namespace ForkPlus.Jobs
{
	public class JobQueue
	{
		public static readonly int JobLogMaxSize = 100;

		public static readonly TimeSpan ZombieDelay = TimeSpan.FromSeconds(2.0);

		private object _lock = new object();

		private readonly TaskScheduler _taskScheduler;

		private readonly List<Job> _runningJobs;

		private readonly CircularArray<Job> _jobLog;

		private uint _jobLogVersion;

		public bool IsIdle
		{
			get
			{
				lock (_lock)
				{
					return _runningJobs.Count == 0;
				}
			}
		}

		[Null]
		public Job PrimaryJob
		{
			get
			{
				DateTime utcNow = DateTime.UtcNow;
				lock (_lock)
				{
					for (int num = _jobLog.Count - 1; num >= 0; num--)
					{
						Job job = _jobLog[num];
						if ((job.Flags & JobFlags.ShowOnToolbar) != 0)
						{
							DateTime? startTime;
							if (job.Status == JobStatus.Running)
							{
								if (job.Flags.HasFlag(JobFlags.ShowOnToolbarWhenFinished))
								{
									return job;
								}
								startTime = job.StartTime;
								if (startTime.HasValue)
								{
									DateTime valueOrDefault = startTime.GetValueOrDefault();
									if ((utcNow - valueOrDefault).TotalMilliseconds > 500.0)
									{
										return job;
									}
								}
							}
							startTime = job.FinishTime;
							if (startTime.HasValue)
							{
								DateTime valueOrDefault2 = startTime.GetValueOrDefault();
								if (job.Status == JobStatus.Finished && job.Monitor.ProgressMessage != null && utcNow - valueOrDefault2 < ZombieDelay && job.Flags.HasFlag(JobFlags.ShowOnToolbarWhenFinished))
								{
									return job;
								}
							}
						}
					}
				}
				return null;
			}
		}

		public uint JobLogVersion
		{
			get
			{
				lock (_lock)
				{
					return _jobLogVersion;
				}
			}
		}

		public JobQueue()
		{
			_taskScheduler = TaskScheduler.Default;
			_runningJobs = new List<Job>(8);
			_jobLog = new CircularArray<Job>(JobLogMaxSize);
		}

		public Job Add(string name, Action<JobMonitor> action, JobFlags flags = JobFlags.Default, bool showMessageWhenDone = true)
		{
			Job job = new Job(name, action, flags, showMessageWhenDone);
			Schedule(job);
			return job;
		}

		public void Schedule(Job job)
		{
			TaskCreationOptions creationOptions = (((job.Flags & JobFlags.LongRunning) != 0) ? TaskCreationOptions.LongRunning : TaskCreationOptions.None);
			Task task = new Task(delegate
			{
				job.Status = JobStatus.Running;
				// v3.1.1：包 try/finally，确保 action 抛异常时 Job 也能从 _runningJobs 移除、
				// Status 置为 Finished，否则 IsIdle 永远为 false、状态栏永远转圈
				try
				{
					job.Run();
				}
				finally
				{
					RemoveJob(job);
					job.Status = JobStatus.Finished;
				}
			}, creationOptions);
			AddJob(job);
			task.Start(_taskScheduler);
		}

		public Job[] GetJobHistory(Func<Job, bool> isIncluded)
		{
			lock (_lock)
			{
				return _jobLog.Filter(isIncluded);
			}
		}

		[Null]
		public Job FindJob(string name)
		{
			lock (_lock)
			{
				return IReadOnlyListExtensions.FirstItem(_runningJobs, (Job x) => x.Name == name);
			}
		}

		private void AddJob(Job job)
		{
			lock (_lock)
			{
				_runningJobs.Add(job);
				if ((job.Flags & JobFlags.SaveToLog) != 0)
				{
					_jobLog.Add(job);
					_jobLogVersion++;
				}
			}
		}

		private void RemoveJob(Job job)
		{
			lock (_lock)
			{
				_runningJobs.UnstableRemove((Job x) => x == job);
				if ((job.Flags & JobFlags.ShowOnToolbar) != 0)
				{
					_jobLogVersion++;
				}
			}
		}
	}
}
