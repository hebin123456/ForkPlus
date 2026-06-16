namespace ForkPlus.Git
{
    public struct Head
    {
        public Sha DetachedHead { get; }

        [Null]
        public string Reference { get; }

        public Head(Sha detachedHead)
        {
            DetachedHead = detachedHead;
            Reference = null;
        }

        public Head(string reference)
        {
            DetachedHead = Sha.Zero;
            Reference = reference;
        }

        public override int GetHashCode()
        {
            int num = DetachedHead.GetHashCode();
            string reference = Reference;
            if (reference != null)
            {
                num += reference.GetHashCode() * 31;
            }
            return num;
        }
    }
}
