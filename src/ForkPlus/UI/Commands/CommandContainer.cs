namespace ForkPlus.UI.Commands
{
	public abstract class CommandContainer
	{
		protected static T Lazy<T>(ref T lazyObject) where T : new()
		{
			if (lazyObject == null)
			{
				lazyObject = new T();
			}
			return lazyObject;
		}
	}
}
