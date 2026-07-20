using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using ForkPlus.Services;

namespace ForkPlus
{
	internal struct Benchmarker : IDisposable
	{
		private Stopwatch _stopwatch;

		private string _target;

		public Benchmarker([CallerMemberName] string target = "")
		{
			_target = target;
			_stopwatch = Stopwatch.StartNew();
		}

		public void AssertElapsed(int limitMs)
		{
			if (_stopwatch.ElapsedMilliseconds >= limitMs)
			{
				char c = (Thread.CurrentThread.IsBackground ? ' ' : '*');
				Log.Warn($"{c}{_stopwatch.ElapsedMilliseconds,7} ms: {_target}");
			}
		}

		public void LogElapsed()
		{
			if (Thread.CurrentThread.IsBackground)
			{
				Log.Debug($" {_stopwatch.ElapsedMilliseconds,7} ms: {_target}");
			}
			else
			{
				Log.Debug($"*{_stopwatch.ElapsedMilliseconds,7} ms: {_target}");
			}
		}

		public void ReportElapsed()
		{
			PerformanceTelemetry.Record(_target, _stopwatch.ElapsedMilliseconds, Thread.CurrentThread.IsBackground);
			bool logElapsedTime = ServiceLocator.UserSettings.LogElapsedTime;
			if (!logElapsedTime)
			{
				return;
			}
			if (Thread.CurrentThread.IsBackground)
			{
				if (_stopwatch.ElapsedMilliseconds >= 2000 || logElapsedTime)
				{
					Log.Debug($" {_stopwatch.ElapsedMilliseconds,7} ms: {_target}");
				}
			}
			else if (_stopwatch.ElapsedMilliseconds >= 200 || logElapsedTime)
			{
				Log.Debug($"*{_stopwatch.ElapsedMilliseconds,7} ms: {_target}");
			}
		}

		public void Dispose()
		{
			ReportElapsed();
		}
	}
}
