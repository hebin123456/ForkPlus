namespace ForkPlus
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
    public sealed class NullAttribute : Attribute
    {
    }
}
