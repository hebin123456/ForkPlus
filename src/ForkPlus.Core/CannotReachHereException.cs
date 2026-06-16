namespace ForkPlus
{
    public sealed class CannotReachHereException : Exception
    {
        public CannotReachHereException()
        {
        }

        public CannotReachHereException(string message)
            : base(message)
        {
        }
    }
}
