using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ForkPlus.Jobs.Impl;

namespace ForkPlus.Jobs
{
	public class JobQueue
	{
		public static readonly int JobLogMaxSize = 100;

		public static readonly TimeSpan ZombieDelay = TimeSpan.FromSeconds(2.0);

		// v4.0.6：进程内全部 JobQueue 实例的弱引用注册表（UI 冻结看门狗聚合"卡住当下
		// 有哪些后台任务在跑"用）。各窗口/控件自建队列（MainWindow、RepositoryUserControl、
		// 各账号页等十余处），无注册表时看门狗只能看到静态可达的那一两个。弱引用保证
		// 注册表本身不延长队列宿主（窗口/控件）的生命周期；查询时顺带清理死引用。
		private static readonly object _allQueuesLock = new object();

		private static readonly List<WeakReference<JobQueue>> _allQueues = new List<WeakReference<JobQueue>>();

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
			RegisterInstance(this);
		}

		/// <summary>v4.0.6：全进程所有队列实例中处于 Running 状态的任务聚合（看门狗冻结报告用）。</summary>
		public static Job[] GetRunningJobsGlobally()
		{
			JobQueue[] queues = GetAllQueues();
			if (queues.Length == 0)
			{
				return Array.Empty<Job>();
			}
			List<Job> runningJobs = new List<Job>();
			JobQueue[] array = queues;
			foreach (JobQueue jobQueue in array)
			{
				runningJobs.AddRange(jobQueue.GetRunningJobs());
			}
			return runningJobs.ToArray();
		}

		/// <summary>v4.0.6：本队列运行中任务快照。直读 _runningJobs（含未带 SaveToLog
		/// 标志的任务——GetJobHistory 只覆盖 _jobLog，会漏掉大多数 UI 触发的刷新任务）。</summary>
		public Job[] GetRunningJobs()
		{
			lock (_lock)
			{
				return _runningJobs.Where((Job x) => x.Status == JobStatus.Running).ToArray();
			}
		}

		private static void RegisterInstance(JobQueue queue)
		{
			lock (_allQueuesLock)
			{
				_allQueues.Add(new WeakReference<JobQueue>(queue));
			}
		}

		private static JobQueue[] GetAllQueues()
		{
			lock (_allQueuesLock)
			{
				// 顺带清理已被 GC 回收的宿主留下的死引用。
				List<JobQueue> aliveQueues = new List<JobQueue>(_allQueues.Count);
				for (int i = _allQueues.Count - 1; i >= 0; i--)
				{
					if (!_allQueues[i].TryGetTarget(out JobQueue target))
					{
						_allQueues.RemoveAt(i);
					}
					else
					{
						aliveQueues.Add(target);
					}
				}
				return aliveQueues.ToArray();
			}
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
