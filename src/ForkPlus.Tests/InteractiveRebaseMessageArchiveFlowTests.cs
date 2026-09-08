// 回归测试（2026-09-04，"交互式变基 reword 消息丢失疑云"排查产物）：
//
// 背景：此前怀疑"变基窗口确认后 reword 的消息没有生效"。人工排查发现历史测试
// 受 Chromium 遮挡窗口干扰（输入打进浏览器地址栏），并非应用缺陷；但
// SaveMessageArchiveForTodoList（归档保存）→ TryApplyArchivedMessage（归档应用）
// 的端到端链路此前没有测试覆盖（Apply 侧已有 CommitMessageArchiveTests）。
//
// 本文件用生产代码真实类型（RevisionEntry/InteractiveRebaseTodoListItem）按
// UI 顺序（新→旧）构造 todo 列表，反射调用 InteractiveRebaseWindow 的
// SaveMessageArchiveForTodoList 写出归档，再用 CommitMessageArchive 应用，
// 断言 reword/squash 场景的消息逐环不丢。
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using Xunit;

namespace ForkPlus.Tests
{
	public class InteractiveRebaseMessageArchiveFlowTests : IDisposable
	{
		private readonly string _repoRoot;

		private readonly string _rebaseMergeDir;

		public InteractiveRebaseMessageArchiveFlowTests()
		{
			_repoRoot = Path.Combine(Path.GetTempPath(), "fp-irflow-" + Guid.NewGuid().ToString("N"));
			_rebaseMergeDir = Path.Combine(_repoRoot, ".git", "rebase-merge");
			Directory.CreateDirectory(_rebaseMergeDir);
		}

		public void Dispose()
		{
			try
			{
				Directory.Delete(_repoRoot, recursive: true);
			}
			catch (IOException)
			{
			}
		}

		private static RevisionEntry Entry(int row, string sha, string message, InteractiveRebaseAction action = InteractiveRebaseAction.Pick, string customMessage = null)
		{
			Sha? parsed = Sha.Parse(sha);
			Assert.True(parsed.HasValue, "测试 sha 必须可解析: " + sha);
			var item = new InteractiveRebaseTodoListItem(parsed.Value, action, new UserIdentity("Tester", "t@t.com"),
				DateTime.UnixEpoch, message, new LocalBranch[0]);
			var entry = new RevisionEntry(row, item);
			if (customMessage != null)
			{
				entry.CustomMessage = customMessage;
			}
			return entry;
		}

		/// <summary>生产路径反射调用 InteractiveRebaseWindow.SaveMessageArchiveForTodoList。</summary>
		private void InvokeSaveArchive(ObservableCollection<RevisionEntry> todoList, string todoListPath)
		{
			object window = FormatterServices.GetUninitializedObject(typeof(InteractiveRebaseWindow));
			FieldInfo todoField = typeof(InteractiveRebaseWindow).GetField("_todoList", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.NotNull(todoField);
			todoField.SetValue(window, todoList);
			MethodInfo save = typeof(InteractiveRebaseWindow).GetMethod("SaveMessageArchiveForTodoList", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.NotNull(save);
			save.Invoke(window, new object[] { todoListPath });
		}

		[Fact]
		public void RewordFlow_SaveArchiveThenApply_WritesEditedMessage()
		{
			string sha1 = new string('1', 40);
			string sha2 = new string('2', 40);
			string sha3 = new string('3', 40);
			// UI 顺序（新→旧）：commit 5 … commit 3(reword, 新消息) …
			var todoList = new ObservableCollection<RevisionEntry>
			{
				Entry(0, sha1, "commit 5"),
				Entry(1, sha2, "commit 4"),
				Entry(2, sha3, "commit 3", InteractiveRebaseAction.Reword, "commit 3 via ui"),
			};

			string todoPath = Path.Combine(_rebaseMergeDir, "git-rebase-todo");
			InvokeSaveArchive(todoList, todoPath);

			string archivePath = Path.Combine(_rebaseMergeDir, CommitMessageArchive.ArchiveFilename);
			Assert.True(File.Exists(archivePath), "窗口确认后必须写出归档文件");
			string archiveJson = File.ReadAllText(archivePath);
			Assert.Contains(sha3, archiveJson);
			Assert.Contains("commit 3 via ui", archiveJson);

			// git 处理到 reword 指令：done 最后一行 r <sha3>，COMMIT_EDITMSG 待改写。
			File.WriteAllText(Path.Combine(_rebaseMergeDir, "done"), "r " + sha3 + " commit 3\n");
			string messageFile = Path.Combine(_repoRoot, ".git", "COMMIT_EDITMSG");
			Directory.CreateDirectory(Path.GetDirectoryName(messageFile));
			File.WriteAllText(messageFile, "commit 3\n\n# ------------------------ >8 ------------------------\n");

			bool applied = CommitMessageArchive.TryApplyArchivedMessage(
				new GitModule(_repoRoot, Path.Combine(_repoRoot, ".git"), null, null), messageFile);

			Assert.True(applied, "reword 指令执行时必须能从归档取回用户编辑的消息");
			Assert.Equal("commit 3 via ui", File.ReadAllText(messageFile));
		}

		[Fact]
		public void SquashFlow_SaveArchiveThenApply_WritesGroupAnchorMessage()
		{
			string shaAnchor = new string('a', 40);
			string shaSquashed = new string('b', 40);
			// UI 顺序（新→旧）：squash 项在前，锚定提交在后。
			var todoList = new ObservableCollection<RevisionEntry>
			{
				Entry(0, shaSquashed, "squashed commit", InteractiveRebaseAction.Squash),
				Entry(1, shaAnchor, "anchor commit", InteractiveRebaseAction.Pick, "combined message"),
			};

			string todoPath = Path.Combine(_rebaseMergeDir, "git-rebase-todo");
			InvokeSaveArchive(todoList, todoPath);

			// squash 编辑消息时 done 最后一行是 squash 指令。
			File.WriteAllText(Path.Combine(_rebaseMergeDir, "done"), "s " + shaSquashed + " squashed commit\n");
			string messageFile = Path.Combine(_repoRoot, ".git", "COMMIT_EDITMSG");
			Directory.CreateDirectory(Path.GetDirectoryName(messageFile));
			File.WriteAllText(messageFile, "# This is a combination of 2 commits.\n");

			bool applied = CommitMessageArchive.TryApplyArchivedMessage(
				new GitModule(_repoRoot, Path.Combine(_repoRoot, ".git"), null, null), messageFile);

			Assert.True(applied, "squash 指令编辑消息时必须命中归档（squash 项与锚定项共用锚点消息）");
			Assert.Equal("combined message", File.ReadAllText(messageFile));
		}

		[Fact]
		public void MixedActions_DropEntriesAreNotArchived()
		{
			string shaKeep = new string('c', 40);
			string shaDrop = new string('d', 40);
			var todoList = new ObservableCollection<RevisionEntry>
			{
				Entry(0, shaDrop, "to be dropped", InteractiveRebaseAction.Drop),
				Entry(1, shaKeep, "keep me", InteractiveRebaseAction.Pick, "kept message"),
			};

			string todoPath = Path.Combine(_rebaseMergeDir, "git-rebase-todo");
			InvokeSaveArchive(todoList, todoPath);

			string archiveJson = File.ReadAllText(Path.Combine(_rebaseMergeDir, CommitMessageArchive.ArchiveFilename));
			Assert.DoesNotContain(shaDrop, archiveJson);
			Assert.Contains(shaKeep, archiveJson);
		}
	}
}
