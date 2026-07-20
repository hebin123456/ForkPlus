using ForkPlus.Services;

namespace ForkPlus.Accounts
{
    public static class GitServiceNotificationTargetTypeExtension
    {
        public static string GetIconKey(this GitServiceNotificationTargetType targetType)
        {
            return targetType switch
            {
                GitServiceNotificationTargetType.Commit => IconKeys.Commit,
                GitServiceNotificationTargetType.Issue => IconKeys.Issue,
                GitServiceNotificationTargetType.PullRequest => IconKeys.PullRequest,
                _ => IconKeys.Issue,
            };
        }
    }
}
