using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.Git
{
    public static class UpstreamStatusExtensions
    {
        public static string ToLongDescription(this UpstreamStatus status, LocalBranch branch)
        {
            if (!status.IsValid)
            {
                return "";
            }
            if (status.Ahead > 0)
            {
                if (status.Behind > 0)
                {
                    return PreferencesLocalization.FormatCurrent("'{0}' {1} commits ahead, {2} commits behind '{3}'", branch.Name, status.Ahead, status.Behind, branch.UpstreamFullName);
                }
                return PreferencesLocalization.FormatCurrent("'{0}' {1} commits ahead '{2}'", branch.Name, status.Ahead, branch.UpstreamFullName);
            }
            if (status.Behind > 0)
            {
                return PreferencesLocalization.FormatCurrent("'{0}' {1} commits behind '{2}'", branch.Name, status.Behind, branch.UpstreamFullName);
            }
            return "";
        }
    }
}
