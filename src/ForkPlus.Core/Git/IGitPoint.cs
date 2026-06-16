namespace ForkPlus.Git
{
    public interface IGitPoint
    {
        string ObjectName { get; }
        string FriendlyName { get; }
    }
}
