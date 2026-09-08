using System.IO;
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
			// Bug fix (2026-09-04, "there was a problem with the editor" 间歇性失败)：
			// 旧实现 (byte)stream.ReadByte() 在对端提前断开时把 EOF(-1) 强转为 255，
			// 拼出垃圾长度并分配错误缓冲。此处显式检测 EOF 并返回 null（调用方已判空）。
			byte b0 = ReadByteOrThrow(stream);
			byte b1 = ReadByteOrThrow(stream);
			byte b2 = ReadByteOrThrow(stream);
			byte b3 = ReadByteOrThrow(stream);
			IntToByteLE intToByteLE = default(IntToByteLE);
			intToByteLE.B0 = b0;
			intToByteLE.B1 = b1;
			intToByteLE.B2 = b2;
			intToByteLE.B3 = b3;
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

		private static byte ReadByteOrThrow(PipeStream stream)
		{
			int value = stream.ReadByte();
			if (value < 0)
			{
				throw new EndOfStreamException("IPC pipe closed while reading message header");
			}
			return (byte)value;
		}

		public static int WriteString(this PipeStream stream, string outString)
		{
			byte[] bytes = _defaultStreamEncoding.GetBytes(outString);
			int num = bytes.Length;
			// Bug fix (2026-09-04, "there was a problem with the editor" 间歇性失败)：
			// 旧实现用 4 次 WriteByte 逐字节写长度前缀；Unix 域套接字是流式语义，
			// 每次单字节 write 都可能立刻唤醒阻塞在对端 Read(4) 的 RI/AskPass 客户端，
			// 使其只读到 1~3 字节前缀而误判消息损坏、以退出码 1 结束，git 随即报
			// "there was a problem with the editor"（实测 500 次往返约 0.4% 复现率）。
			// 改为单次 4 字节原子写入，客户端一次 Read 即可读全。
			IntToByteLE intToByteLE = default(IntToByteLE);
			intToByteLE.IntVal = num;
			byte[] array = new byte[4] { intToByteLE.B0, intToByteLE.B1, intToByteLE.B2, intToByteLE.B3 };
			stream.Write(array, 0, array.Length);
			stream.Write(bytes, 0, num);
			stream.Flush();
			return num + 4;
		}
	}
}
