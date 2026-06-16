using System;

namespace ForkPlus.Git
{
    public struct RevisionHeader
    {
        public UserIdentity Author { get; }
        public DateTime AuthorDate { get; }
        public string Message { get; }
        public bool HasBody { get; }

        public RevisionHeader(UserIdentity author, DateTime authorDate, string message, bool hasBody)
        {
            Author = author;
            AuthorDate = authorDate;
            Message = message;
            HasBody = hasBody;
        }
    }
}
