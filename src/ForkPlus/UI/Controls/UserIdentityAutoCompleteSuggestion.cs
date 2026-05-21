using ForkPlus.Git;

namespace ForkPlus.UI.Controls
{
	public class UserIdentityAutoCompleteSuggestion : AutoCompleteSuggestion
	{
		public UserIdentity UserIdentity { get; }

		public UserIdentityAutoCompleteSuggestion(Range range, UserIdentity userIdentity)
			: base(range, userIdentity.Name + " <" + userIdentity.Email + ">")
		{
			UserIdentity = userIdentity;
		}
	}
}
