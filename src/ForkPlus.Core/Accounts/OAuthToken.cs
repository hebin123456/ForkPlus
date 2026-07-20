using System;

namespace ForkPlus.Accounts
{
	public class OAuthToken
	{
		public string Token { get; }

		[Null]
		public string RefreshToken { get; }

		[Null]
		public DateTime? ExpirationTimeUTC { get; }

		public OAuthToken(string token, [Null] string refreshToken, [Null] DateTime? expirationTimeUTC)
		{
			Token = token;
			RefreshToken = refreshToken;
			ExpirationTimeUTC = expirationTimeUTC;
		}
	}
}
