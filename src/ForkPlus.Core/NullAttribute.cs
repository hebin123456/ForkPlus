using System;

namespace ForkPlus
{
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Delegate)]
	internal sealed class NullAttribute : Attribute
	{
	}
}
