using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ForkPlus.Biturbo;

namespace ForkPlus.Git.Commands
{
	internal static class BtReferencesExtensions
	{
		public static GitCommandResult<(string[], Sha[])> GetRefs(this BtReferences btReferences)
		{
			int num = (int)btReferences.names_offsets_len;
			long[] array = new long[num];
			Marshal.Copy(btReferences.names_offsets, array, 0, num);
			int num2 = (int)btReferences.names_data_len;
			byte[] array2 = new byte[num2];
			if (num2 > 0)
			{
				Marshal.Copy(btReferences.names_data, array2, 0, num2);
			}
			string[] array3 = new string[num];
			for (int i = 0; i < num; i++)
			{
				int num3 = (int)((i != 0) ? array[i - 1] : 0);
				int count = (int)array[i] - num3;
				string @string = Encoding.UTF8.GetString(array2, num3, count);
				array3[i] = @string;
			}
			int num4 = Marshal.SizeOf<BtOid>();
			Sha[] array4 = new Sha[num];
			for (int j = 0; j < num; j++)
			{
				BtOid @this = Marshal.PtrToStructure<BtOid>(new IntPtr(btReferences.oids.ToInt64() + j * num4));
				array4[j] = @this.ToSha();
			}
			return GitCommandResult<(string[], Sha[])>.Success((array3, array4));
		}

		public static GitCommandResult<(string[], string[])> GetSymrefs(this BtReferences btReferences)
		{
			int num = (int)btReferences.symrefs_offsets_len / 2;
			long[] array = new long[btReferences.symrefs_offsets_len];
			if (btReferences.symrefs_offsets_len > 0)
			{
				Marshal.Copy(btReferences.symrefs_offsets, array, 0, (int)btReferences.symrefs_offsets_len);
			}
			int num2 = (int)btReferences.symrefs_data_len;
			byte[] array2 = new byte[num2];
			if (num2 > 0)
			{
				Marshal.Copy(btReferences.symrefs_data, array2, 0, num2);
			}
			string[] array3 = new string[num];
			string[] array4 = new string[num];
			for (int i = 0; i < num; i++)
			{
				int num3 = (int)((i != 0) ? array[i * 2 - 1] : 0);
				int num4 = (int)array[i * 2];
				int count = num4 - num3;
				array3[i] = Encoding.UTF8.GetString(array2, num3, count);
				int num5 = num4;
				int count2 = (int)array[i * 2 + 1] - num5;
				array4[i] = Encoding.UTF8.GetString(array2, num5, count2);
			}
			return GitCommandResult<(string[], string[])>.Success((array3, array4));
		}

		/// <summary>Migration note：确保 symrefs 里含 "HEAD" 条目。
		/// native biturbo 的 bt_get_references 在 Linux 上不返回 HEAD symref（Windows 原版返回，
		/// 实测 Linux 仅返回 refs/remotes/origin/HEAD）。ReferenceStorage.New 靠 "HEAD" → target
		/// 匹配 refs/heads/* 推导 ActiveBranchIndex；缺失时恒 null → 本地分支 IsActive 恒 false
		/// （侧栏当前分支不加粗/无 ActiveBranch 对勾图标、引用徽章当前分支不加粗）。
		/// 读 .git/HEAD 兜底追加（detached HEAD 非 symref 情况原样返回）。</summary>
		public static (string[] symrefs, string[] targets) EnsureHeadSymref(string gitDir, string[] symrefs, string[] targets)
		{
			for (int i = 0; i < symrefs.Length; i++)
			{
				if (symrefs[i] == "HEAD")
				{
					return (symrefs, targets);
				}
			}
			string text = ReadHeadSymrefTarget(gitDir);
			if (text == null || !text.StartsWith("refs/heads/"))
			{
				return (symrefs, targets);
			}
			string[] array = new string[symrefs.Length + 1];
			string[] array2 = new string[targets.Length + 1];
			Array.Copy(symrefs, array, symrefs.Length);
			Array.Copy(targets, array2, targets.Length);
			array[symrefs.Length] = "HEAD";
			array2[targets.Length] = text;
			Log.Info("bt_get_references missing HEAD symref (Linux), appended from .git/HEAD -> " + text);
			return (array, array2);
		}

		/// <summary>按仓库形态把 symrefs 里的 HEAD 条目对齐到「本模块自己的 HEAD」。
		/// 普通仓库 / 子模块：走 <see cref="EnsureHeadSymref"/>（bt 缺 HEAD symref 时读 .git/HEAD 补齐，
		/// 已有则原样保留——bt 对普通仓库返回的就是该仓库自己的 HEAD）。
		/// 链接 worktree：refs 从 common dir 枚举（见 <see cref="GitModule.ReferencesGitDir"/>），
		/// bt 返回的 HEAD symref 是**主 worktree** 的 HEAD，不是本 worktree 的——必须按
		/// &lt;worktreeGitDir&gt;/HEAD 覆盖 target；本 worktree 处于 detached HEAD（HEAD 文件为裸 sha）
		/// 时移除 HEAD 条目，让 UI 与 git 一致地显示 detached 状态。</summary>
		public static (string[] symrefs, string[] targets) ResolveRepositoryHead(GitModule gitModule, string[] symrefs, string[] targets)
		{
			if (gitModule.Type == ModuleType.Worktree)
			{
				return ApplyWorktreeHead(gitModule.WorktreeGitDir, symrefs, targets);
			}
			return EnsureHeadSymref(gitModule.GitDir(), symrefs, targets);
		}

		/// <summary>用 worktree 自身的 HEAD 修正 symrefs 里的 HEAD 条目（见 <see cref="ResolveRepositoryHead"/>）。</summary>
		private static (string[] symrefs, string[] targets) ApplyWorktreeHead(string worktreeGitDir, string[] symrefs, string[] targets)
		{
			string text = ReadHeadSymrefTarget(worktreeGitDir);
			int num = Array.IndexOf(symrefs, "HEAD");
			if (text == null || !text.StartsWith("refs/heads/"))
			{
				// detached HEAD（HEAD 是裸 sha，或读取失败）：移除 HEAD symref，ActiveBranchIndex
				// 推导为 null → UI 显示 detached HEAD（与 git 行为一致）。保留 bt 返回的主 worktree
				// HEAD 是错误信息——会把别的 worktree 的分支显示成本仓库的当前分支。
				if (num < 0)
				{
					return (symrefs, targets);
				}
				Log.Info("worktree HEAD is detached, dropping main-worktree HEAD symref for this worktree");
				return (RemoveAt(symrefs, num), RemoveAt(targets, num));
			}
			if (num >= 0)
			{
				if (!(targets[num] == text))
				{
					string[] array = (string[])targets.Clone();
					array[num] = text;
					Log.Info("worktree HEAD symref corrected: " + text);
					return (symrefs, array);
				}
				return (symrefs, targets);
			}
			string[] array2 = new string[symrefs.Length + 1];
			string[] array3 = new string[targets.Length + 1];
			Array.Copy(symrefs, array2, symrefs.Length);
			Array.Copy(targets, array3, targets.Length);
			array2[symrefs.Length] = "HEAD";
			array3[targets.Length] = text;
			Log.Info("worktree HEAD symref appended -> " + text);
			return (array2, array3);
		}

		private static string[] RemoveAt(string[] array, int index)
		{
			string[] array2 = new string[array.Length - 1];
			Array.Copy(array, array2, index);
			Array.Copy(array, index + 1, array2, index, array.Length - index - 1);
			return array2;
		}

		/// <summary>读取 .git/HEAD 文件，如果是 symref（"ref: refs/heads/xxx"）返回 target，否则返回 null。</summary>
		[Null]
		private static string ReadHeadSymrefTarget(string gitDir)
		{
			try
			{
				string path = PathHelper.Combine(gitDir, "HEAD");
				string text = File.ReadAllText(path).TrimEnd();
				if (text.StartsWith("ref: "))
				{
					return text.Substring(5);
				}
				return null;
			}
			catch (Exception ex)
			{
				Log.Warn("Cannot read .git/HEAD: " + ex.Message);
				return null;
			}
		}
	}
}
