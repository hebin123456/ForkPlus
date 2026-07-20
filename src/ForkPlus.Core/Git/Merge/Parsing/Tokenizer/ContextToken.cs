using System.Diagnostics;

namespace ForkPlus.Git.Merge.Parsing.Tokenizer
{
	[DebuggerDisplay("[{Range.Start}..{Range.End}]  {RemoteType}-{LocalType}'{ContextString}'")]
	public class ContextToken : MergeToken
	{
		public ContextType RemoteType { get; }

		public ContextType LocalType { get; }

		public string ContextString { get; }

		public ContextToken(Range range, ContextType remoteType, ContextType localType, string contextString)
			: base(range)
		{
			RemoteType = remoteType;
			LocalType = localType;
			ContextString = contextString;
		}
	}
}
