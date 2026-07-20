using System;
using System.Net.Http;
using System.Net.Http.Headers;
using ForkPlus.Utils.Http;

namespace ForkPlus.Accounts
{
	public class PrivateAccessTokenAuthentication : IRestServiceAuthentication
	{
		private readonly object _updateLock = new object();

		private bool _loaded;

		[Null]
		protected readonly string ServerUrl;

		[Null]
		protected string Token;

		public virtual AuthenticationType AuthenticationType => AuthenticationType.AccessToken;

		[Null]
		public string Username { get; }

		public PrivateAccessTokenAuthentication([Null] string serverUrl, [Null] string username, [Null] string token = null)
		{
			ServerUrl = serverUrl;
			Username = username;
			Token = token;
			if (Token != null)
			{
				_loaded = true;
			}
		}

		public void Destroy()
		{
			if (ServerUrl == null || Username == null)
			{
				throw new InvalidOperationException("Authentication without host can not be saved");
			}
			try
			{
				WindowsCredentialManager.RemoveCredential(Key(ServerUrl, Username));
			}
			catch (Exception ex)
			{
				Log.Error("Failed to remove credential entry", ex);
			}
			try
			{
				WindowsCredentialManager.RemoveCredential(OldKey(ServerUrl, Username));
			}
			catch (Exception ex2)
			{
				Log.Warn("Failed to remove credential entry", ex2);
			}
			Log.Info($"Removed {AuthenticationType} credentials for {Username}@{ServerUrl}");
		}

		public bool Save()
		{
			if (ServerUrl == null || Username == null)
			{
				throw new InvalidOperationException("Authentication without host can not be saved");
			}
			try
			{
				WindowsCredentialManager.WriteCredential(Key(ServerUrl, Username), Username, Token);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to write credential entry", ex);
				return false;
			}
			Log.Info("Saved credentials for " + Username + "@" + ServerUrl);
			return true;
		}

		public bool Authorize(HttpRequestMessage request)
		{
			if (!EnsureLoaded())
			{
				return false;
			}
			DoAuthorize(request);
			return true;
		}

		protected virtual void DoAuthorize(HttpRequestMessage request)
		{
			((HttpHeaders)request.Headers).Add("Authorization", "Bearer " + Token);
		}

		public string GetHttpsPassword()
		{
			EnsureLoaded();
			return Token;
		}

		private bool EnsureLoaded()
		{
			lock (_updateLock)
			{
				if (_loaded)
				{
					return true;
				}
				Credential credential = WindowsCredentialManager.ReadCredential(Key(ServerUrl, Username));
				Credential credential3;
				if (credential == null)
				{
					Credential credential2 = WindowsCredentialManager.ReadCredential(OldKey(ServerUrl, Username));
					if (credential2 == null)
					{
						Log.Error("Cannot read credential records '" + Key(ServerUrl, Username) + "' and '" + OldKey(ServerUrl, Username) + "'");
						return false;
					}
					credential3 = credential2;
				}
				else
				{
					credential3 = credential;
				}
				Token = credential3.Password;
				Log.Info($"Loaded {AuthenticationType} credentials for {Username}@{ServerUrl}");
				_loaded = true;
				return true;
			}
		}

		protected virtual string Key(string host, string username)
		{
			return "fork:" + host + "." + username + ".accesstoken";
		}

		private string OldKey(string host, string username)
		{
			return "fork:" + host + "." + username;
		}
	}
}
