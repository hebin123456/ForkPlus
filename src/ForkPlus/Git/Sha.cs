using System;
using System.Diagnostics;

namespace ForkPlus.Git
{
	public readonly struct Sha : IEquatable<Sha>, IComparable<Sha>
	{
		public static readonly Sha Zero = new Sha(0u, 0u, 0u, 0u, 0u);

		public static readonly Sha NullSha = Parse("4b825dc642cb6eb9a060e54bf8d69288fbee4904").Value;

		public readonly uint DW1;

		public readonly uint DW2;

		public readonly uint DW3;

		public readonly uint DW4;

		public readonly uint DW5;

		public static Sha? Parse(string shaString)
		{
			if (shaString.Length != 40)
			{
				return null;
			}
			uint dw = ReadDword(shaString[0], shaString[1], shaString[2], shaString[3], shaString[4], shaString[5], shaString[6], shaString[7]);
			uint dw2 = ReadDword(shaString[8], shaString[9], shaString[10], shaString[11], shaString[12], shaString[13], shaString[14], shaString[15]);
			uint dw3 = ReadDword(shaString[16], shaString[17], shaString[18], shaString[19], shaString[20], shaString[21], shaString[22], shaString[23]);
			uint dw4 = ReadDword(shaString[24], shaString[25], shaString[26], shaString[27], shaString[28], shaString[29], shaString[30], shaString[31]);
			uint dw5 = ReadDword(shaString[32], shaString[33], shaString[34], shaString[35], shaString[36], shaString[37], shaString[38], shaString[39]);
			return new Sha(dw, dw2, dw3, dw4, dw5);
		}

		public static bool TryParse(string shaString, out Sha result)
		{
			if (shaString.Length != 40)
			{
				result = NullSha;
				return false;
			}
			uint dw = ReadDword(shaString[0], shaString[1], shaString[2], shaString[3], shaString[4], shaString[5], shaString[6], shaString[7]);
			uint dw2 = ReadDword(shaString[8], shaString[9], shaString[10], shaString[11], shaString[12], shaString[13], shaString[14], shaString[15]);
			uint dw3 = ReadDword(shaString[16], shaString[17], shaString[18], shaString[19], shaString[20], shaString[21], shaString[22], shaString[23]);
			uint dw4 = ReadDword(shaString[24], shaString[25], shaString[26], shaString[27], shaString[28], shaString[29], shaString[30], shaString[31]);
			uint dw5 = ReadDword(shaString[32], shaString[33], shaString[34], shaString[35], shaString[36], shaString[37], shaString[38], shaString[39]);
			result = new Sha(dw, dw2, dw3, dw4, dw5);
			return true;
		}

		public Sha(uint dw1, uint dw2, uint dw3, uint dw4, uint dw5)
		{
			DW1 = dw1;
			DW2 = dw2;
			DW3 = dw3;
			DW4 = dw4;
			DW5 = dw5;
		}

		[DebuggerStepThrough]
		public override string ToString()
		{
			return $"{DW1:x8}{DW2:x8}{DW3:x8}{DW4:x8}{DW5:x8}";
		}

		[DebuggerStepThrough]
		public string ToAbbreviatedString()
		{
			return (DW1 / 16).ToString("x7");
		}

		public override int GetHashCode()
		{
			return (int)DW1;
		}

		public override bool Equals(object obj)
		{
			if (obj is Sha sha)
			{
				return this == sha;
			}
			return false;
		}

		public bool Equals(Sha other)
		{
			if (DW1 == other.DW1 && DW2 == other.DW2 && DW3 == other.DW3 && DW4 == other.DW4)
			{
				return DW5 == other.DW5;
			}
			return false;
		}

		public static bool operator ==(Sha lhs, Sha rhs)
		{
			return lhs.Equals(rhs);
		}

		public static bool operator !=(Sha lhs, Sha rhs)
		{
			return !lhs.Equals(rhs);
		}

		private static uint ReadDword(char char1, char char2, char char3, char char4, char char5, char char6, char char7, char char8)
		{
			return (uint)((ReadChar(char1) << 28) | (ReadChar(char2) << 24) | (ReadChar(char3) << 20) | (ReadChar(char4) << 16) | (ReadChar(char5) << 12) | (ReadChar(char6) << 8) | (ReadChar(char7) << 4) | ReadChar(char8));
		}

		private static int ReadChar(char char1)
		{
			if (char1 >= '0' && char1 <= '9')
			{
				return char1 - 48;
			}
			if (char1 >= 'a' && char1 <= 'f')
			{
				return char1 - 97 + 10;
			}
			if (char1 >= 'A' && char1 <= 'F')
			{
				return char1 - 65 + 10;
			}
			throw new Exception();
		}

		public int CompareTo(Sha other)
		{
			int num = DW1.CompareTo(other.DW1);
			if (num != 0)
			{
				return num;
			}
			int num2 = DW2.CompareTo(other.DW2);
			if (num2 != 0)
			{
				return num2;
			}
			int num3 = DW3.CompareTo(other.DW3);
			if (num3 != 0)
			{
				return num3;
			}
			int num4 = DW4.CompareTo(other.DW4);
			if (num4 != 0)
			{
				return num4;
			}
			int num5 = DW5.CompareTo(other.DW5);
			if (num5 != 0)
			{
				return num5;
			}
			return 0;
		}
	}
}
