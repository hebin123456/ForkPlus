using ForkPlus.Services;

namespace ForkPlus.Git
{
 public static class RemoteTypeExtension
 {
  public static string GetIconKey(this RemoteType remoteType)
  {
   switch (remoteType)
   {
   case RemoteType.Azure:
   case RemoteType.Visualstudio:
    return IconKeys.Azure;
   case RemoteType.Bitbucket:
   case RemoteType.BitbucketServer:
    return IconKeys.Bitbucket;
   case RemoteType.Gitea:
    return IconKeys.Gitea;
   case RemoteType.Github:
   case RemoteType.GithubEnterprise:
    return IconKeys.GitHub;
   case RemoteType.Gitlab:
   case RemoteType.GitlabServer:
    return IconKeys.GitLab;
   default:
    return IconKeys.Remote;
   }
  }

  public static string GetIconGeometryKey(this RemoteType remoteType)
  {
   switch (remoteType)
   {
   case RemoteType.Azure:
   case RemoteType.Visualstudio:
    return IconKeys.AzureGeometry;
   case RemoteType.Bitbucket:
   case RemoteType.BitbucketServer:
    return IconKeys.BitbucketGeometry;
   case RemoteType.Gitea:
    return IconKeys.GiteaGeometry;
   case RemoteType.Github:
   case RemoteType.GithubEnterprise:
    return IconKeys.GitHubGeometry;
   case RemoteType.Gitlab:
   case RemoteType.GitlabServer:
    return IconKeys.GitLabGeometry;
   default:
    return IconKeys.RemoteGeometry;
   }
  }

  [Null]
  public static string FriendlyName(this RemoteType remoteType)
  {
   return remoteType switch
   {
    RemoteType.Azure => "Azure",
    RemoteType.Bitbucket => "Bitbucket",
    RemoteType.BitbucketServer => "Bitbucket Server",
    RemoteType.Gitea => "Gitea",
    RemoteType.Github => "GitHub",
    RemoteType.GithubEnterprise => "GitHub Enterprise Server",
    RemoteType.Gitlab => "GitLab",
    RemoteType.GitlabServer => "GitLab Server",
    RemoteType.Visualstudio => "Visualstudio",
    _ => null,
   };
  }
 }
}
