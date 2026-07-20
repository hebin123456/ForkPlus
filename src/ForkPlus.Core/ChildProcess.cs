using System;
using ForkPlus.Biturbo;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;

namespace ForkPlus
{
	public static class ChildProcess
	{
		public delegate T MapData<T>(byte[] data);

		public static Result<(int, TStdout, TStderr), GitCommandError> Execute<TStdout, TStderr>(string path, [Null] string workingDirectory, string[] args, string[] env, [Null] byte[] stdin, MapData<TStdout> stdoutMapper, MapData<TStderr> stderrMapper)
		{
			BtSpawnWithOutputResult out_result = default(BtSpawnWithOutputResult);
			BtResult btResult = Bt.bt_spawn_with_output(path, workingDirectory, args, args.Length, env, env.Length, stdin, (stdin != null) ? stdin.Length : 0, ref out_result);
			if (btResult != 0)
			{
				GitCommandError gitCommandError = btResult.ToGitCommandError();
				Log.Warn("Bt git request failed:\n" + gitCommandError.FriendlyDescription);
				return Result<(int, TStdout, TStderr), GitCommandError>.Err(gitCommandError);
			}
			int status = out_result.status;
			byte[] data = out_result.stdout.GetData(out_result.stdout_len);
			byte[] data2 = out_result.stderr.GetData(out_result.stderr_len);
			TStdout item;
			TStderr item2;
			try
			{
				item = stdoutMapper(data);
				item2 = stderrMapper(data2);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to decode process output", ex);
				return Result<(int, TStdout, TStderr), GitCommandError>.Err(new GitCommandError.UnknownException(ex));
			}
			finally
			{
				Bt.bt_release_spawn_with_output_result(ref out_result);
			}
			return Result<(int, TStdout, TStderr), GitCommandError>.Ok((status, item, item2));
		}

		public static Result<int, ISpawnError> SpawnWithCallback(string path, [Null] string workingDirectory, string[] args, string[] env, [Null] byte[] stdin, Action<string> stdoutPipeHandler, Action<string> stderrPipeHandler, [Null] JobMonitor monitor)
		{
			using SpawnWithCallbackInner spawnWithCallbackInner = new SpawnWithCallbackInner(path, workingDirectory, args, env, stdin, stdoutPipeHandler, stderrPipeHandler, monitor);
			return spawnWithCallbackInner.Spawn();
		}
	}
}
