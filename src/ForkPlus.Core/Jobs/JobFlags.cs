using System;

namespace ForkPlus.Jobs
{
	[Flags]
	public enum JobFlags
	{
		Hidden = 0,
		SaveToLog = 1,
		ShowOnToolbar = 2,
		ShowOnToolbarWhenFinished = 4,
		Background = 8,
		LongRunning = 0x10,
		Default = 7
	}
}
