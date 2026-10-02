using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ForkPlus.Plugins
{
	/// <summary>
	/// v4.5.0：最小 PNG 编码器（8 位 RGBA，每行 filter=None，zlib 存原始扫描线）。
	///
	/// 放在 SDK 里是因为插件与宿主的契约就是"回 PNG 帧"——提供这个编码器，插件作者
	/// 就不必为了回一张图去引入完整图像库（真实插件当然也可以换成 SkiaSharp / 自家库，
	/// 那取决于它想用什么许可证的依赖）。
	/// </summary>
	public static class PngWriter
	{
		private static readonly byte[] Signature = new byte[8] { 137, 80, 78, 71, 13, 10, 26, 10 };

		private static readonly uint[] CrcTable = BuildCrcTable();

		/// <summary>
		/// 把 RGBA 像素缓冲编码成 PNG。<paramref name="rgba"/> 长度必须为 width*height*4
		/// （每像素 R,G,B,A 顺序）。
		/// </summary>
		public static byte[] Encode(int width, int height, byte[] rgba)
		{
			if (width <= 0 || height <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(width), "PNG dimensions must be positive.");
			}
			if (rgba == null || rgba.Length != width * height * 4)
			{
				throw new ArgumentException("RGBA buffer length must be width*height*4.", nameof(rgba));
			}
			int stride = 1 + width * 4;
			byte[] raw = new byte[height * stride];
			for (int y = 0; y < height; y++)
			{
				int rowStart = y * stride;
				raw[rowStart] = 0; // 每行 filter = None
				Buffer.BlockCopy(rgba, y * width * 4, raw, rowStart + 1, width * 4);
			}

			using MemoryStream output = new MemoryStream();
			output.Write(Signature, 0, Signature.Length);
			WriteChunk(output, "IHDR", BuildIhdr(width, height));
			WriteChunk(output, "IDAT", ZlibCompress(raw));
			WriteChunk(output, "IEND", new byte[0]);
			return output.ToArray();
		}

		private static byte[] BuildIhdr(int width, int height)
		{
			byte[] ihdr = new byte[13];
			WriteInt32Be(ihdr, 0, width);
			WriteInt32Be(ihdr, 4, height);
			ihdr[8] = 8;  // bit depth
			ihdr[9] = 6;  // color type: RGBA
			ihdr[10] = 0; // compression
			ihdr[11] = 0; // filter
			ihdr[12] = 0; // interlace
			return ihdr;
		}

		private static byte[] ZlibCompress(byte[] data)
		{
			using MemoryStream output = new MemoryStream();
			output.WriteByte(0x78); // zlib header: CMF
			output.WriteByte(0x01); // zlib header: FLG（无预设字典，最快）
			using (DeflateStream deflate = new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true))
			{
				deflate.Write(data, 0, data.Length);
			}
			uint adler = Adler32(data);
			output.WriteByte((byte)((adler >> 24) & 0xFF));
			output.WriteByte((byte)((adler >> 16) & 0xFF));
			output.WriteByte((byte)((adler >> 8) & 0xFF));
			output.WriteByte((byte)(adler & 0xFF));
			return output.ToArray();
		}

		private static void WriteChunk(Stream stream, string type, byte[] data)
		{
			byte[] typeBytes = Encoding.ASCII.GetBytes(type);
			WriteInt32Be(stream, data.Length);
			stream.Write(typeBytes, 0, typeBytes.Length);
			stream.Write(data, 0, data.Length);
			WriteInt32Be(stream, (int)Crc32(typeBytes, data));
		}

		private static void WriteInt32Be(Stream stream, int value)
		{
			stream.WriteByte((byte)((value >> 24) & 0xFF));
			stream.WriteByte((byte)((value >> 16) & 0xFF));
			stream.WriteByte((byte)((value >> 8) & 0xFF));
			stream.WriteByte((byte)(value & 0xFF));
		}

		private static void WriteInt32Be(byte[] buffer, int offset, int value)
		{
			buffer[offset] = (byte)((value >> 24) & 0xFF);
			buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
			buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
			buffer[offset + 3] = (byte)(value & 0xFF);
		}

		private static uint[] BuildCrcTable()
		{
			uint[] table = new uint[256];
			for (uint n = 0; n < 256; n++)
			{
				uint c = n;
				for (int k = 0; k < 8; k++)
				{
					c = (((c & 1) != 0) ? (0xEDB88320u ^ (c >> 1)) : (c >> 1));
				}
				table[n] = c;
			}
			return table;
		}

		private static uint Crc32(byte[] type, byte[] data)
		{
			uint crc = 0xFFFFFFFFu;
			crc = UpdateCrc(crc, type);
			crc = UpdateCrc(crc, data);
			return crc ^ 0xFFFFFFFFu;
		}

		private static uint UpdateCrc(uint crc, byte[] bytes)
		{
			for (int i = 0; i < bytes.Length; i++)
			{
				crc = CrcTable[(crc ^ bytes[i]) & 0xFF] ^ (crc >> 8);
			}
			return crc;
		}

		private static uint Adler32(byte[] data)
		{
			const uint mod = 65521u;
			uint a = 1;
			uint b = 0;
			for (int i = 0; i < data.Length; i++)
			{
				a = (a + data[i]) % mod;
				b = (b + a) % mod;
			}
			return (b << 16) | a;
		}
	}
}