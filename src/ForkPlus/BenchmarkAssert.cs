using System;
using System.Runtime.CompilerServices;

namespace ForkPlus
{
	internal struct BenchmarkAssert : IDisposable
	{
		private readonly Benchmarker _benchmarker;

		private readonly int _limitMs;

		public BenchmarkAssert(int limitMs, [CallerMemberName] string target = "")
		{
			_benchmarker = new Benchmarker(target);
			_limitMs = limitMs;
		}

		public void Dispose()
		{
			_benchmarker.AssertElapsed(_limitMs);
		}
	}
}
