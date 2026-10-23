using System.Diagnostics;
using System.IO;

namespace ForkPlus.Git
{
	[DebuggerDisplay("{RepositoryName}")]
	public class GitModule
	{
		private RepositorySettings _settings;

		public string Path { get; }

		public string CommonGitDir { get; }

		[Null]
		public string WorktreeGitDir { get; }

		[Null]
		public string ParentRepoPath { get; }

		public string RepositoryName { get; }

		[Null]
		public string ParentRepositoryName { get; }

		public ModuleType Type { get; }

		public RepositorySettings Settings
		{
			get
			{
				if (_settings == null)
				{
					_settings = RepositorySettings.Load(this);
				}
				return _settings;
			}
		}

		public string GitModulesFilePath => this.MakePath(".gitmodules");

		public GitModule(string path, string commonGitDir, [Null] string worktreeGitDir, [Null] string parentRepoPath)
		{
			Path = path;
			CommonGitDir = commonGitDir;
			WorktreeGitDir = worktreeGitDir;
			ParentRepoPath = parentRepoPath;
			RepositoryName = System.IO.Path.GetFileName(path);
			if (ParentRepoPath != null)
			{
				ParentRepositoryName = System.IO.Path.GetFileName(ParentRepoPath);
			}
			if (parentRepoPath != null)
			{
				Type = ModuleType.Submodule;
			}
			else if (worktreeGitDir != null)
			{
				Type = ModuleType.Worktree;
			}
			else
			{
				Type = ModuleType.Repository;
			}
		}

		public string GitDir()
		{
			return WorktreeGitDir ?? CommonGitDir;
		}

		/// <summary>供 native biturbo 枚举 refs 用的 git 目录。
		/// bt_get_references 手工扫 &lt;gitdir&gt;/refs + packed-refs，不解析 commondir——链接 worktree 的
		/// gitdir 里没有 refs/（全部在 common dir），只能扫出 ORIG_HEAD 等伪 ref，导致分支列表为空、
		/// ActiveBranchIndex 恒 null（UI 误显示 "detached HEAD"）。worktree 改用 common dir 枚举；
		/// 其返回的 HEAD symref 是主 worktree 的 HEAD，随后由
		/// BtReferencesExtensions.ResolveRepositoryHead 按本 worktree 自身的 HEAD 修正。
		/// 其余 native 调用（bt_get_commits / bt_get_committer_times / bt_get_behind_ahead_counts）
		/// 走 libgit2 语义、正确解析 commondir，仍用 <see cref="GitDir"/>。</summary>
		public string ReferencesGitDir()
		{
			if (Type == ModuleType.Worktree)
			{
				return CommonGitDir;
			}
			return GitDir();
		}

		/// <summary>检测 HEAD 是否 unborn（指向不存在分支的 symref，即"没有任何 commit"状态）。
		/// 纯文件检查，不 spawn git 子进程——替代旧版 git rev-parse --verify HEAD 检测
		/// （v2.1.4 引入）。在进程创建被终端安全软件拦截的机器上，一次 git spawn 实测
		/// ~1-2.4s，而该检测位于打开仓库的首屏关键路径（RefreshRepositoryReferences /
		/// GetRepositoryState 各一次），是"轨道图刷出来慢"的主因（2026-10 性能审计：
		/// 3.3s 数据管线里 rev-parse 占 2.4s）。
		/// 判定规则（与 rev-parse --verify HEAD 语义一致）：
		///   HEAD = "ref: refs/heads/X" 且 X 不存在（loose ref + packed-refs 均无）→ unborn。
		///   其余情况（symref 目标存在 / detached sha / 读取失败）一律返回 false 走原流程：
		///   - detached HEAD 必然指向真实 commit（git 不会写出指向无效对象的 detached HEAD）；
		///   - 读失败保守视为非空（与旧版 catch 行为一致）。
		/// 注意 orphan 分支（git checkout --orphan）HEAD unborn 但仓库非空——旧版 rev-parse
		/// 对此同样失败并走"空仓库"路径，本实现保持行为一致，不引入回归。</summary>
		public bool IsHeadUnborn()
		{
			try
			{
				// 注意：实例属性 string Path 遮蔽 System.IO.Path，须全限定（同构造函数）
				string headPath = System.IO.Path.Combine(GitDir(), "HEAD");
				string head = File.ReadAllText(headPath).Trim();
				if (!head.StartsWith("ref: "))
				{
					return false;
				}
				string target = head.Substring(5).Trim();
				if (!target.StartsWith("refs/heads/"))
				{
					return false;
				}
				// refs/heads 恒在 common dir（per-worktree refs 只有 refs/bisect、refs/worktree
				// 等，不含 heads），worktree 场景用 ReferencesGitDir()（见该方法的说明）。
				string refsRoot = ReferencesGitDir();
				string looseRefPath = System.IO.Path.Combine(refsRoot, target.Replace('/', System.IO.Path.DirectorySeparatorChar));
				if (File.Exists(looseRefPath))
				{
					return false;
				}
				string packedRefsPath = System.IO.Path.Combine(refsRoot, "packed-refs");
				if (File.Exists(packedRefsPath))
				{
					foreach (string line in File.ReadLines(packedRefsPath))
					{
						if (line.Length == 0 || line[0] == '#' || line[0] == '^')
						{
							continue;
						}
						// 行格式 "<sha> <ref>"；带前导空格匹配防前缀误命中
						//（如 refs/heads/feature/master 误匹配 refs/heads/master）
						if (line.EndsWith(" " + target, System.StringComparison.Ordinal))
						{
							return false;
						}
					}
				}
				return true;
			}
			catch
			{
				return false;
			}
		}

		public static GitModule Create(string path, string commonGitDir, [Null] string worktreeGitDir, [Null] string parentModulePath)
		{
			if (!Directory.Exists(path))
			{
				return null;
			}
			try
			{
				return new GitModule(path, commonGitDir, worktreeGitDir, parentModulePath);
			}
			catch
			{
				return null;
			}
		}
	}
}
