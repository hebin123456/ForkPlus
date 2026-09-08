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
