using System;

namespace ForkPlus.Git.Commands
{
	public class GitCommandResult
	{
		private readonly Result<bool, GitCommandError> _result;

		public bool Succeeded => _result.IsOk;

		public GitCommandError Error => _result.Error;

		private GitCommandResult(Result<bool, GitCommandError> result)
		{
			_result = result;
		}

		public static GitCommandResult Success()
		{
			return new GitCommandResult(Result<bool, GitCommandError>.Ok(val: true));
		}

		public static GitCommandResult Failure(GitCommandError error)
		{
			return new GitCommandResult(Result<bool, GitCommandError>.Err(error));
		}

		public static GitCommandResult Failure(Exception ex)
		{
			return new GitCommandResult(Result<bool, GitCommandError>.Err(new GitCommandError.UnknownException(ex)));
		}
	}
	public sealed class GitCommandResult<T>
	{
		private readonly Result<T, GitCommandError> _result;

		public bool Succeeded => _result.IsOk;

		public GitCommandError Error => _result.Error;

		public T Result => _result.Value;

		private GitCommandResult(Result<T, GitCommandError> result)
		{
			_result = result;
		}

		public static GitCommandResult<T> Success(T result)
		{
			return new GitCommandResult<T>(Result<T, GitCommandError>.Ok(result));
		}

		public static GitCommandResult<T> Failure(GitCommandError error)
		{
			return new GitCommandResult<T>(Result<T, GitCommandError>.Err(error));
		}

		public static GitCommandResult<T> Failure(Exception ex)
		{
			return new GitCommandResult<T>(Result<T, GitCommandError>.Err(new GitCommandError.UnknownException(ex)));
		}

		public GitCommandResult ToGitCommandResult()
		{
			if (Succeeded)
			{
				return GitCommandResult.Success();
			}
			return GitCommandResult.Failure(Error);
		}

		public GitCommandResult<TDst> Map<TDst>(Func<T, TDst> transform)
		{
			if (Succeeded)
			{
				return GitCommandResult<TDst>.Success(transform(Result));
			}
			return GitCommandResult<TDst>.Failure(Error);
		}
	}
}
