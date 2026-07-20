using System;
using System.Collections.Generic;
using System.IO;
using ForkPlus.Git;
using ForkPlus.Undo;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// v3.3.0 UndoIndexStore 单元测试。
	///
	/// 测试 .git/forkplus-undo-index.json 的读写、原子写入、容量淘汰、文件损坏恢复。
	///
	/// 测试策略：
	/// - 用临时目录 + 构造一个真实的 GitModule 指向它（GitModule.GitDir() 返回该临时目录）
	/// - 每个测试独立创建临时目录，测试结束清理
	/// - 不依赖真实 git 进程，只测文件 IO 和序列化
	/// </summary>
	public class UndoIndexStoreTests : IDisposable
	{
		private readonly string _tempDir;

		public UndoIndexStoreTests()
		{
			_tempDir = Path.Combine(Path.GetTempPath(), "ForkPlus-UndoIndexStoreTests-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_tempDir);
		}

		public void Dispose()
		{
			try
			{
				if (Directory.Exists(_tempDir))
				{
					Directory.Delete(_tempDir, recursive: true);
				}
			}
			catch
			{
				// 测试清理失败不阻断
			}
		}

		/// <summary>创建指向 _tempDir 的 GitModule。GitDir() 返回 _tempDir。</summary>
		private GitModule CreateGitModule()
		{
			return new GitModule(_tempDir, _tempDir, null, null);
		}

		private static UndoIndexEntry MakeEntry(string sha, string opName, DateTime? ts = null, string opType = null)
		{
			return new UndoIndexEntry(sha, opName, ts ?? DateTime.UtcNow, opType);
		}

		[Fact]
		public void GetIndexPath_NullGitModule_ReturnsNull()
		{
			UndoIndexStore store = new UndoIndexStore(null);
			Assert.Null(store.GetIndexPath());
		}

		[Fact]
		public void GetIndexPath_ValidGitModule_ReturnsPathUnderGitDir()
		{
			GitModule module = CreateGitModule();
			UndoIndexStore store = new UndoIndexStore(module);

			string path = store.GetIndexPath();
			Assert.NotNull(path);
			Assert.Equal(Path.Combine(_tempDir, UndoIndexStore.IndexFileName), path);
		}

		[Fact]
		public void Load_FileDoesNotExist_ReturnsEmptyDict()
		{
			UndoIndexStore store = new UndoIndexStore(CreateGitModule());
			Dictionary<string, UndoIndexEntry> dict = store.Load();

			Assert.NotNull(dict);
			Assert.Empty(dict);
		}

		[Fact]
		public void Lookup_BeforeRecord_ReturnsNull()
		{
			UndoIndexStore store = new UndoIndexStore(CreateGitModule());
			Assert.Null(store.Lookup("anySha"));
		}

		[Fact]
		public void Lookup_NullOrEmptySha_ReturnsNull()
		{
			UndoIndexStore store = new UndoIndexStore(CreateGitModule());
			Assert.Null(store.Lookup(null));
			Assert.Null(store.Lookup(""));
		}

		[Fact]
		public void Record_NullEntry_SilentlySkipped()
		{
			UndoIndexStore store = new UndoIndexStore(CreateGitModule());
			store.Record(null);
			Assert.Empty(store.Load());
		}

		[Fact]
		public void Record_EmptyHeadSha_SilentlySkipped()
		{
			UndoIndexStore store = new UndoIndexStore(CreateGitModule());
			store.Record(MakeEntry("", "op1"));
			store.Record(MakeEntry(null, "op2"));
			Assert.Empty(store.Load());
		}

		[Fact]
		public void Record_ThenLookup_ReturnsEntry()
		{
			UndoIndexStore store = new UndoIndexStore(CreateGitModule());
			DateTime ts = new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc);
			store.Record(MakeEntry("sha1", "Commit 'fix bug'", ts, "Commit"));

			UndoIndexEntry found = store.Lookup("sha1");
			Assert.NotNull(found);
			Assert.Equal("sha1", found.HeadSha);
			Assert.Equal("Commit 'fix bug'", found.OperationName);
			Assert.Equal("Commit", found.OperationType);
			Assert.Equal(ts, found.TimestampUtc);
		}

		[Fact]
		public void Record_SameHeadShaTwice_OverwritesExistingEntry()
		{
			UndoIndexStore store = new UndoIndexStore(CreateGitModule());
			store.Record(MakeEntry("sha1", "old op"));
			store.Record(MakeEntry("sha1", "new op"));

			Dictionary<string, UndoIndexEntry> dict = store.Load();
		Assert.Single(dict);
		Assert.Equal("new op", dict["sha1"].OperationName);
		}

		[Fact]
		public void Record_MultipleDistinctShas_AllPersisted()
		{
			UndoIndexStore store = new UndoIndexStore(CreateGitModule());
			store.Record(MakeEntry("sha1", "op1"));
			store.Record(MakeEntry("sha2", "op2"));
			store.Record(MakeEntry("sha3", "op3"));

			Dictionary<string, UndoIndexEntry> dict = store.Load();
			Assert.Equal(3, dict.Count);
			Assert.Equal("op1", dict["sha1"].OperationName);
			Assert.Equal("op2", dict["sha2"].OperationName);
			Assert.Equal("op3", dict["sha3"].OperationName);
		}

		/// <summary>新建一个 store 指向同一个目录，模拟"重启 ForkPlus 后再次加载"。</summary>
		[Fact]
		public void Load_AcrossStoreInstances_ReadsPersistedFile()
		{
			UndoIndexStore store1 = new UndoIndexStore(CreateGitModule());
			store1.Record(MakeEntry("sha1", "Commit 'feature A'"));

			// 模拟"软件重启"：用全新的 store 实例从磁盘读
			UndoIndexStore store2 = new UndoIndexStore(CreateGitModule());
			UndoIndexEntry found = store2.Lookup("sha1");

			Assert.NotNull(found);
			Assert.Equal("Commit 'feature A'", found.OperationName);
		}

		[Fact]
		public void Record_CreatesIndexFileOnDisk()
		{
			UndoIndexStore store = new UndoIndexStore(CreateGitModule());
			string path = store.GetIndexPath();
			Assert.False(File.Exists(path), "test precondition: file should not exist yet");

			store.Record(MakeEntry("sha1", "op1"));

			Assert.True(File.Exists(path), "Record should create the index file");
		}

		[Fact]
		public void Load_CorruptFile_ReturnsEmptyAndDeletesFile()
		{
			// 写入一个损坏的 JSON 文件
			UndoIndexStore setupStore = new UndoIndexStore(CreateGitModule());
			string path = setupStore.GetIndexPath();
			File.WriteAllText(path, "{ this is not valid json ][");

			// 新 store 加载时应静默处理
			UndoIndexStore store = new UndoIndexStore(CreateGitModule());
			Dictionary<string, UndoIndexEntry> dict = store.Load();

			Assert.Empty(dict);
			// 文件应被删除（清理重建）
			Assert.False(File.Exists(path), "corrupt file should be deleted");
		}

		[Fact]
		public void Load_EmptyFile_ReturnsEmpty()
		{
			UndoIndexStore setupStore = new UndoIndexStore(CreateGitModule());
			string path = setupStore.GetIndexPath();
			File.WriteAllText(path, "");

			UndoIndexStore store = new UndoIndexStore(CreateGitModule());
			Dictionary<string, UndoIndexEntry> dict = store.Load();

			Assert.Empty(dict);
		}

		[Fact]
		public void Load_WhitespaceOnlyFile_ReturnsEmpty()
		{
			UndoIndexStore setupStore = new UndoIndexStore(CreateGitModule());
			string path = setupStore.GetIndexPath();
			File.WriteAllText(path, "   \n  \t  ");

			UndoIndexStore store = new UndoIndexStore(CreateGitModule());
			Dictionary<string, UndoIndexEntry> dict = store.Load();

			Assert.Empty(dict);
		}

		[Fact]
		public void Record_BeyondCapacity_OldestEntriesEvicted()
		{
			// 用小容量方便测试 LRU 淘汰
			UndoIndexStore store = new UndoIndexStore(CreateGitModule(), capacity: 3);
			DateTime baseTs = new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc);

			store.Record(MakeEntry("sha1", "op1", baseTs));
			store.Record(MakeEntry("sha2", "op2", baseTs.AddSeconds(1)));
			store.Record(MakeEntry("sha3", "op3", baseTs.AddSeconds(2)));
			store.Record(MakeEntry("sha4", "op4", baseTs.AddSeconds(3)));

			// 容量 3，sha1（最早）应被淘汰
			Dictionary<string, UndoIndexEntry> dict = store.Load();
			Assert.Equal(3, dict.Count);
			Assert.False(dict.ContainsKey("sha1"), "oldest entry should be evicted");
			Assert.True(dict.ContainsKey("sha2"));
			Assert.True(dict.ContainsKey("sha3"));
			Assert.True(dict.ContainsKey("sha4"));
		}

		[Fact]
		public void Record_AtCapacityLimit_NoEviction()
		{
			UndoIndexStore store = new UndoIndexStore(CreateGitModule(), capacity: 3);
			DateTime baseTs = new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc);

			store.Record(MakeEntry("sha1", "op1", baseTs));
			store.Record(MakeEntry("sha2", "op2", baseTs.AddSeconds(1)));
			store.Record(MakeEntry("sha3", "op3", baseTs.AddSeconds(2)));

			Dictionary<string, UndoIndexEntry> dict = store.Load();
			Assert.Equal(3, dict.Count);
			Assert.True(dict.ContainsKey("sha1"));
		}

		[Fact]
		public void Record_NullOperationName_NormalizedToEmpty()
		{
			UndoIndexStore store = new UndoIndexStore(CreateGitModule());
			store.Record(MakeEntry("sha1", null));

			UndoIndexEntry found = store.Lookup("sha1");
			Assert.NotNull(found);
			Assert.Equal("", found.OperationName);
		}

		[Fact]
		public void Lookup_UnknownSha_ReturnsNull()
		{
			UndoIndexStore store = new UndoIndexStore(CreateGitModule());
			store.Record(MakeEntry("sha1", "op1"));

			Assert.Null(store.Lookup("unknownSha"));
		}

		[Fact]
		public void Record_HugeOperationName_PersistedIntact()
		{
			// 验证大文本不会被截断（OperationName 可能含较长的 commit subject）
			string longName = "Commit '" + new string('x', 1000) + "'";
			UndoIndexStore store = new UndoIndexStore(CreateGitModule());
			store.Record(MakeEntry("sha1", longName));

			UndoIndexEntry found = store.Lookup("sha1");
			Assert.NotNull(found);
			Assert.Equal(longName, found.OperationName);
		}

		[Fact]
		public void Record_SpecialCharactersInOperationName_PersistedIntact()
		{
			// 验证 JSON 序列化对特殊字符（引号、换行、Unicode）的处理
			string special = "Commit \"fix: 重要 bug\n第二行\" / 'foo' \\ \t tab";
			UndoIndexStore store = new UndoIndexStore(CreateGitModule());
			store.Record(MakeEntry("sha1", special));

			UndoIndexEntry found = store.Lookup("sha1");
			Assert.NotNull(found);
			Assert.Equal(special, found.OperationName);
		}

		[Fact]
		public void Record_AtomicallyReplaces_NoTmpFileLeft()
		{
			UndoIndexStore store = new UndoIndexStore(CreateGitModule());
			string path = store.GetIndexPath();

			store.Record(MakeEntry("sha1", "op1"));
			store.Record(MakeEntry("sha2", "op2"));

			// 原子写入应不留 .tmp 文件
			Assert.False(File.Exists(path + ".tmp"), "no .tmp file should be left after atomic write");
			Assert.True(File.Exists(path));
		}
	}
}
