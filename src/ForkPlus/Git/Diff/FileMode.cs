using System.Diagnostics;

namespace ForkPlus.Git.Diff
{
	[DebuggerDisplay("{AsString()}")]
	public struct FileMode
	{
		public static FileMode Submodule = new FileMode(160000);

		private int _fileMode;

		public static FileMode? Parse(string input)
		{
			if (int.TryParse(input, out var result))
			{
				if (result < 0 || result > 999999)
				{
					return null;
				}
				return new FileMode(result);
			}
			return null;
		}

		public FileMode(int value)
		{
			_fileMode = value;
		}

		public string AsString()
		{
			return $"{_fileMode:000000}";
		}

		public static bool operator ==(FileMode c1, FileMode c2)
		{
			return c1._fileMode == c2._fileMode;
		}

		public static bool operator !=(FileMode c1, FileMode c2)
		{
			return !(c1 == c2);
		}

		public override bool Equals(object obj)
		{
			if (obj is FileMode fileMode)
			{
				return _fileMode == fileMode._fileMode;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return _fileMode.GetHashCode();
		}
	}
}
