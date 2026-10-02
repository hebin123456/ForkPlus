using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Plugins
{
	/// <summary>
	/// v4.5.0：宿主与插件进程之间的线协议常量。
	///
	/// 传输：宿主启动插件自带的宿主可执行文件，走其 stdin/stdout；stderr 归插件打日志。
	/// 分帧：LSP 风格——<c>Content-Length: &lt;n&gt;\r\n\r\n</c> + n 字节 UTF-8 JSON。
	/// 语义：请求-响应（宿主发 <see cref="PluginRequest"/>，插件回 <see cref="PluginResponse"/>），
	/// 宿主不期望也不处理插件的主动通知。
	///
	/// 版本策略：<see cref="Version"/> 是硬闸门——清单 apiVersion 与之不符的插件直接拒载，
	/// 避免"跑起来才炸"。新增可选字段不算破坏性变更，不必递增版本号。
	///
	/// 为什么走位图而不是让插件返回 UI 控件：见 <see cref="RenderResult"/>。
	/// </summary>
	public static class PluginProtocol
	{
		/// <summary>当前宿主实现的协议版本。递增意味着老插件需要重新编译。</summary>
		public const int Version = 1;

		public const string MethodHello = "hello";

		public const string MethodRender = "render";

		public const string MethodShutdown = "shutdown";

		/// <summary>握手：宿主告知自身版本，插件回报名下的视图清单。</summary>
		public static PluginRequest CreateHello(int id, string hostName, string hostVersion)
		{
			return new PluginRequest(id, MethodHello, JObject.FromObject(new HelloParams
			{
				ProtocolVersion = Version,
				HostName = hostName,
				HostVersion = hostVersion
			}));
		}

		/// <summary>渲染/匹配请求：把两侧内容交给插件，插件返回位图。</summary>
		public static PluginRequest CreateRender(int id, RenderParams parameters)
		{
			return new PluginRequest(id, MethodRender, JObject.FromObject(parameters));
		}
	}

	/// <summary>v4.5.0：一次请求。Params 用 JObject 承载，便于插件侧按需扩展字段。</summary>
	public sealed class PluginRequest
	{
		[JsonProperty("id")]
		public int Id { get; set; }

		[JsonProperty("method")]
		public string Method { get; set; }

		[JsonProperty("params")]
		public JObject Params { get; set; }

		public PluginRequest()
		{
		}

		public PluginRequest(int id, string method, JObject parameters)
		{
			Id = id;
			Method = method;
			Params = parameters;
		}
	}

	/// <summary>v4.5.0：一次响应（id 与请求对应）。</summary>
	public sealed class PluginResponse
	{
		[JsonProperty("id")]
		public int Id { get; set; }

		[JsonProperty("result")]
		public JToken Result { get; set; }

		[JsonProperty("error")]
		public PluginError Error { get; set; }

		public bool IsError => Error != null;
	}

	/// <summary>v4.5.0：协议级错误。插件内部异常应转成这里，而不是把宿主连接搞崩。</summary>
	public sealed class PluginError
	{
		[JsonProperty("code")]
		public string Code { get; set; }

		[JsonProperty("message")]
		public string Message { get; set; }

		public override string ToString()
		{
			return string.IsNullOrEmpty(Code) ? Message : Code + ": " + Message;
		}
	}

	/// <summary>v4.5.0：hello 请求参数。</summary>
	public sealed class HelloParams
	{
		[JsonProperty("protocolVersion")]
		public int ProtocolVersion { get; set; }

		[JsonProperty("hostName")]
		public string HostName { get; set; }

		[JsonProperty("hostVersion")]
		public string HostVersion { get; set; }
	}

	/// <summary>v4.5.0：hello 响应结果——插件自我介绍。</summary>
	public sealed class HelloResult
	{
		[JsonProperty("protocolVersion")]
		public int ProtocolVersion { get; set; }

		[JsonProperty("pluginId")]
		public string PluginId { get; set; }

		[JsonProperty("name")]
		public string Name { get; set; }

		[JsonProperty("version")]
		public string Version { get; set; }
	}

	/// <summary>
	/// v4.5.0：render 请求参数——渲染"一侧"内容。git blob 未必落盘，故内容以字节给出
	/// （Newtonsoft 会把 byte[] 自动编成 base64 字符串，插件侧解码后自行渲染）。
	///
	/// 为什么是单侧：宿主的对比是"左右各一个 BinaryContentUserControl + 宿主级的并排/滑动/
	/// 洋葱皮/像素高亮"，插件只需回答"这一侧长什么样"，对比交互由宿主统一提供，
	/// 插件不必（也不能）自己实现一套对比容器。
	/// </summary>
	public sealed class RenderParams
	{
		/// <summary>要调用的视图 id（清单里声明的那个）。</summary>
		[JsonProperty("viewerId")]
		public string ViewerId { get; set; }

		/// <summary>仓库内路径（用于展示与插件判定）；未知可为 null。</summary>
		[JsonProperty("path")]
		public string Path { get; set; }

		[JsonProperty("isLfs")]
		public bool IsLfs { get; set; }

		/// <summary>该侧字节；无内容（新增/删除文件、LFS 未 smudge、大文件未预载）时为 null。</summary>
		[JsonProperty("data")]
		public byte[] Data { get; set; }

		/// <summary>宿主建议的渲染宽度（像素），插件可参考但不强制。</summary>
		[JsonProperty("width")]
		public int Width { get; set; }

		/// <summary>宿主建议的渲染高度（像素）。</summary>
		[JsonProperty("height")]
		public int Height { get; set; }

		/// <summary>"light" / "dark"，插件据此适配配色。</summary>
		[JsonProperty("theme")]
		public string Theme { get; set; }
	}

	/// <summary>
	/// v4.5.0：render 响应结果。
	///
	/// 设计取舍：插件只能回位图（0..n 帧）+ 一行状态文案，不能回 Avalonia 控件。
	/// 这样插件进程与主程序之间只有字节流，既拿到了进程隔离（GPL / 崩溃 / 依赖冲突
	/// 都关在插件进程里），也让宿主能复用现成的缩放、平移、并排、滑动、洋葱皮、
	/// 像素高亮等对比交互——插件不必自己实现这些。
	/// 代价是插件无法自带按钮/输入框；确有需要时由宿主的通用控件（如动图播放条）承载。
	/// </summary>
	public sealed class RenderResult
	{
		/// <summary>渲染帧（PNG 编码）。空表示插件认领了但渲染不出内容，宿主按失败处理。</summary>
		[JsonProperty("frames")]
		public List<byte[]> Frames { get; set; }

		/// <summary>多帧时的帧间隔（毫秒）；0 或单帧表示静态图。</summary>
		[JsonProperty("frameDelayMs")]
		public int FrameDelayMs { get; set; }

		/// <summary>附加到文件名后的一行说明（如 "3 字重 · 512px"）。可为 null。</summary>
		[JsonProperty("statusLabel")]
		public string StatusLabel { get; set; }

		/// <summary>插件侧的非致命说明（渲染降级等），宿主记日志用。</summary>
		[JsonProperty("warning")]
		public string Warning { get; set; }
	}
}