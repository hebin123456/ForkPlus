using System;

namespace ForkPlus.Git
{
	public class LfsPointer
	{
		public string Sha256String { get; }

		public long Size { get; }

		public string StringPointer => $"version https://git-lfs.github.com/spec/v1\noid sha256:{Sha256String}\nsize {Size}\n";

		[Null]
		public static LfsPointer Parse([Null] string stringPointer)
		{
			if (stringPointer == null)
			{
				return null;
			}
			string[] array = stringPointer.Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries);
			if (array.Length != 3)
			{
				return null;
			}
			string[] array2 = array[1].Split(':');
			if (array2.Length != 2)
			{
				return null;
			}
			string[] array3 = array[2].Split(Consts.Chars.Space);
			if (array3.Length != 2)
			{
				return null;
			}
			if (!long.TryParse(array3[1], out var result))
			{
				return null;
			}
			return new LfsPointer(array2[1], result);
		}

		[Null]
		public static LfsPointer Parse(string oidString, string sizeString)
		{
			string[] array = oidString.Split(':');
			if (array.Length != 2)
			{
				return null;
			}
			string[] array2 = sizeString.Split(Consts.Chars.Space);
			if (array2.Length != 2)
			{
				return null;
			}
			if (!long.TryParse(array2[1].TrimEnd(), out var result))
			{
				return null;
			}
			return new LfsPointer(array[1].TrimEnd(), result);
		}

		public LfsPointer(string sha256String, long size)
		{
			Sha256String = sha256String;
			Size = size;
		}
	}
}
