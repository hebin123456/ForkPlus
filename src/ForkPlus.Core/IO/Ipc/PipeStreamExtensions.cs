using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;

namespace ForkPlus.IO.Ipc
{
	public static class PipeStreamExtensions
	{
		[StructLayout(LayoutKind.Explicit)]
		public struct IntToByteLE
		{
			[FieldOffset(0)]
			public int IntVal;

			[FieldOffset(0)]
			public byte B0;

			[FieldOffset(1)]
			public byte B1;

			[FieldOffset(2)]
			public byte B2;

			[FieldOffset(3)]
			public byte B3;
		}

		private static UnicodeEncoding _defaultStreamEncoding = new UnicodeEncoding();

		public static string ReadString(this PipeStream stream)
		{
			IntToByteLE intToByteLE = default(IntToByteLE);
			intToByteLE.B0 = (byte)stream.ReadByte();
			intToByteLE.B1 = (byte)stream.ReadByte();
			intToByteLE.B2 = (byte)stream.ReadByte();
			intToByteLE.B3 = (byte)stream.ReadByte();
			int intVal = intToByteLE.IntVal;
			byte[] array = new byte[intVal];
			// PipeStream.Read 可能返回少于请求的字节数（CA2022），用循环读满。
			int offset = 0;
			while (offset < intVal)
			{
				int read = stream.Read(array, offset, intVal - offset);
				if (read <= 0)
				{
					break;
				}
				offset += read;
			}
			return _defaultStreamEncoding.GetString(array);
		}

		public static int WriteString(this PipeStream stream, string outString)
		{
			byte[] bytes = _defaultStreamEncoding.GetBytes(outString);
			int num = bytes.Length;
			IntToByteLE intToByteLE = default(IntToByteLE);
			intToByteLE.IntVal = num;
			stream.WriteByte(intToByteLE.B0);
			stream.WriteByte(intToByteLE.B1);
			stream.WriteByte(intToByteLE.B2);
			stream.WriteByte(intToByteLE.B3);
			stream.Write(bytes, 0, num);
			stream.Flush();
			return num + 4;
		}
	}
}
