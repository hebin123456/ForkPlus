namespace ForkPlus.Git.Commands
{
	public class BenchmarkResult
	{
		public int Steps { get; }

		public int CurrentStep { get; }

		public double? SystemLatency { get; }

		public double? Status { get; }

		public double? References { get; }

		public double? Revisions { get; }

		public double? RevisionsBt { get; }

		public double? Score { get; }

		public string BenchmarkLog { get; }

		public BenchmarkResult(int steps, int currentStep, double? systemLatency, double? status, double? references, double? revisions, double? revisionsBt, double? score, string benchmarkLog)
		{
			Steps = steps;
			CurrentStep = currentStep;
			SystemLatency = systemLatency;
			Status = status;
			References = references;
			Revisions = revisions;
			RevisionsBt = revisionsBt;
			Score = score;
			BenchmarkLog = benchmarkLog;
		}
	}
}
