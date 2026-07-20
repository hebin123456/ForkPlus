using System;
using System.Net;
using System.Net.Http;
using ForkPlus.Git;

namespace ForkPlus
{
	public static class NetworkHelper
	{
		// .NET 10 起 WebClient 已过时（SYSLIB0014），改用 HttpClient。
		// 静态共享实例避免每次请求创建新的 socket 连接耗尽端口。
		// 3 秒超时对应原 WebClientWithTimeout 的行为。
		private static readonly HttpClient HttpClient = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(3)
		};

		public static bool CheckConnection(RemoteType remoteType)
		{
			switch (remoteType)
			{
			case RemoteType.Bitbucket:
				return CheckConnection("https://bitbucket.org");
			case RemoteType.Github:
				return CheckConnection("https://github.com");
			default:
				Log.Error("Unknown remote type");
				return true;
			}
		}

		private static bool CheckConnection(string url)
		{
			Benchmarker benchmarker = new Benchmarker("Check internet connection: " + url);
			try
			{
				using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, url))
				using (HttpResponseMessage response = HttpClient.Send(request))
				{
					return true;
				}
			}
			catch (HttpRequestException ex)
			{
				Log.Warn("Failed to check internet connection", ex);
				return false;
			}
			finally
			{
				benchmarker.ReportElapsed();
			}
		}
	}
}
