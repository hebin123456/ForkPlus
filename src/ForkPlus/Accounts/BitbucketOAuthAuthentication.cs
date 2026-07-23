using System;
using System.Net.Http;
using System.Net.Http.Headers;
using ForkPlus.Services;
using ForkPlus.Utils.Http;

namespace ForkPlus.Accounts
{
	public class BitbucketOAuthAuthentication : IRestServiceAuthentication
	{
		private readonly object _updateLock = new object();

		private bool _loaded;

		[Null]
		protected readonly string ServerUrl;

		[Null]
		protected OAuthToken OAuthToken;

		public virtual AuthenticationType AuthenticationType => AuthenticationType.OAuth;

		[Null]
		public string Username { get; }

		public BitbucketOAuthAuthentication([Null] string serverUrl, [Null] string username, [Null] OAuthToken token = null)
		{
			ServerUrl = serverUrl;
			Username = username;
			OAuthToken = token;
			if (OAuthToken != null)
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
				ServiceLocator.Credential.RemoveCredential(TokenKey(ServerUrl, Username));
			}
			catch (Exception ex)
			{
				Log.Error("Failed to remove credential entry", ex);
			}
			try
			{
				ServiceLocator.Credential.RemoveCredential(RefreshTokenKey(ServerUrl, Username));
			}
			catch (Exception ex2)
			{
				Log.Error("Failed to remove credential entry", ex2);
			}
			try
			{
				ServiceLocator.Credential.RemoveCredential(ExpirationTimeKey(ServerUrl, Username));
			}
			catch (Exception ex3)
			{
				Log.Error("Failed to remove credential entry", ex3);
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
				ServiceLocator.Credential.WriteCredential(TokenKey(ServerUrl, Username), Username, OAuthToken.Token);
				string refreshToken = OAuthToken.RefreshToken;
				if (refreshToken != null)
				{
					ServiceLocator.Credential.WriteCredential(RefreshTokenKey(ServerUrl, Username), Username, refreshToken);
				}
				DateTime? expirationTimeUTC = OAuthToken.ExpirationTimeUTC;
				if (expirationTimeUTC.HasValue)
				{
					DateTime valueOrDefault = expirationTimeUTC.GetValueOrDefault();
					ServiceLocator.Credential.WriteCredential(ExpirationTimeKey(ServerUrl, Username), Username, valueOrDefault.Ticks.ToString());
				}
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
			RefreshTokenIfNeeded();
			((HttpHeaders)request.Headers).Add("Authorization", "Bearer " + OAuthToken.Token);
		}

		public string GetHttpsPassword()
		{
			EnsureLoaded();
			RefreshTokenIfNeeded();
			return OAuthToken?.Token;
		}

		private void RefreshTokenIfNeeded()
		{
			lock (_updateLock)
			{
				if (OAuthToken == null)
				{
					return;
				}
				DateTime? expirationTimeUTC = OAuthToken.ExpirationTimeUTC;
				if (expirationTimeUTC.HasValue)
				{
					DateTime valueOrDefault = expirationTimeUTC.GetValueOrDefault();
					if (valueOrDefault - DateTime.UtcNow < TimeSpan.FromMinutes(30.0))
					{
						RefreshToken();
					}
				}
			}
		}

		private bool RefreshToken()
		{
			string refreshToken = OAuthToken.RefreshToken;
			if (refreshToken == null)
			{
				return false;
			}
			BasicAuthentication authentication = new BasicAuthentication(null, BitbucketConsts.ClientId, BitbucketConsts.ClientSecret);
			ServiceResult<OAuthToken> serviceResult = new BitbucketAuthenticationService(new Connection("https://bitbucket.org", authentication)).RefreshToken(refreshToken);
			if (!serviceResult.Succeeded)
			{
				return false;
			}
			OAuthToken = serviceResult.Result;
			return true;
		}

		private bool EnsureLoaded()
		{
			lock (_updateLock)
			{
				if (_loaded)
				{
					return true;
				}
				Credential credential = ServiceLocator.Credential.ReadCredential(TokenKey(ServerUrl, Username));
				if (credential == null)
				{
					Log.Error("Cannot read credential record '" + TokenKey(ServerUrl, Username) + "'");
					return false;
				}
				string password = credential.Password;
				string refreshToken = ServiceLocator.Credential.ReadCredential(RefreshTokenKey(ServerUrl, Username))?.Password;
				string s = ServiceLocator.Credential.ReadCredential(ExpirationTimeKey(ServerUrl, Username))?.Password;
				DateTime? expirationTimeUTC = null;
				if (long.TryParse(s, out var result))
				{
					expirationTimeUTC = new DateTime(result);
				}
				OAuthToken = new OAuthToken(password, refreshToken, expirationTimeUTC);
				Log.Info($"Loaded {AuthenticationType} credentials for {Username}@{ServerUrl}");
				_loaded = true;
				return true;
			}
		}

		protected virtual string TokenKey(string host, string username)
		{
			return "fork:" + host + "." + username + ".oauth_token";
		}

		protected virtual string RefreshTokenKey(string host, string username)
		{
			return "fork:" + host + "." + username + ".oauth_refreshtoken";
		}

		protected virtual string ExpirationTimeKey(string host, string username)
		{
			return "fork:" + host + "." + username + ".oauth_expirationtime";
		}
	}
}
