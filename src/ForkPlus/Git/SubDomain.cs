using System;

namespace ForkPlus.Git
{
	[Flags]
	public enum SubDomain
	{
		None = 0,
		Head = 1,
		Remotes = 2,
		Stashes = 4,
		Submodules = 8,
		Worktrees = 0x10,
		GitFlowSettings = 0x20,
		BugtrackerSettings = 0x40,
		UserColors = 0x80,
		CustomCommands = 0x100,
		ReferenceSettings = 0x200,
		RevisionsSlim = 0x400,
		Revisions = 0xC00,
		References = 0x1000,
		State = 0x10000,
		ChangedFiles = 0x20000,
		UntrackedChangedFiles = 0x40000,
		RepositoryData = 0x1FFF,
		Status = 0x70000,
		All = 0x71FFF,
		DefaultRefresh = 0x717FF
	}
}
