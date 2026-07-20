using System;
using System.Diagnostics;

namespace ForkPlus.Git
{
	[DebuggerDisplay("{UrlString}")]
	public class GitUrl
	{
		public enum NetworkProtocol
		{
			Ssh,
			Https,
			Other
		}

		public string UrlString { get; }

		public NetworkProtocol Protocol { get; }

		[Null]
		public Uri Uri { get; }

		[Null]
		public string Host { get; }

		[Null]
		public string Username { get; }

		public RemoteType RemoteType { get; }

		[Null]
		public string Slug { get; }

		[Null]
		public string RepositoryName { get; }

		public bool IsValid => RepositoryName != null;

		public GitUrl(string urlString)
		{
			UrlString = urlString;
			RemoteType = RemoteType.Custom;
			string text = urlString;
			if (text.StartsWith("git@"))
			{
				text = text.Substring("git@".Length);
				text = text.Replace(":", "/");
				text = "ssh://" + text;
			}
			else if (text.Contains("@vs-ssh.visualstudio.com"))
			{
				text = text.Replace("@vs-ssh", "");
				text = text.Replace(":", "/");
				text = "ssh://" + text;
			}
			if (!Uri.TryCreate(text, UriKind.Absolute, out var result))
			{
				Host = null;
				Protocol = NetworkProtocol.Other;
				Uri = null;
				Username = null;
				return;
			}
			Host = result.Host.ToLower();
			Protocol = GetProtocol(result.Scheme);
			RemoteType = GetRemoteType(Host);
			Uri = result;
			Username = ParseUsername(result);
			string text2 = Uri.LocalPath.TrimStart(Consts.Chars.Slash);
			if (text2.EndsWith(".git"))
			{
				text2 = text2.Substring(0, text2.Length - ".git".Length);
			}
			Slug = text2;
			string[] array = text2.Split(Consts.Chars.Slash);
			if (array.Length >= 2)
			{
				RepositoryName = array[array.Length - 1];
			}
			else if (array.Length == 1)
			{
				RepositoryName = array[0];
			}
		}

		[Null]
		private static string ParseUsername(Uri uri)
		{
			if (uri.Scheme == "ssh")
			{
				return null;
			}
			string userInfo = uri.UserInfo;
			if (string.IsNullOrEmpty(userInfo))
			{
				return null;
			}
			int num = userInfo.IndexOf(":");
			return num switch
			{
				0 => null, 
				-1 => userInfo, 
				_ => userInfo.Substring(0, num), 
			};
		}

		private static NetworkProtocol GetProtocol(string scheme)
		{
			if (scheme == "https")
			{
				return NetworkProtocol.Https;
			}
			if (scheme == "ssh")
			{
				return NetworkProtocol.Ssh;
			}
			return NetworkProtocol.Other;
		}

		private static RemoteType GetRemoteType([Null] string host)
		{
			if (host == null)
			{
				return RemoteType.Custom;
			}
			if (host.Contains("github.com"))
			{
				return RemoteType.Github;
			}
			if (host.Contains("gitlab.com"))
			{
				return RemoteType.Gitlab;
			}
			if (host.Contains("bitbucket.org"))
			{
				return RemoteType.Bitbucket;
			}
			if (host.Contains("dev.azure.com"))
			{
				return RemoteType.Azure;
			}
			if (host.Contains("visualstudio.com"))
			{
				return RemoteType.Visualstudio;
			}
			return RemoteType.Custom;
		}
	}
}
