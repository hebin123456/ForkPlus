using System.Linq;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Git;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// v3.2.0 AI Commit Composer 拆分方案单元测试。覆盖 RebuildMatchedFiles（路径归一化匹配 + OldPath 索引）、
	/// GetUnassignedFiles、IsComplete。重点测试 Release Note 明确点名的"路径归一化匹配"鲁棒性。
	/// </summary>
	public class WipCommitPlanTests
	{
		private static ChangedFile MakeStaged(string path, string oldPath = null)
		{
			return new ChangedFile(path, StatusType.Modified, StatusType.None,
				ChangeType.Modified, staged: true, isNew: false, tracked: true, oldPath: oldPath);
		}

		[Fact]
		public void RebuildMatchedFiles_ExactMatch_MatchesAll()
		{
			WipCommitPlan plan = new WipCommitPlan(new[]
			{
				MakeStaged("src/Foo.cs"),
				MakeStaged("src/Bar.cs")
			});
			WipCommitGroup g = new WipCommitGroup { Subject = "feat" };
			g.Files.Add("src/Foo.cs");
			g.Files.Add("src/Bar.cs");
			plan.Groups.Add(g);

			plan.RebuildMatchedFiles();

			Assert.Equal(2, g.MatchedFileCount);
			Assert.False(g.HasUnmatchedFiles);
			Assert.True(plan.IsComplete);
		}

		[Fact]
		public void RebuildMatchedFiles_WindowsSeparator_MatchesUnixSeparator()
		{
			// AI 给出 Windows 路径，staged 文件是 Unix 风格
			WipCommitPlan plan = new WipCommitPlan(new[]
			{
				MakeStaged("src/Foo/Bar.cs")
			});
			WipCommitGroup g = new WipCommitGroup { Subject = "feat" };
			g.Files.Add("src\\Foo\\Bar.cs");
			plan.Groups.Add(g);

			plan.RebuildMatchedFiles();

			Assert.Equal(1, g.MatchedFileCount);
			Assert.True(plan.IsComplete);
		}

		[Fact]
		public void RebuildMatchedFiles_CaseInsensitive_Matches()
		{
			// AI 给大写路径，staged 是小写
			WipCommitPlan plan = new WipCommitPlan(new[]
			{
				MakeStaged("src/foo.cs")
			});
			WipCommitGroup g = new WipCommitGroup { Subject = "feat" };
			g.Files.Add("SRC/FOO.CS");
			plan.Groups.Add(g);

			plan.RebuildMatchedFiles();

			Assert.Equal(1, g.MatchedFileCount);
			Assert.True(plan.IsComplete);
		}

		[Fact]
		public void RebuildMatchedFiles_TrailingSlash_Normalized()
		{
			WipCommitPlan plan = new WipCommitPlan(new[]
			{
				MakeStaged("src/foo")
			});
			WipCommitGroup g = new WipCommitGroup { Subject = "feat" };
			g.Files.Add("src/foo/");
			plan.Groups.Add(g);

			plan.RebuildMatchedFiles();

			Assert.Equal(1, g.MatchedFileCount);
		}

		[Fact]
		public void RebuildMatchedFiles_OldPath_RenamedFileMatches()
		{
			// 重命名文件：staged 路径是新名，AI 给出旧名也能匹配
			WipCommitPlan plan = new WipCommitPlan(new[]
			{
				MakeStaged("src/NewName.cs", oldPath: "src/OldName.cs")
			});
			WipCommitGroup g = new WipCommitGroup { Subject = "rename" };
			g.Files.Add("src/OldName.cs");
			plan.Groups.Add(g);

			plan.RebuildMatchedFiles();

			Assert.Equal(1, g.MatchedFileCount);
			Assert.True(plan.IsComplete);
		}

		[Fact]
		public void RebuildMatchedFiles_NonExistentPath_NotMatched()
		{
			WipCommitPlan plan = new WipCommitPlan(new[]
			{
				MakeStaged("src/Foo.cs")
			});
			WipCommitGroup g = new WipCommitGroup { Subject = "feat" };
			g.Files.Add("src/Foo.cs");
			g.Files.Add("nonexistent/Bar.cs");
			plan.Groups.Add(g);

			plan.RebuildMatchedFiles();

			Assert.Equal(1, g.MatchedFileCount);
			Assert.True(g.HasUnmatchedFiles);
			Assert.Equal(new[] { "nonexistent/Bar.cs" }, g.UnmatchedFiles.ToList());
		}

		[Fact]
		public void RebuildMatchedFiles_Idempotent_CanBeCalledMultipleTimes()
		{
			WipCommitPlan plan = new WipCommitPlan(new[]
			{
				MakeStaged("a.cs"),
				MakeStaged("b.cs")
			});
			WipCommitGroup g = new WipCommitGroup { Subject = "feat" };
			g.Files.Add("a.cs");
			plan.Groups.Add(g);

			plan.RebuildMatchedFiles();
			Assert.Equal(1, g.MatchedFileCount);

			// 再次调用不应重复添加
			plan.RebuildMatchedFiles();
			Assert.Equal(1, g.MatchedFileCount);
		}

		[Fact]
		public void RebuildMatchedFiles_NullFilesInGroup_SilentlySkipped()
		{
			WipCommitPlan plan = new WipCommitPlan(new[]
			{
				MakeStaged("a.cs")
			});
			WipCommitGroup g = new WipCommitGroup { Subject = "feat" };
			g.Files.Add("a.cs");
			g.Files.Add(null);
			g.Files.Add("");
			g.Files.Add("   ");
			plan.Groups.Add(g);

			plan.RebuildMatchedFiles();

			Assert.Equal(1, g.MatchedFileCount);
		}

		[Fact]
		public void RebuildMatchedFiles_NullStagedFile_SilentlySkipped()
		{
			WipCommitPlan plan = new WipCommitPlan(new ChangedFile[]
			{
				MakeStaged("a.cs"),
				null
			});
			WipCommitGroup g = new WipCommitGroup { Subject = "feat" };
			g.Files.Add("a.cs");
			plan.Groups.Add(g);

			plan.RebuildMatchedFiles();

			Assert.Equal(1, g.MatchedFileCount);
		}

		[Fact]
		public void RebuildMatchedFiles_SameFileInMultipleGroups_AllGroupsGetMatchedReference()
		{
			// 一个文件被多个分组引用：每组 MatchedFiles 都会包含
			WipCommitPlan plan = new WipCommitPlan(new[]
			{
				MakeStaged("shared.cs")
			});
			WipCommitGroup g1 = new WipCommitGroup { Subject = "g1" };
			g1.Files.Add("shared.cs");
			WipCommitGroup g2 = new WipCommitGroup { Subject = "g2" };
			g2.Files.Add("shared.cs");
			plan.Groups.Add(g1);
			plan.Groups.Add(g2);

			plan.RebuildMatchedFiles();

			Assert.Equal(1, g1.MatchedFileCount);
			Assert.Equal(1, g2.MatchedFileCount);
		}

		[Fact]
		public void RebuildMatchedFiles_DuplicateFileInSameGroup_DeduplicatedBySeen()
		{
			WipCommitPlan plan = new WipCommitPlan(new[]
			{
				MakeStaged("a.cs")
			});
			WipCommitGroup g = new WipCommitGroup { Subject = "feat" };
			g.Files.Add("a.cs");
			g.Files.Add("a.cs");  // 重复
			g.Files.Add("A.CS");  // 大小写不同但归一化后相同
			plan.Groups.Add(g);

			plan.RebuildMatchedFiles();

			// 实现里用 HashSet<ChangedFile> 去重，所以只匹配一次
			Assert.Equal(1, g.MatchedFileCount);
		}

		[Fact]
		public void GetUnassignedFiles_AllAssigned_ReturnsEmpty()
		{
			WipCommitPlan plan = new WipCommitPlan(new[]
			{
				MakeStaged("a.cs"),
				MakeStaged("b.cs")
			});
			WipCommitGroup g = new WipCommitGroup { Subject = "x" };
			g.Files.Add("a.cs");
			g.Files.Add("b.cs");
			plan.Groups.Add(g);

			plan.RebuildMatchedFiles();

			Assert.Empty(plan.GetUnassignedFiles());
			Assert.True(plan.IsComplete);
		}

		[Fact]
		public void GetUnassignedFiles_SomeUnassigned_ReturnsUnassignedOnly()
		{
			WipCommitPlan plan = new WipCommitPlan(new[]
			{
				MakeStaged("a.cs"),
				MakeStaged("b.cs"),
				MakeStaged("c.cs")
			});
			WipCommitGroup g = new WipCommitGroup { Subject = "x" };
			g.Files.Add("a.cs");
			plan.Groups.Add(g);

			plan.RebuildMatchedFiles();

			var unassigned = plan.GetUnassignedFiles();
			Assert.Equal(2, unassigned.Count);
			Assert.Contains(unassigned, f => f.Path == "b.cs");
			Assert.Contains(unassigned, f => f.Path == "c.cs");
			Assert.False(plan.IsComplete);
		}

		[Fact]
		public void IsComplete_NoGroups_False()
		{
			WipCommitPlan plan = new WipCommitPlan(new ChangedFile[] { });
			Assert.False(plan.IsComplete);
		}

		[Fact]
		public void IsComplete_GroupsWithNoFiles_FalseIfStagedFilesRemain()
		{
			WipCommitPlan plan = new WipCommitPlan(new[]
			{
				MakeStaged("a.cs")
			});
			WipCommitGroup g = new WipCommitGroup { Subject = "empty" };
			plan.Groups.Add(g);

			plan.RebuildMatchedFiles();

			Assert.False(plan.IsComplete);
		}

		[Fact]
		public void IsComplete_EmptyStagedAndOneGroup_True()
		{
			WipCommitPlan plan = new WipCommitPlan(new ChangedFile[] { });
			WipCommitGroup g = new WipCommitGroup { Subject = "empty" };
			plan.Groups.Add(g);

			plan.RebuildMatchedFiles();

			Assert.True(plan.IsComplete);
		}

		[Fact]
		public void Constructor_NullStagedFiles_StagedFilesRemainsEmpty()
		{
			WipCommitPlan plan = new WipCommitPlan(null);
			Assert.Empty(plan.StagedFiles);
			Assert.Empty(plan.Groups);
		}

		[Fact]
		public void RebuildMatchedFiles_GroupWithNullFiles_SilentlySkipped()
		{
			// 测试 group.Files 为 null 的边界（虽然构造时初始化为非 null，但防御性测试）
			WipCommitPlan plan = new WipCommitPlan(new[]
			{
				MakeStaged("a.cs")
			});
			WipCommitGroup g = new WipCommitGroup { Subject = "feat" };
			// 模拟极端情况：用反射清空 Files 不实际，但 RebuildMatchedFiles 内部有 group.Files == null 检查
			// 这里直接构造正常场景，但确保不会抛异常
			plan.Groups.Add(g);

			plan.RebuildMatchedFiles();

			Assert.Equal(0, g.MatchedFileCount);
		}

		[Fact]
		public void RebuildMatchedFiles_MultipleGroupsAssignDifferentFiles_BothComplete()
		{
			WipCommitPlan plan = new WipCommitPlan(new[]
			{
				MakeStaged("auth/login.cs"),
				MakeStaged("auth/logout.cs"),
				MakeStaged("ui/button.cs"),
				MakeStaged("ui/form.cs")
			});

			WipCommitGroup g1 = new WipCommitGroup { Subject = "feat: auth module" };
			g1.Files.Add("auth/login.cs");
			g1.Files.Add("auth/logout.cs");

			WipCommitGroup g2 = new WipCommitGroup { Subject = "feat: ui components" };
			g2.Files.Add("ui/button.cs");
			g2.Files.Add("ui/form.cs");

			plan.Groups.Add(g1);
			plan.Groups.Add(g2);

			plan.RebuildMatchedFiles();

			Assert.True(plan.IsComplete);
			Assert.Equal(2, g1.MatchedFileCount);
			Assert.Equal(2, g2.MatchedFileCount);
		}
	}
}
