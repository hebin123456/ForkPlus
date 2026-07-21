using System;
using ForkPlus.Git;

namespace ForkPlus.Biturbo
{
	public class CommitGraphCache : IDisposable
	{
		public readonly BtCommitGraphCache Handle;

		private bool _disposed;

		private readonly GitModule _gitModule;

		public CommitGraphCache(GitModule gitModule)
		{
			_gitModule = gitModule;
			string path = gitModule.Path;
			Handle = Bt.bt_new_commit_graph_cache(path);
		}

		public void Dispose()
		{
			if (!_disposed)
			{
				BtCommitGraphCache commit_graph_cache_ptr = Handle;
				Bt.bt_release_commit_graph_cache(ref commit_graph_cache_ptr);
				_disposed = true;
			}
		}

		~CommitGraphCache()
		{
			Dispose();
		}
	}
}
