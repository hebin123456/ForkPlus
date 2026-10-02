using System;
using System.IO;
using System.Text;

namespace ForkPlus.Plugins
{
	/// <summary>
	/// v4.5.0：LSP 风格分帧的消息通道（<c>Content-Length: n\r\n\r\n</c> + UTF-8 JSON）。
	/// 自带读缓冲——按行读报文头时逐字节读会拖慢大位图传输，缓冲后一次系统调用读满。
	/// 单线程使用（宿主侧由 <see cref="PluginSession"/> 加锁串行化请求）。
	/// </summary>
	public sealed class PluginChannel : IDisposable
	{
		/// <summary>单条报文上限，防止对端（或损坏的流）声称一个天文数字导致 OOM。</summary>
		private const int MaxMessageBytes = 256 * 1024 * 1024;

		private readonly Stream _stream;

		private readonly bool _ownsStream;

		private readonly byte[] _buffer = new byte[16 * 1024];

		private int _start;

		private int _end;

		public PluginChannel(Stream stream, bool ownsStream = true)
		{
			_stream = stream ?? throw new ArgumentNullException(nameof(stream));
			_ownsStream = ownsStream;
		}

		/// <summary>写出一个 JSON 报文（UTF-8，带 Content-Length 头）。</summary>
		public void Write(string json)
		{
			byte[] payload = Encoding.UTF8.GetBytes(json ?? "null");
			byte[] header = Encoding.ASCII.GetBytes("Content-Length: " + payload.Length + "\r\n\r\n");
			_stream.Write(header, 0, header.Length);
			_stream.Write(payload, 0, payload.Length);
			_stream.Flush();
		}

		/// <summary>
		/// 读入一个报文；对端正常关闭（EOF）返回 null。
		/// 报文头损坏或长度越界抛 <see cref="IOException"/>，由会话层转成插件错误。
		/// </summary>
		public string Read()
		{
			int contentLength = -1;
			while (true)
			{
				string line = ReadLine();
				if (line == null)
				{
					return null;
				}
				if (line.Length == 0)
				{
					break;
				}
				const string prefix = "Content-Length:";
				if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					string value = line.Substring(prefix.Length).Trim();
					if (!int.TryParse(value, out contentLength))
					{
						throw new IOException("Plugin sent a malformed Content-Length header: '" + line + "'.");
					}
				}
			}
			if (contentLength < 0)
			{
				throw new IOException("Plugin message had no Content-Length header.");
			}
			if (contentLength > MaxMessageBytes)
			{
				throw new IOException($"Plugin message of {contentLength} bytes exceeds the {MaxMessageBytes} byte limit.");
			}
			byte[] payload = ReadExactly(contentLength);
			if (payload == null)
			{
				return null;
			}
			return Encoding.UTF8.GetString(payload);
		}

		private byte[] ReadExactly(int count)
		{
			byte[] result = new byte[count];
			int offset = 0;
			while (offset < count)
			{
				int available = _end - _start;
				if (available == 0)
				{
					if (!FillBuffer())
					{
						return null;
					}
					continue;
				}
				int take = Math.Min(available, count - offset);
				Buffer.BlockCopy(_buffer, _start, result, offset, take);
				_start += take;
				offset += take;
			}
			return result;
		}

		/// <summary>读一行（不含换行符）；EOF 返回 null。</summary>
		private string ReadLine()
		{
			StringBuilder builder = null;
			while (true)
			{
				if (_start >= _end && !FillBuffer())
				{
					return builder?.ToString();
				}
				int index = Array.IndexOf(_buffer, (byte)10, _start, _end - _start);
				if (index < 0)
				{
					builder = builder ?? new StringBuilder();
					builder.Append(Encoding.ASCII.GetString(_buffer, _start, _end - _start));
					_start = _end;
					continue;
				}
				int length = index - _start;
				string chunk = Encoding.ASCII.GetString(_buffer, _start, length);
				_start = index + 1;
				if (builder == null)
				{
					return chunk.TrimEnd('\r');
				}
				builder.Append(chunk);
				return builder.ToString().TrimEnd('\r');
			}
		}

		private bool FillBuffer()
		{
			_start = 0;
			_end = 0;
			int read = _stream.Read(_buffer, 0, _buffer.Length);
			if (read <= 0)
			{
				return false;
			}
			_end = read;
			return true;
		}

		public void Dispose()
		{
			if (_ownsStream)
			{
				_stream.Dispose();
			}
		}
	}
}