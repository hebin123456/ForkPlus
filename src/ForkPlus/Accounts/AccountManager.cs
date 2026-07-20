using System;
using System.Collections.Generic;
using System.IO;
using ForkPlus.Git;
using ForkPlus.Utils.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Accounts
{
	public class AccountManager : IAccountManager
	{
		private class Coder
		{
			public static JArray Encode(Account[] accounts)
			{
				JArray jArray = new JArray();
				foreach (Account account in accounts)
				{
					jArray.Add(Encode(account));
				}
				return jArray;
			}

			private static JToken Encode(Account account)
			{
				JObject jObject = new JObject();
				jObject.Add("serviceType", EncodeServiceType(account.ServiceType));
				jObject.Add("authenticationType", EncodeAuthenticationType(account.AuthenticationType));
				jObject.Add("serverUrl", new JValue(account.ServerUrl));
				jObject.Add("username", new JValue(account.Username));
				jObject.Add("avatarUrl", new JValue(account.AvatarUrl));
				if (account.EnableNotifications)
				{
					jObject.Add("enableNotifications", new JValue(account.EnableNotifications));
					if (account.NotificationsUpdatedAt != DateTime.MinValue)
					{
						jObject.Add("notificationsUpdatedAt", new JValue(account.NotificationsUpdatedAt));
					}
				}
				string email = account.Email;
				if (email != null && account.ServiceType == RemoteType.Bitbucket && account.AuthenticationType == AuthenticationType.AccessToken)
				{
					jObject.Add("email", new JValue(email));
				}
				return jObject;
			}

			private static JValue EncodeServiceType(RemoteType serviceType)
			{
				return serviceType switch
				{
					RemoteType.Bitbucket => new JValue("Bitbucket"), 
					RemoteType.BitbucketServer => new JValue("BitbucketServer"), 
					RemoteType.Gitea => new JValue("Gitea"), 
					RemoteType.Github => new JValue("GitHub"), 
					RemoteType.GithubEnterprise => new JValue("GitHubEnterprise"), 
					RemoteType.Gitlab => new JValue("GitLab"), 
					RemoteType.GitlabServer => new JValue("GitLabServer"), 
					_ => JValue.CreateNull(), 
				};
			}

			private static JValue EncodeAuthenticationType(AuthenticationType authenticationType)
			{
				return authenticationType switch
				{
					AuthenticationType.AccessToken => new JValue("AccessToken"), 
					AuthenticationType.OAuth => new JValue("OAuth"), 
					_ => JValue.CreateNull(), 
				};
			}

			public static Account[] DecodeAccountArray([Null] JArray jsonArray)
			{
				if (jsonArray == null)
				{
					return new Account[0];
				}
				List<Account> list = new List<Account>(jsonArray.Count);
				foreach (JToken item in jsonArray)
				{
					Account account = DecodeAccount(item as JObject);
					if (account != null)
					{
						list.Add(account);
					}
				}
				return list.ToArray();
			}

			[Null]
			private static Account DecodeAccount(JObject json)
			{
				if (json != null)
				{
					string @string = json.GetString("serviceType");
					if (@string != null)
					{
						string string2 = json.GetString("serverUrl");
						if (string2 != null)
						{
							string string3 = json.GetString("username");
							if (string3 != null)
							{
								string string4 = json.GetString("avatarUrl");
								string text = json.GetString("authenticationType") ?? "AccessToken";
								RemoteType remoteType;
								switch (@string)
								{
								case "Bitbucket":
									remoteType = RemoteType.Bitbucket;
									break;
								case "BitbucketServer":
									remoteType = RemoteType.BitbucketServer;
									break;
								case "Gitea":
									remoteType = RemoteType.Gitea;
									break;
								case "GitHub":
									remoteType = RemoteType.Github;
									break;
								case "GitHubEnterprise":
									remoteType = RemoteType.GithubEnterprise;
									break;
								case "GitLab":
									remoteType = RemoteType.Gitlab;
									break;
								case "GitLabServer":
									remoteType = RemoteType.GitlabServer;
									break;
								default:
									Log.Error("Cannot parse service type '" + @string + "'");
									return null;
								}
								bool enableNotifications = ((remoteType != RemoteType.Github && remoteType != RemoteType.GithubEnterprise) ? (json["enableNotifications"]?.Value<bool>() ?? false) : (json["enableNotifications"]?.Value<bool>() ?? true));
								DateTime? notificationsUpdatedAt = json["notificationsUpdatedAt"]?.Value<DateTime>();
								AuthenticationType authenticationType;
								if (!(text == "AccessToken"))
								{
									if (!(text == "OAuth"))
									{
										Log.Error("Cannot parse authentication type '" + text + "'");
										return null;
									}
									authenticationType = AuthenticationType.OAuth;
								}
								else
								{
									authenticationType = AuthenticationType.AccessToken;
								}
								string email = ((remoteType != 0 || authenticationType != 0) ? null : json.GetString("email"));
								return new Account(remoteType, authenticationType, string2, email, string3, string4, enableNotifications, notificationsUpdatedAt);
							}
						}
					}
				}
				Log.Error("Cannot parse account json");
				return null;
			}
		}

		public static readonly AccountManager Current = new AccountManager();

		private readonly object _syncpoint = new object();

		private Account[] _accounts = new Account[0];

		public Account[] Accounts
		{
			get
			{
				lock (_syncpoint)
				{
					return _accounts;
				}
			}
			private set
			{
				lock (_syncpoint)
				{
					_accounts = value;
				}
			}
		}

		public event EventHandler AccountsChanged;

		private AccountManager()
		{
			Accounts = Load();
		}

		public void AddOrUpdate(Account account)
		{
			Account[] accounts = Accounts;
			List<Account> list = new List<Account>(accounts.Length + 1);
			Account[] array = accounts;
			foreach (Account account2 in array)
			{
				if (!(account2.ServerUrl == account.ServerUrl) || !(account2.Username == account.Username))
				{
					list.Add(account2);
				}
			}
			list.Add(account);
			Accounts = list.ToArray();
			Save();
			this.AccountsChanged?.Invoke(this, EventArgs.Empty);
		}

		public void LogOut(Account account)
		{
			Account[] accounts = Accounts;
			List<Account> list = new List<Account>(accounts.Length + 1);
			Account[] array = accounts;
			foreach (Account account2 in array)
			{
				if (account2.ServerUrl == account.ServerUrl && account2.Username == account.Username)
				{
					account2.Service.Connection.Authentication.Destroy();
				}
				else
				{
					list.Add(account2);
				}
			}
			Accounts = list.ToArray();
			Save();
			this.AccountsChanged?.Invoke(this, EventArgs.Empty);
		}

		[Null]
		public IAccount FindAccount(string host, [Null] string username)
		{
			Account[] accounts = Accounts;
			if (username != null)
			{
				username = username.ToLower();
				Account[] array = accounts;
				foreach (Account account in array)
				{
					if (account.Host == host && account.Username.ToLower() == username)
					{
						return account;
					}
				}
			}
			else
			{
				Account[] array = accounts;
				foreach (Account account2 in array)
				{
					if (account2.Host == host)
					{
						return account2;
					}
				}
			}
			return null;
		}

		public void Save()
		{
			Log.Info("Save accounts");
			try
			{
				string content = Coder.Encode(Accounts).ToString(Formatting.Indented);
				Directory.CreateDirectory(App.ForkDirectoryPath);
				try
				{
					FileHelper.AtomicWrite(Path.Combine(App.ForkDirectoryPath, "accounts.json"), content);
				}
				catch (Exception ex)
				{
					Log.Error("Cannot save settings", ex);
				}
			}
			catch (Exception ex2)
			{
				Log.Error("Cannot save settings", ex2);
			}
		}

		private Account[] Load()
		{
			string path = Path.Combine(App.ForkDirectoryPath, "accounts.json");
			try
			{
				if (File.Exists(path))
				{
					return Coder.DecodeAccountArray(JsonConvert.DeserializeObject(File.ReadAllText(path)) as JArray);
				}
			}
			catch (Exception ex)
			{
				Log.Error("Cannot load accounts", ex);
			}
			return new Account[0];
		}
	}
}
