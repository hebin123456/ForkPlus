namespace ForkPlus.Utils.Http
{
	public static class AuthenticationTypeExtensions
	{
		public static string FriendlyName(this AuthenticationType authenticationType)
		{
			return authenticationType switch
			{
				AuthenticationType.AccessToken => "Access Token", 
				AuthenticationType.OAuth => "OAuth", 
				_ => "", 
			};
		}
	}
}
