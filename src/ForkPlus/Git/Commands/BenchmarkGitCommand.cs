using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using ForkPlus.Biturbo;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.Git.Commands
{
	public class BenchmarkGitCommand
	{
		private class Context
		{
			private readonly JobMonitor _monitor;

			private readonly StringBuilder _sb;

			public string BenchmarkLog => _sb.ToString();

			public bool IsCanceled => _monitor.IsCanceled;

			public int Iterations { get; }

			public int TotalSteps { get; }

			public int CurrentStep { get; set; }

			[Null]
			public Action<BenchmarkResult> Callback { get; }

			public double? SystemLatency { get; set; }

			public double? Status { get; set; }

			public double? References { get; set; }

			public double? Revisions { get; set; }

			public double? RevisionsBt { get; set; }

			public double? Score { get; set; }

			public Context(JobMonitor monitor, int iterationsPerStep, int steps, [Null] Action<BenchmarkResult> callback)
			{
				Iterations = iterationsPerStep;
				TotalSteps = iterationsPerStep * steps;
				CurrentStep = 0;
				Callback = callback;
				_monitor = monitor;
				_sb = new StringBuilder();
			}

			public BenchmarkResult AsResult()
			{
				return new BenchmarkResult(TotalSteps, CurrentStep, SystemLatency, Status, References, Revisions, RevisionsBt, Score, BenchmarkLog);
			}

			public void WriteLine(string line)
			{
				_sb.AppendLine(line);
				_monitor.AppendOutputLine(line);
			}

			public void UpdateProgress(double progress, string message)
			{
				_monitor.Update(progress, message);
			}
		}

		public GitCommandResult<BenchmarkResult> Execute(GitModule gitModule, JobMonitor monitor, [Null] Action<BenchmarkResult> callback)
		{
			Context context = new Context(monitor, 7, 5, callback);
			GitCommandResult<double> gitCommandResult = Run("Measuring system latency", context, () => new GetHeadGitCommand().ExecuteOld(gitModule).ToGitCommandResult());
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<BenchmarkResult>.Failure(gitCommandResult.Error);
			}
			context.SystemLatency = gitCommandResult.Result;
			callback?.Invoke(context.AsResult());
			GitCommandResult<double> gitCommandResult2 = Run("Reading changed files", context, () => new GetChangedFilesGitCommand().Execute(gitModule).ToGitCommandResult());
			if (!gitCommandResult2.Succeeded)
			{
				return GitCommandResult<BenchmarkResult>.Failure(gitCommandResult2.Error);
			}
			context.Status = gitCommandResult2.Result;
			callback?.Invoke(context.AsResult());
			GitCommandResult<double> gitCommandResult3 = Run("Reading branches and tags", context, () => new GetReferencesGitCommand().ExecuteOld(gitModule).ToGitCommandResult());
			if (!gitCommandResult3.Succeeded)
			{
				return GitCommandResult<BenchmarkResult>.Failure(gitCommandResult3.Error);
			}
			context.References = gitCommandResult3.Result;
			callback?.Invoke(context.AsResult());
			GitCommandResult<double> gitCommandResult4 = Run("Reading commits", context, () => GetGitLog(gitModule, RevisionSortOrder.Date, reflog: false, monitor));
			if (!gitCommandResult4.Succeeded)
			{
				return GitCommandResult<BenchmarkResult>.Failure(gitCommandResult4.Error);
			}
			context.Revisions = gitCommandResult4.Result;
			callback?.Invoke(context.AsResult());
			GitCommandResult<GitConfig> gitCommandResult5 = new GetGitConfigGitCommand().Execute(gitModule);
			if (!gitCommandResult5.Succeeded)
			{
				return GitCommandResult<BenchmarkResult>.Failure(gitCommandResult5.Error);
			}
			GitConfig result = gitCommandResult5.Result;
			GitCommandResult<ReferenceStorage> gitCommandResult6 = new GetReferencesGitCommand().Execute(gitModule, result);
			if (!gitCommandResult6.Succeeded)
			{
				return GitCommandResult<BenchmarkResult>.Failure(gitCommandResult6.Error);
			}
			ReferenceStorage referenceStorage = gitCommandResult6.Result;
			CommitGraphCache commitGraphCache = new CommitGraphCache(gitModule);
			int minPagesCount = ForkPlusSettings.Default.MinPagesCount;
			Sha? headSha = referenceStorage.HeadSha;
			Sha[] requiredShas;
			if (headSha.HasValue)
			{
				Sha valueOrDefault = headSha.GetValueOrDefault();
				requiredShas = new Sha[1] { valueOrDefault };
			}
			else
			{
				requiredShas = new Sha[0];
			}
			GitCommandResult<double> gitCommandResult7 = Run("Reading commits bt", context, () => new GetRevisionStorageGitCommand().Execute(gitModule, referenceStorage, ForkPlusSettings.Default.RevisionSortOrder == RevisionSortOrder.Topo, reflog: false, 0, minPagesCount, requiredShas, DateTime.UtcNow.MillisecondsSince1970(), commitGraphCache, monitor).ToGitCommandResult());
			if (!gitCommandResult7.Succeeded)
			{
				return GitCommandResult<BenchmarkResult>.Failure(gitCommandResult7.Error);
			}
			context.RevisionsBt = gitCommandResult7.Result;
			callback?.Invoke(context.AsResult());
			double? num = context.SystemLatency + context.Status + context.References + context.Revisions;
			double? num2 = num / 5.0;
			context.WriteLine("");
			context.WriteLine(string.Format(Translate(" Total time: {0:0.000}s"), num));
			context.WriteLine(string.Format(Translate(" Avg test time: {0:0.000}s"), num2));
			context.Score = 10.0 / num2;
			context.WriteLine(string.Format(Translate(" Score = 10 / {0:0.000} = {1:0.0}"), num2, context.Score));
			context.WriteLine($"\n{context.SystemLatency:0.000}");
			context.WriteLine($"{context.Status:0.000}");
			context.WriteLine($"{context.References:0.000}");
			context.WriteLine($"{context.Revisions:0.000}");
			context.WriteLine($"{context.Score:0.0}");
			monitor.Success(string.Format(Translate("Score: {0:0.0}"), context.Score));
			return GitCommandResult<BenchmarkResult>.Success(context.AsResult());
		}

		private static GitCommandResult<double> Run(string title, Context ctx, Func<GitCommandResult> actionToBenchmark)
		{
			if (ctx.CurrentStep != 0)
			{
				ctx.WriteLine("");
			}
			ctx.WriteLine(Translate(title));
			List<double> list = new List<double>(ctx.Iterations);
			for (int i = 0; i < ctx.Iterations; i++)
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				GitCommandResult gitCommandResult = actionToBenchmark();
				long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
				if (ctx.IsCanceled)
				{
					return GitCommandResult<double>.Failure(new GitCommandError.Cancelled());
				}
				if (!gitCommandResult.Succeeded)
				{
					return GitCommandResult<double>.Failure(gitCommandResult.Error);
				}
				ctx.CurrentStep++;
				list.Add((double)elapsedMilliseconds / 1000.0);
				double num = 100.0 * (double)ctx.CurrentStep / (double)ctx.TotalSteps;
				ctx.UpdateProgress(num, string.Format(Translate("{0} {1:0.}%..."), Translate(title), num));
				ctx.Callback?.Invoke(ctx.AsResult());
			}
			double num2 = RemoveMin(list);
			double num3 = RemoveMax(list);
			string[] value = list.Map((double x) => $"{x:0.000}");
			ctx.WriteLine(string.Format(Translate("  Measurements (s): {0:0.000}*, {1}, {2:0.000}*"), num3, string.Join(", ", value), num2));
			double num4 = 0.0;
			for (int j = 0; j < list.Count; j++)
			{
				num4 += list[j];
			}
			double num5 = num4 / (double)list.Count;
			ctx.WriteLine(string.Format(Translate("  Avg: {0:0.000}s"), num5));
			return GitCommandResult<double>.Success(num5);
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

		private static double RemoveMin(List<double> items)
		{
			int index = 0;
			for (int i = 1; i < items.Count; i++)
			{
				if (items[index] > items[i])
				{
					index = i;
				}
			}
			double result = items[index];
			items.UnstableRemoveAt(index);
			return result;
		}

		private static double RemoveMax(List<double> items)
		{
			int index = 0;
			for (int i = 1; i < items.Count; i++)
			{
				if (items[index] < items[i])
				{
					index = i;
				}
			}
			double result = items[index];
			items.UnstableRemoveAt(index);
			return result;
		}

		private static GitCommandResult GetGitLog(GitModule gitModule, RevisionSortOrder sortOrder, bool reflog, JobMonitor cancellationToken)
		{
			GitCommand gitCommand = new GitCommand();
			gitCommand.Add("log");
			gitCommand.Add("HEAD");
			gitCommand.Add("--branches");
			gitCommand.Add("--remotes");
			gitCommand.Add("--tags");
			gitCommand.Add($"--max-count={ForkPlusSettings.Default.MaxCommitCount}");
			gitCommand.Add("--no-show-signature");
			if (reflog)
			{
				gitCommand.Add("--reflog");
			}
			if (sortOrder == RevisionSortOrder.Topo)
			{
				gitCommand.Add("--topo-order");
			}
			else
			{
				gitCommand.Add("--date-order");
			}
			gitCommand.Add("--pretty=format:%H%P");
			gitCommand.Add("--");
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
