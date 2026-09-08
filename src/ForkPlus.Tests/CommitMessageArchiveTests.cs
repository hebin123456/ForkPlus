// 回归测试（2026-09-04，"交互式变基确定/取消都弹 there was a problem with the editor"修复产物）：
//
// 根因（两个）：
//  1. 取消路径：用户点取消 → RI.exe 退出码 1 → git 中止并输出
//     "error: there was a problem with the editor '<RI.exe>'"（git 2.x 小写 there）。
//     ShowInteractiveRebaseWindowCommand.IsCanceled 只匹配旧版 "Could not execute
//     editor" → 误判为失败 → 弹出错误窗口。
//  2. reword/squash 路径：git 处理 reword/squash 指令时用 core.editor（同为 RI.exe）
//     编辑 COMMIT_EDITMSG；RI.exe 统一转发路径，主进程把它当 todo 列表解析成空列表
//     并挂起等待（无人应答）→ git 卡死至超时报同样的 editor 错误。
//
// 修复：IsCanceled 忽略大小写匹配 "problem with the editor"；IpcMessageHandler 区分
// todo 文件与提交消息文件（后者用 CommitMessageArchive 归档消息直接应答）。
// 本文件覆盖：CommitMessageArchive 的归档应用逻辑 + IsCanceled 的错误文本匹配。
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using Newtonsoft.Json;
using Xunit;

namespace ForkPlus.Tests
{
	public class CommitMessageArchiveTests : IDisposable
	{
		private readonly string _repoRoot;

		private readonly string _rebaseMergeDir;

		public CommitMessageArchiveTests()
		{
			_repoRoot = Path.Combine(Path.GetTempPath(), "fp-cma-" + Guid.NewGuid().ToString("N"));
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

		private GitModule CreateModule()
		{
			return new GitModule(_repoRoot, Path.Combine(_repoRoot, ".git"), null, null);
		}

		private static string Sha(int seed)
		{
			// 确定性 40 位小写 hex（Sha.TryParse/ToString 均为小写）。
			Random random = new Random(seed);
			char[] chars = new char[40];
			for (int i = 0; i < chars.Length; i++)
			{
				chars[i] = "0123456789abcdef"[random.Next(16)];
			}
			return new string(chars);
		}

		private string WriteCommitEditMsg(string content)
		{
			string path = Path.Combine(_repoRoot, ".git", "COMMIT_EDITMSG");
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllText(path, content);
			return path;
		}

		[Fact]
		public void TryApplyArchivedMessage_RewordTodoLine_WritesArchivedMessage()
		{
			// 窗口确认时覆写 git-rebase-todo 的行格式：`r <sha> <subject>`（reword 指令
			// 已执行并追加到 done）。归档 key = 被 reword 提交的 sha。
			string sha = Sha(1);
			File.WriteAllText(Path.Combine(_rebaseMergeDir, "done"), "p " + Sha(2) + " older\nr " + sha + " new subject\n");
			File.WriteAllText(Path.Combine(_rebaseMergeDir, CommitMessageArchive.ArchiveFilename),
				JsonConvert.SerializeObject(new Dictionary<string, string> { { sha, "new subject\n\nnew body" } }));
			string messageFile = WriteCommitEditMsg("old subject\n\n# comment lines\n");

			bool applied = CommitMessageArchive.TryApplyArchivedMessage(CreateModule(), messageFile);

			Assert.True(applied, "归档中有当前 reword 提交的消息，必须应用");
			Assert.Equal("new subject\n\nnew body", File.ReadAllText(messageFile));
		}

		[Fact]
		public void TryApplyArchivedMessage_SquashTodoLineAfterUpdateRef_SkipsNonShaLines()
		{
			// squash 编辑消息时 done 最后一行是 squash 指令；update-ref 指令行
			// （`u <ref>`）第二段不是 sha，必须跳过继续向上找。
			string sha = Sha(3);
			File.WriteAllText(Path.Combine(_rebaseMergeDir, "done"),
				"s " + sha + " squashed\nu refs/heads/topic\n");
			File.WriteAllText(Path.Combine(_rebaseMergeDir, CommitMessageArchive.ArchiveFilename),
				JsonConvert.SerializeObject(new Dictionary<string, string> { { sha, "combined message" } }));
			string messageFile = WriteCommitEditMsg("# This is a combination of 2 commits.\n");

			bool applied = CommitMessageArchive.TryApplyArchivedMessage(CreateModule(), messageFile);

			Assert.True(applied, "done 最后的 update-ref 行应被跳过，命中前面的 squash 指令 sha");
			Assert.Equal("combined message", File.ReadAllText(messageFile));
		}

		[Theory]
		[InlineData("p {0} subject")]          // 窗口覆写格式（缩写指令 + 全 sha + subject）
		[InlineData("pick {0} # subject")]      // git 默认 todo 格式
		[InlineData("reword {0} # subject")]    // git 默认 todo 格式（全指令名）
		public void TryApplyArchivedMessage_VariousDoneLineFormats_ExtractsSha(string lineFormat)
		{
			string sha = Sha(4);
			File.WriteAllText(Path.Combine(_rebaseMergeDir, "done"), string.Format(lineFormat, sha) + "\n");
			File.WriteAllText(Path.Combine(_rebaseMergeDir, CommitMessageArchive.ArchiveFilename),
				JsonConvert.SerializeObject(new Dictionary<string, string> { { sha, "archived" } }));
			string messageFile = WriteCommitEditMsg("original\n");

			bool applied = CommitMessageArchive.TryApplyArchivedMessage(CreateModule(), messageFile);

			Assert.True(applied, "done 行格式 '" + lineFormat + "' 必须能提取 sha");
			Assert.Equal("archived", File.ReadAllText(messageFile));
		}

		[Fact]
		public void TryApplyArchivedMessage_InstructionFormatToken_IsStrippedFromSha()
		{
			// rebase.instructionFormat=#!_%H 生成的行：`p #!_<sha>`（git 原生首次生成的
			// todo 被直接执行的场景，如 --autosquash 插入的行未被窗口覆写）。
			string sha = Sha(5);
			File.WriteAllText(Path.Combine(_rebaseMergeDir, "done"), "p #!_" + sha + "\n");
			File.WriteAllText(Path.Combine(_rebaseMergeDir, CommitMessageArchive.ArchiveFilename),
				JsonConvert.SerializeObject(new Dictionary<string, string> { { sha, "from-instruction-format" } }));
			string messageFile = WriteCommitEditMsg("original\n");

			bool applied = CommitMessageArchive.TryApplyArchivedMessage(CreateModule(), messageFile);

			Assert.True(applied, "#!_ 前缀必须被剥离后再解析 sha");
			Assert.Equal("from-instruction-format", File.ReadAllText(messageFile));
		}

		[Fact]
		public void TryApplyArchivedMessage_NoArchiveFile_ReturnsFalseAndKeepsMessage()
		{
			string sha = Sha(6);
			File.WriteAllText(Path.Combine(_rebaseMergeDir, "done"), "r " + sha + " subject\n");
			string messageFile = WriteCommitEditMsg("original message\n");

			bool applied = CommitMessageArchive.TryApplyArchivedMessage(CreateModule(), messageFile);

			Assert.False(applied, "没有归档文件时必须返回 false（调用方照常放行 git）");
			Assert.Equal("original message\n", File.ReadAllText(messageFile));
		}

		[Fact]
		public void TryApplyArchivedMessage_ShaNotInArchive_ReturnsFalse()
		{
			File.WriteAllText(Path.Combine(_rebaseMergeDir, "done"), "r " + Sha(7) + " subject\n");
			File.WriteAllText(Path.Combine(_rebaseMergeDir, CommitMessageArchive.ArchiveFilename),
				JsonConvert.SerializeObject(new Dictionary<string, string> { { Sha(8), "other" } }));
			string messageFile = WriteCommitEditMsg("original message\n");

			bool applied = CommitMessageArchive.TryApplyArchivedMessage(CreateModule(), messageFile);

			Assert.False(applied, "归档中没有当前提交的消息时必须返回 false");
		}

		[Fact]
		public void TryApplyArchivedMessage_MissingDoneFile_ReturnsFalse()
		{
			string messageFile = WriteCommitEditMsg("original message\n");

			bool applied = CommitMessageArchive.TryApplyArchivedMessage(CreateModule(), messageFile);

			Assert.False(applied, "没有 done 文件（非变基中）时必须返回 false");
		}

		[Fact]
		public void TryApplyArchivedMessage_MissingMessageFile_ReturnsFalse()
		{
			string sha = Sha(9);
			File.WriteAllText(Path.Combine(_rebaseMergeDir, "done"), "r " + sha + " subject\n");
			File.WriteAllText(Path.Combine(_rebaseMergeDir, CommitMessageArchive.ArchiveFilename),
				JsonConvert.SerializeObject(new Dictionary<string, string> { { sha, "msg" } }));

			bool applied = CommitMessageArchive.TryApplyArchivedMessage(CreateModule(), Path.Combine(_repoRoot, ".git", "COMMIT_EDITMSG"));

			Assert.False(applied, "消息文件不存在时必须返回 false 且不抛异常");
		}

		[Theory]
		[InlineData("error: there was a problem with the editor 'D:/xxx/ForkPlus.RI.exe'", true)]   // git 2.x（小写 there，真机实测文本）
		[InlineData("error: There was a problem with the editor 'ForkPlus.RI.exe'.", true)]        // 大小写变体
		[InlineData("error: Could not execute editor", true)]                                       // 旧版 git
		[InlineData("error: failed to push some refs", false)]                                      // 普通错误
		[InlineData("", false)]
		public void IsCanceled_MatchesEditorCancelErrors(string stderr, bool expected)
		{
			// ShowInteractiveRebaseWindowCommand.IsCanceled 为 private static，反射调用
			// （与生产路径完全一致的判别逻辑）。
			MethodInfo method = typeof(ForkPlus.UI.Commands.ShowInteractiveRebaseWindowCommand)
				.GetMethod("IsCanceled", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.NotNull(method);
			bool actual = (bool)method.Invoke(null, new object[] { new GitCommandError.GitError(stderr) });
			Assert.Equal(expected, actual);
		}

		[Fact]
		public void IsCanceled_NullError_ReturnsFalse()
		{
			MethodInfo method = typeof(ForkPlus.UI.Commands.ShowInteractiveRebaseWindowCommand)
				.GetMethod("IsCanceled", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.NotNull(method);
			bool actual = (bool)method.Invoke(null, new object[] { null });
			Assert.False(actual);
		}
	}
}
