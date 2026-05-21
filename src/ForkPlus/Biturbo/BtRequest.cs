using System;
using ForkPlus.Git.Commands;

namespace ForkPlus.Biturbo
{
	public static class BtRequest
	{
		public delegate BtResult ExecuteDelegate<TBtResult>(ref TBtResult outResult);

		public delegate GitCommandResult<TResult> MapResultDelegate<TBtResult, TResult>(ref TBtResult outResult);

		public delegate void ReleaseResultDelegate<TBtResult>(ref TBtResult outResult);

		public static GitCommandResult<TResult> Run<TBtResult, TResult>(Func<TBtResult> with, ExecuteDelegate<TBtResult> execute, MapResultDelegate<TBtResult, TResult> map, ReleaseResultDelegate<TBtResult> release)
		{
			TBtResult outResult = with();
			BtResult btResult = execute(ref outResult);
			if (btResult != 0)
			{
				return GitCommandResult<TResult>.Failure(btResult.ToGitCommandError());
			}
			GitCommandResult<TResult> result = map(ref outResult);
			release(ref outResult);
			return result;
		}
	}
}
