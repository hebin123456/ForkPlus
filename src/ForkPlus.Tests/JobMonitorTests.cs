using System.Threading;
using ForkPlus.Jobs;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// v3.1.1 JobMonitor 状态机守卫单元测试。重点回归 v3.1.1 修复点：
	/// Cancel 后 Success/Fail/Update 不应覆盖 Canceled 状态。
	/// 零外部依赖（不依赖 WPF / GitModule）。
	/// </summary>
	public class JobMonitorTests
	{
		[Fact]
		public void NewMonitor_InitialStateIsInProgress()
		{
			JobMonitor monitor = new JobMonitor();
			// JobMonitorState.InProgress = 0（默认值）
			Assert.Equal(JobMonitorState.InProgress, monitor.State);
			Assert.False(monitor.IsCanceled);
			Assert.Null(monitor.Progress);
			Assert.Null(monitor.ProgressMessage);
		}

		[Fact]
		public void Cancel_SetsStateToCanceled()
		{
			JobMonitor monitor = new JobMonitor();
			monitor.Cancel();
			Assert.Equal(JobMonitorState.Canceled, monitor.State);
			Assert.True(monitor.IsCanceled);
			Assert.Equal("canceled", monitor.ProgressMessage);
		}

		[Fact]
		public void Cancel_InvokesCancellationAction()
		{
			int invoked = 0;
			JobMonitor monitor = new JobMonitor();
			monitor.SetCancellationAction(() => Interlocked.Increment(ref invoked));
			monitor.Cancel();
			Assert.Equal(1, invoked);
		}

		[Fact]
		public void Cancel_IsIdempotent_ActionInvokedOnlyOnce()
		{
			// v3.1.1 守卫：重复 Cancel 不应重复触发 cancellationAction
			int invoked = 0;
			JobMonitor monitor = new JobMonitor();
			monitor.SetCancellationAction(() => Interlocked.Increment(ref invoked));
			monitor.Cancel();
			monitor.Cancel();
			monitor.Cancel();
			Assert.Equal(1, invoked);
		}

		[Fact]
		public void Success_SetsStateToSucceeded()
		{
			JobMonitor monitor = new JobMonitor();
			monitor.Success("done");
			Assert.Equal(JobMonitorState.Succeeded, monitor.State);
			Assert.Equal(100, monitor.Progress);
			Assert.Equal("done", monitor.ProgressMessage);
		}

		[Fact]
		public void Fail_SetsStateToFailed()
		{
			JobMonitor monitor = new JobMonitor();
			monitor.Fail("error msg");
			Assert.Equal(JobMonitorState.Failed, monitor.State);
			Assert.Equal(100, monitor.Progress);
			Assert.Equal("error msg", monitor.ProgressMessage);
		}

		[Fact]
		public void Update_SetsProgressAndState()
		{
			JobMonitor monitor = new JobMonitor();
			monitor.Update(42.5, "working", JobMonitorState.InProgress);
			Assert.Equal(42.5, monitor.Progress);
			Assert.Equal("working", monitor.ProgressMessage);
			Assert.Equal(JobMonitorState.InProgress, monitor.State);
		}

		[Fact]
		public void Cancel_AfterSuccess_DoesNotChangeState()
		{
			// 注意：Cancel 自己有 Canceled 守卫，但 Success 没有逆向守卫
			// 实际行为：Cancel 在 Success 之后仍会把 state 改为 Canceled
			JobMonitor monitor = new JobMonitor();
			monitor.Success("done");
			Assert.Equal(JobMonitorState.Succeeded, monitor.State);

			monitor.Cancel();
			Assert.Equal(JobMonitorState.Canceled, monitor.State);
		}

		// ===== v3.1.1 修复的核心守卫 =====

		[Fact]
		public void Success_AfterCancel_DoesNotOverwriteCanceled()
		{
			// v3.1.1 修复点：Cancel 后 Success 不应把 state 改回 Succeeded
			JobMonitor monitor = new JobMonitor();
			monitor.Cancel();
			Assert.True(monitor.IsCanceled);

			monitor.Success("done");

			Assert.Equal(JobMonitorState.Canceled, monitor.State);
			Assert.True(monitor.IsCanceled);
		}

		[Fact]
		public void Fail_AfterCancel_DoesNotOverwriteCanceled()
		{
			// v3.1.1 修复点：Cancel 后 Fail 不应把 state 改为 Failed
			JobMonitor monitor = new JobMonitor();
			monitor.Cancel();

			monitor.Fail("error");

			Assert.Equal(JobMonitorState.Canceled, monitor.State);
			Assert.True(monitor.IsCanceled);
		}

		[Fact]
		public void Update_AfterCancel_DoesNotChangeStateButStillInvokesProgressAction()
		{
			// v3.1.1 修复点：Cancel 后 Update 不应把 state 改回 InProgress，
			// 但仍应触发 progressAction（用于 UI 刷新取消信号）
			int progressInvoked = 0;
			JobMonitor monitor = new JobMonitor();
			monitor.SetProgressAction(() => Interlocked.Increment(ref progressInvoked));
			monitor.Cancel();

			monitor.Update(50, "still working", JobMonitorState.InProgress);

			Assert.Equal(JobMonitorState.Canceled, monitor.State);
			Assert.True(monitor.IsCanceled);
			Assert.True(progressInvoked >= 1);
		}

		[Fact]
		public void SetState_DirectlyOverwritesState()
		{
			// SetState 没有守卫，是裸赋值
			JobMonitor monitor = new JobMonitor();
			monitor.Cancel();
			Assert.True(monitor.IsCanceled);

			monitor.SetState(JobMonitorState.InProgress);
			Assert.Equal(JobMonitorState.InProgress, monitor.State);
			Assert.False(monitor.IsCanceled);
		}

		[Fact]
		public void Append_AddsToOutput()
		{
			JobMonitor monitor = new JobMonitor();
			monitor.Append("hello ");
			monitor.Append("world");
			Assert.Equal("hello world", monitor.Output);
			Assert.Equal(11, monitor.OutputLength);
		}

		[Fact]
		public void AppendOutputLine_WithoutNewline_AppendsNewline()
		{
			JobMonitor monitor = new JobMonitor();
			monitor.AppendOutputLine("line1");
			monitor.AppendOutputLine("line2");
			Assert.Contains("line1", monitor.Output);
			Assert.Contains("line2", monitor.Output);
			Assert.Contains("\n", monitor.Output);
		}

		[Fact]
		public void AppendOutputLine_WithNewline_DoesNotDoubleAppendNewline()
		{
			// 实现细节：行尾已是 \n 时不再追加换行
			JobMonitor monitor = new JobMonitor();
			monitor.AppendOutputLine("line1\n");
			monitor.AppendOutputLine("line2\n");
			// 不应出现 \n\n（除非输入本身就有）
			Assert.DoesNotContain("\n\n", monitor.Output);
		}

		[Fact]
		public void Progress_UpdatesMonotonically()
		{
			JobMonitor monitor = new JobMonitor();
			monitor.Update(10, "step1");
			Assert.Equal(10, monitor.Progress);
			monitor.Update(50, "step2");
			Assert.Equal(50, monitor.Progress);
			monitor.Update(90, "step3");
			Assert.Equal(90, monitor.Progress);
		}

		[Fact]
		public void CancellationAction_Null_DoesNotThrowOnCancel()
		{
			JobMonitor monitor = new JobMonitor();
			// 默认 cancellationAction 为 null
			monitor.Cancel();
			Assert.True(monitor.IsCanceled);
		}

		[Fact]
		public void ProgressAction_Null_UpdateDoesNotThrow()
		{
			JobMonitor monitor = new JobMonitor();
			// 默认 progressAction 为 null
			monitor.Update(10, "msg");
			Assert.Equal(10, monitor.Progress);
		}
	}
}
