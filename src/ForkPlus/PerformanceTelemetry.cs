using System;
using System.Collections.Generic;
using System.Linq;

namespace ForkPlus
{
	internal sealed class PerformanceSample
	{
		public DateTime TimestampUtc { get; }

		public string Name { get; }

		public long ElapsedMilliseconds { get; }

		public bool BackgroundThread { get; }

		public PerformanceSample(DateTime timestampUtc, string name, long elapsedMilliseconds, bool backgroundThread)
		{
			TimestampUtc = timestampUtc;
			Name = name;
			ElapsedMilliseconds = elapsedMilliseconds;
			BackgroundThread = backgroundThread;
		}
	}

	internal static class PerformanceTelemetry
	{
		private const int MaxSamples = 256;

		private static readonly object Sync = new object();

		private static readonly Queue<PerformanceSample> Samples = new Queue<PerformanceSample>();

		public static void Record(string name, long elapsedMilliseconds, bool backgroundThread)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				return;
			}
			lock (Sync)
			{
				Samples.Enqueue(new PerformanceSample(DateTime.UtcNow, name, elapsedMilliseconds, backgroundThread));
				while (Samples.Count > MaxSamples)
				{
					Samples.Dequeue();
				}
			}
		}

		public static PerformanceSample[] RecentSamples()
		{
			lock (Sync)
			{
				return Samples.ToArray();
			}
		}

		public static PerformanceSample[] SlowestSamples(int count)
		{
			lock (Sync)
			{
				return Samples
					.OrderByDescending((PerformanceSample sample) => sample.ElapsedMilliseconds)
					.Take(Math.Max(0, count))
					.ToArray();
			}
		}

		public static void Clear()
		{
			lock (Sync)
			{
				Samples.Clear();
			}
		}
	}
}
