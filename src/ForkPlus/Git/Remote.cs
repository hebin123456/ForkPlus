using System;
using System.Collections.Generic;
using ForkPlus.Accounts;

namespace ForkPlus.Git
{
	public partial class Remote
	{
		public sealed class RemoteComparer : IComparer<Remote>
		{
			private readonly StringComparer _stringComparer;

			public RemoteComparer(StringComparer stringComparer)
			{
				_stringComparer = stringComparer;
			}

			public int Compare(Remote x, Remote y)
			{
				int num = _stringComparer.Compare(x.Name, y.Name);
				if (num != 0)
				{
					return num;
				}
				int num2 = StringComparer.Ordinal.Compare(x.Url, y.Url);
				if (num2 != 0)
				{
					return num2;
				}
				int num3 = x.DisableImplicitFetch.CompareTo(y.DisableImplicitFetch);
				if (num3 != 0)
				{
					return num3;
				}
				int num4 = Compare(x.Account, y.Account);
				if (num4 != 0)
				{
					return num4;
				}
				return 0;
			}

			private static int Compare([Null] Account x, [Null] Account y)
			{
				if (x == null)
				{
					if (y == null)
					{
						return 0;
					}
					return 1;
				}
				if (y == null)
				{
					return -1;
				}
				if (x == y)
				{
					return 0;
				}
				return x.GetHashCode().CompareTo(y.GetHashCode());
			}
		}

		public static readonly RemoteComparer Comparer = new RemoteComparer(StringComparer.Ordinal);

		public static readonly RemoteComparer ComparerIgnoreCaseNumeric = new RemoteComparer(NumericIgnoreCaseStringComparer.Comparer);

		public string Name { get; }

		public string Url { get; }

		public bool DisableImplicitFetch { get; }

		[Null]
		public Account Account { get; }

		public GitUrl GitUrl { get; }

		public RemoteType RemoteType { get; }

		public string IconKey => RemoteType.GetIconKey();

		public string IconGeometryKey => RemoteType.GetIconGeometryKey();

		public Remote(string name, string url, bool disableImplicitFetch, [Null] Account account)
		{
			Name = name;
			Url = url;
			DisableImplicitFetch = disableImplicitFetch;
			Account = account;
			GitUrl = new GitUrl(url);
			RemoteType = account?.ServiceType ?? GitUrl.RemoteType;
		}

		public bool DataEquals(Remote other)
		{
			if (Name == other.Name && Url == other.Url && DisableImplicitFetch == other.DisableImplicitFetch)
			{
				return Account == other.Account;
			}
			return false;
		}
	}
}
