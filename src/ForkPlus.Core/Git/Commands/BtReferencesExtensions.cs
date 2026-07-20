using System;
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
	}
}
