using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace ForkPlus.Accounts
{
	public class BitbucketBasicAuthentication : PrivateAccessTokenAuthentication
	{
		private string _email;

		public BitbucketBasicAuthentication([Null] string serverUrl, string email, string username, [Null] string token = null)
			: base(serverUrl, username, token)
		{
			_email = email;
		}

		protected override void DoAuthorize(HttpRequestMessage request)
		{
			string text = Convert.ToBase64String(Encoding.UTF8.GetBytes(_email + ":" + Token));
			((HttpHeaders)request.Headers).Add("Authorization", "Basic " + text);
		}
	}
}
