using ForkPlus.Biturbo;

namespace ForkPlus.Git.Commands
{
	public class GetGitConfigGitCommand
	{
		public GitCommandResult<GitConfig> Execute(GitModule gitModule)
		{
			return Execute(gitModule.ConfigFilePath());
		}

		public GitCommandResult<GitConfig> Execute(string gitConfigPath)
		{
			return BtRequest.Run(() => default(BtGitConfig), delegate(ref BtGitConfig x)
			{
				return Bt.bt_get_git_config(gitConfigPath, ref x);
			}, delegate(ref BtGitConfig x)
			{
				return Into(ref x);
			}, delegate(ref BtGitConfig x)
			{
				Bt.bt_release_git_config(ref x);
			});
		}

		private GitCommandResult<GitConfig> Into(ref BtGitConfig btGitConfig)
		{
			return GitCommandResult<GitConfig>.Success(new GitConfig(btGitConfig.sections.GetStructArray(btGitConfig.sections_len, delegate(BtGitConfigSection bt_section)
			{
				string utf8String = bt_section.name.GetUtf8String();
				string utf8String2 = bt_section.sub_section.GetUtf8String();
				GitConfig.Variable[] structArray = bt_section.variables.GetStructArray(bt_section.variables_len, (BtGitConfigVariable bt_variable) => new GitConfig.Variable(bt_variable.name.GetUtf8String(), bt_variable.value.GetUtf8String()));
				return new GitConfig.Section(utf8String, utf8String2, structArray);
			})));
		}
	}
}
