using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace ForkPlus.Accounts
{
	public class BasicAuthentication : PrivateAccessTokenAuthentication
	{
		public BasicAuthentication([Null] string serverUrl, string username, [Null] string token = null)
			: base(serverUrl, username, token)
		{
		}

		protected override void DoAuthorize(HttpRequestMessage request)
		{
			string text = Convert.ToBase64String(Encoding.UTF8.GetBytes(base.Username + ":" + Token));
			((HttpHeaders)request.Headers).Add("Authorization", "Basic " + text);
		}
	}
}
