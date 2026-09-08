using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.Git.Commands
{
	public class AddSubmoduleGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string submoduleUrl, string submodulePath, JobMonitor monitor)
		{
			// git ≥ 2.38.1 出于安全默认禁止 file transport（protocol.file.allow=user），本地路径子模块
		// 一律失败（"fatal: transport 'file' not allowed"）。AddSubmoduleWindow 明确支持本地路径 URL
		// （剪贴板识别还专门检测本地目录），此处按用户显式输入的意图放开该限制（模块16 E2E 实证修复）。
		GitCommand command = new GitCommand(App.OverrideCredentialHelper, "-c", "protocol.file.allow=always", "submodule", "add", "--force", "--progress", submoduleUrl, submodulePath.Quotify());
			monitor.Update(0.0, PreferencesLocalization.FormatCurrent("Adding '{0}'", PathHelper.GetReadableFileName(submodulePath)));
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).ExecuteLong(delegate(string stdOutLine)
			{
				monitor.AppendOutputLine(stdOutLine);
			}, delegate(string stdErrLine)
			{
				if (!monitor.HandleGitProgress(stdErrLine))
				{
					monitor.AppendOutputLine(stdErrLine);
				}
			}, monitor);
			monitor.Success(PreferencesLocalization.FormatCurrent("Added '{0}'", PathHelper.GetReadableFileName(submodulePath)));
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
