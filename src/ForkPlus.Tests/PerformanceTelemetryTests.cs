using System.Linq;
using Xunit;

namespace ForkPlus.Tests
{
	public class PerformanceTelemetryTests
	{
		[Fact]
		public void Record_AddsSampleToRecentSamples()
		{
			PerformanceTelemetry.Clear();

			PerformanceTelemetry.Record("status", 123, backgroundThread: true);

			PerformanceSample sample = Assert.Single(PerformanceTelemetry.RecentSamples());
			Assert.Equal("status", sample.Name);
			Assert.Equal(123, sample.ElapsedMilliseconds);
			Assert.True(sample.BackgroundThread);
		}

		[Fact]
		public void Record_KeepsBoundedSampleSet()
		{
			PerformanceTelemetry.Clear();

			for (int i = 0; i < 300; i++)
			{
				PerformanceTelemetry.Record("sample-" + i, i, backgroundThread: false);
			}

			PerformanceSample[] samples = PerformanceTelemetry.RecentSamples();
			Assert.Equal(256, samples.Length);
			Assert.Equal("sample-44", samples.First().Name);
			Assert.Equal("sample-299", samples.Last().Name);
		}

		[Fact]
		public void SlowestSamples_ReturnsDescendingByElapsed()
		{
			PerformanceTelemetry.Clear();
			PerformanceTelemetry.Record("fast", 1, backgroundThread: false);
			PerformanceTelemetry.Record("slow", 20, backgroundThread: false);
			PerformanceTelemetry.Record("medium", 10, backgroundThread: false);

			PerformanceSample[] samples = PerformanceTelemetry.SlowestSamples(2);

			Assert.Equal(new[] { "slow", "medium" }, samples.Select((PerformanceSample sample) => sample.Name));
		}
	}
}
