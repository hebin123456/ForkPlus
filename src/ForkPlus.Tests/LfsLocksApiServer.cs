using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ForkPlus.Tests
{
	/// <summary>最小 LFS locks API 服务器（模块18 测试基建，2026-09-06 探针实证可行）：
	/// git-lfs 的 lock/locks/unlock 走 HTTP LFS API（读 lfs.url 配置），file:// 本地远程
	/// 没有 API server（"missing protocol" 报错）——本类在 127.0.0.1 随机端口实现三个端点：
	///   GET  /locks?path=... → {"locks":[{id,path,locked_at,owner}],"next_cursor":""}
	///   POST /locks {"path":...} → 201 {"lock":{...}}；同路径已锁 → 409
	///   POST /locks/{id}/unlock → 200 {"lock":{}}；未知 id → 404
	/// 响应 Content-Type=application/vnd.git-lfs+json（git-lfs 3.0.2 接受，无需鉴权挑战）。
	/// 用法：using var server = LfsLocksApiServer.Start();
	/// 然后 TestRepoFactory.GitOutput(repo, "config lfs.url " + server.BaseUrl)。
	/// owner 名固定 "Test User"——GetLfsLocksGitCommand 解析 `path\towner\tID:...` 三段
	/// tab 分隔输出（与 git lfs locks 实际格式一致，探针实证）。</summary>
	internal sealed class LfsLocksApiServer : IDisposable
	{
		private readonly HttpListener _listener;

		private readonly Thread _thread;

		private volatile bool _running = true;

		private readonly ConcurrentDictionary<string, string> _locksById = new ConcurrentDictionary<string, string>();

		private int _nextId;

		public string BaseUrl { get; }

		private LfsLocksApiServer(HttpListener listener, int port)
		{
			_listener = listener;
			BaseUrl = "http://127.0.0.1:" + port;
			_thread = new Thread(AcceptLoop)
			{
				IsBackground = true,
				Name = "LfsLocksApiServer"
			};
			_thread.Start();
		}

		public static LfsLocksApiServer Start()
		{
			// 随机空闲端口（TcpListener port 0 → 内核分配；关掉再给 HttpListener 用，小竞态可接受）
			int port;
			using (var probe = new TcpListener(IPAddress.Loopback, 0))
			{
				probe.Start();
				port = ((IPEndPoint)probe.LocalEndpoint).Port;
				probe.Stop();
			}
			var listener = new HttpListener();
			listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
			listener.Start();
			return new LfsLocksApiServer(listener, port);
		}

		private void AcceptLoop()
		{
			while (_running)
			{
				HttpListenerContext context;
				try
				{
					context = _listener.GetContext();
				}
				catch (Exception)
				{
					break; // listener 已 Stop/Close（Dispose 路径）
				}
				try
				{
					Handle(context);
				}
				catch (Exception)
				{
					// 单个请求处理失败不拖垮服务（git-lfs 对 5xx 有自己的重试/报错语义）
				}
			}
		}

		private void Handle(HttpListenerContext context)
		{
			string method = context.Request.HttpMethod;
			string path = context.Request.Url.AbsolutePath;
			if (method == "GET" && path == "/locks")
			{
				// path 过滤：git-lfs unlock <path> 先 GET /locks?path=... 找锁 ID（URL 编码）
				string pathFilter = GetQueryParam(context.Request.Url.Query, "path");
				string[] locks = _locksById
					.Where(kv => pathFilter == null || kv.Value == pathFilter)
					.Select(kv => LockJson(kv.Key, kv.Value))
					.ToArray();
				Respond(context, 200, "{\"locks\":[" + string.Join(",", locks) + "],\"next_cursor\":\"\"}");
			}
			else if (method == "POST" && path == "/locks")
			{
				string body = ReadBody(context);
				string filePath = ExtractJsonStringField(body, "path");
				if (filePath == null)
				{
					Respond(context, 400, "{\"message\":\"missing path\"}");
					return;
				}
				var existing = _locksById.FirstOrDefault(kv => kv.Value == filePath);
				if (existing.Key != null)
				{
					Respond(context, 409, "{\"lock\":" + LockJson(existing.Key, existing.Value) + ",\"message\":\"already created lock\"}");
					return;
				}
				string id = "lock-" + Interlocked.Increment(ref _nextId);
				_locksById[id] = filePath;
				Respond(context, 201, "{\"lock\":" + LockJson(id, filePath) + "}");
			}
			else if (method == "POST" && path.StartsWith("/locks/", StringComparison.Ordinal) && path.EndsWith("/unlock", StringComparison.Ordinal))
			{
				string id = path.Substring("/locks/".Length, path.Length - "/locks/".Length - "/unlock".Length);
				if (_locksById.TryRemove(id, out _))
				{
					Respond(context, 200, "{\"lock\":{}}");
				}
				else
				{
					Respond(context, 404, "{\"message\":\"lock not found\"}");
				}
			}
			else
			{
				Respond(context, 404, "{\"message\":\"not found\"}");
			}
		}

		private static void Respond(HttpListenerContext context, int status, string json)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(json);
			context.Response.StatusCode = status;
			context.Response.ContentType = "application/vnd.git-lfs+json";
			context.Response.ContentLength64 = bytes.Length;
			context.Response.OutputStream.Write(bytes, 0, bytes.Length);
			context.Response.OutputStream.Close();
		}

		private static string ReadBody(HttpListenerContext context)
		{
			using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
			{
				return reader.ReadToEnd();
			}
		}

		/// <summary>极简 JSON 字符串字段提取（POST /locks 的 path 值）——避免测试基建引入
		/// System.Text.Json 文档解析样板；路径不含转义序列（git-lfs 发裸文件路径）。</summary>
		private static string ExtractJsonStringField(string json, string field)
		{
			string marker = "\"" + field + "\"";
			int fieldIndex = json.IndexOf(marker, StringComparison.Ordinal);
			if (fieldIndex == -1)
			{
				return null;
			}
			int colon = json.IndexOf(':', fieldIndex + marker.Length);
			if (colon == -1)
			{
				return null;
			}
			int open = json.IndexOf('"', colon + 1);
			if (open == -1)
			{
				return null;
			}
			int close = json.IndexOf('"', open + 1);
			if (close == -1)
			{
				return null;
			}
			return json.Substring(open + 1, close - open - 1);
		}

		private static string GetQueryParam(string query, string name)
		{
			if (string.IsNullOrEmpty(query))
			{
				return null;
			}
			foreach (string pair in query.TrimStart('?').Split('&'))
			{
				int eq = pair.IndexOf('=');
				if (eq > 0 && pair.Substring(0, eq) == name)
				{
					return Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' '));
				}
			}
			return null;
		}

		private static string LockJson(string id, string path)
		{
			return "{\"id\":\"" + id + "\",\"path\":\"" + path + "\",\"locked_at\":\"2026-09-06T00:00:00Z\",\"owner\":{\"name\":\"Test User\"}}";
		}

		public void Dispose()
		{
			_running = false;
			try
			{
				_listener.Stop();
			}
			catch (Exception)
			{
			}
			try
			{
				_listener.Close();
			}
			catch (Exception)
			{
			}
		}
	}
}
