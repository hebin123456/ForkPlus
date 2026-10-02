using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Plugins
{
	/// <summary>
	/// v4.5.0：插件宿主进程的标准主循环。插件作者只需实现"处理一个请求、返回一个结果对象"，
	/// 分帧读写、shutdown 处理、异常转 <see cref="PluginError"/> 全部由本类完成。
	///
	/// 为什么值得抽出来：插件的 stdout 只允许承载协议报文，任何手写的日志/调试输出都会污染
	/// 协议流；把循环收敛到一处，插件作者就不会各自踩这个坑，也不必每个插件复制一份。
	/// </summary>
	public static class PluginHost
	{
		/// <summary>
		/// 跑主循环，直到 stdin EOF（宿主关闭输入）或收到 shutdown。
		/// <paramref name="handler"/> 返回的对象会被序列化进 <c>response.result</c>；
		/// 返回 null 表示空结果；抛异常则转成 <c>response.error</c>，连接不中断。
		/// </summary>
		public static int Run(Func<PluginRequest, object> handler)
		{
			if (handler == null)
			{
				throw new ArgumentNullException(nameof(handler));
			}
			Stream input = Console.OpenStandardInput();
			Stream output = Console.OpenStandardOutput();
			using PluginChannel reader = new PluginChannel(input, ownsStream: false);
			using PluginChannel writer = new PluginChannel(output, ownsStream: false);
			while (true)
			{
				string json;
				try
				{
					json = reader.Read();
				}
				catch (IOException ex)
				{
					// 报文头损坏等：协议已不可信，只能退出。
					Console.Error.WriteLine("Protocol read failed: " + ex.Message);
					return 1;
				}
				if (json == null)
				{
					// 宿主关闭了输入：正常退出。
					return 0;
				}
				PluginRequest request;
				try
				{
					request = JsonConvert.DeserializeObject<PluginRequest>(json);
				}
				catch (JsonException ex)
				{
					// 单条报文坏掉不该拖垮整个进程；报错后继续等下一条。
					Console.Error.WriteLine("Unreadable request: " + ex.Message);
					continue;
				}
				if (request == null)
				{
					continue;
				}
				if (request.Method == PluginProtocol.MethodShutdown)
				{
					writer.Write(JsonConvert.SerializeObject(new PluginResponse { Id = request.Id }));
					return 0;
				}
				PluginResponse response = new PluginResponse { Id = request.Id };
				try
				{
					object result = handler(request);
					if (result != null)
					{
						response.Result = JToken.FromObject(result);
					}
				}
				catch (Exception ex)
				{
					// 插件内部异常只回给宿主一个错误响应，绝不让进程崩掉。
					Console.Error.WriteLine("Request '" + request.Method + "' failed: " + ex);
					response.Error = new PluginError { Code = "plugin-error", Message = ex.Message };
				}
				writer.Write(JsonConvert.SerializeObject(response));
			}
		}
	}
}