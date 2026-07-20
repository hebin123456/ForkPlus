namespace ForkPlus.Git
{
	public class SymbolicReference : IGitPoint
	{
		public string ObjectName { get; }

		public string FriendlyName { get; }

		public SymbolicReference(string objectName, [Null] string friendlyName = null)
		{
			ObjectName = objectName;
			FriendlyName = friendlyName ?? objectName;
		}
	}
}
