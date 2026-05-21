using System;
using System.Net;
using ForkPlus.Git;

namespace ForkPlus
{
	public static class NetworkHelper
	{
		private class WebClientWithTimeout : WebClient
		{
			protected override WebRequest GetWebRequest(Uri uri)
			{
				WebRequest webRequest = base.GetWebRequest(uri);
				webRequest.Timeout = 3000;
				return webRequest;
			}
		}

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
			WebClientWithTimeout webClientWithTimeout = new WebClientWithTimeout();
			try
			{
				using (webClientWithTimeout.OpenRead(url))
				{
				}
				return true;
			}
			catch (WebException ex)
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
