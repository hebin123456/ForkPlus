namespace ForkPlus.Git
{
    public struct HandleEnumerator
    {
        private bool _isStash;
        private byte _gen;
        private int _cursor;
        private int _end;

        public RevisionStorage.Handle Current => new RevisionStorage.Handle(_cursor, _isStash, _gen);

        public HandleEnumerator(int start, int end, bool isStash, byte gen)
        {
            _cursor = start - 1;
            _end = end;
            _isStash = isStash;
            _gen = gen;
        }

        public bool MoveNext()
        {
            if (++_cursor < _end)
            {
                return true;
            }
            return false;
        }
    }
}
