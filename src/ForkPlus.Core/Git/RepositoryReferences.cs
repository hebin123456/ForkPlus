using System;
using System.Collections.Generic;
using System.Linq;
using ForkPlus.Git.Commands;

namespace ForkPlus.Git
{
    public class RepositoryReferences
    {
        public static readonly RepositoryReferences Empty = new RepositoryReferences(ReferenceStorage.Empty, null, new Reference[0], null, new string[0], new string[0], new string[0], new Symref[0], hideTags: false);

        private static readonly ReferenceComparer ReferenceComparer = new ReferenceComparer();

        public readonly Dictionary<string, ReferenceFilterState> Filter;
        public readonly Dictionary<Sha, int[]> ReferencesBySha;
        public ReferenceStorage ReferenceStorage { get; }
        public Sha? HeadSha { get; }
        public Reference[] Items { get; }
        public LocalBranch[] LocalBranches { get; }
        public RemoteBranch[] RemoteBranches { get; }
        public Tag[] Tags { get; }

        [Null]
        public LocalBranch ActiveBranch { get; }

        public bool IsFilterEnabled => FilterReferences.Length != 0;
        public bool IsHideEnabled => HiddenReferences.Length != 0;

        public string[] FilterReferences { get; }
        public string[] HiddenReferences { get; }
        public string[] PinnedReferences { get; }
        public Symref[] Symrefs { get; }
        public Reference[] Pinned { get; }
        public bool HideTags { get; }

        public static RepositoryReferences New(ReferenceStorage referenceStorage, string[] filterReferences, string[] hiddenReferences, string[] pinnedReferences, bool hideTags)
        {
            List<Reference> list = new List<Reference>();
            LocalBranch activeBranch = null;
            for (int i = 0; i < referenceStorage.Refs.Length; i++)
            {
                Sha sha = referenceStorage.Shas[i];
                bool flag = referenceStorage.ActiveBranchIndex == i;
                string fullReference = referenceStorage.Refs[i];
                string text = null;
                text = ((!referenceStorage.LocalBranches.Contains(i)) ? null : referenceStorage.GetLocalBranchUpstream(i));
                string dereferencedShaString = sha.ToString();
                DateTime committerDate = referenceStorage.GetCommitterDate(i) ?? UnixTime.UnixStartTime;
                Reference reference = Reference.Create(sha, flag, fullReference, text, dereferencedShaString, committerDate);
                if (reference != null && !(reference is RemoteBranch { ShortName: "HEAD" }))
                {
                    if (flag)
                    {
                        activeBranch = reference as LocalBranch;
                    }
                    list.Add(reference);
                }
            }
            list.Sort(ReferenceComparer.Compare);
            Symref[] array = new Symref[referenceStorage.Symrefs.Length];
            for (int j = 0; j < referenceStorage.Symrefs.Length; j++)
            {
                array[j] = new Symref(referenceStorage.Symrefs[j], referenceStorage.SymrefTargets[j]);
            }
            return new RepositoryReferences(referenceStorage, referenceStorage.HeadSha, list.ToArray(), activeBranch, filterReferences, hiddenReferences, pinnedReferences, array, hideTags);
        }

        private RepositoryReferences(ReferenceStorage referenceStorage, Sha? headSha, Reference[] references, [Null] LocalBranch activeBranch, string[] filterReferences, string[] hiddenReferences, string[] pinnedReferences, Symref[] symrefs, bool hideTags)
        {
            ReferenceStorage = referenceStorage;
            HeadSha = headSha;
            Items = references;
            ActiveBranch = activeBranch;
            Symrefs = symrefs;
            HideTags = hideTags;
            List<LocalBranch> list = new List<LocalBranch>(references.Length);
            List<RemoteBranch> list2 = new List<RemoteBranch>(references.Length);
            List<Tag> list3 = new List<Tag>(references.Length);
            List<Reference> list4 = new List<Reference>();
            Dictionary<string, ReferenceFilterState> dictionary = new Dictionary<string, ReferenceFilterState>();
            HashSet<string> hashSet = new HashSet<string>();
            List<string> list5 = new List<string>();
            HashSet<string> hashSet2 = new HashSet<string>();
            List<string> list6 = new List<string>();
            string[] array = filterReferences;
            foreach (string text in array)
            {
                if (text.EndsWith("/"))
                {
                    list5.Add(text);
                }
                else
                {
                    hashSet.Add(text);
                }
            }
            array = hiddenReferences;
            foreach (string text2 in array)
            {
                if (text2.EndsWith("/"))
                {
                    list6.Add(text2);
                }
                else
                {
                    hashSet2.Add(text2);
                }
            }
            List<string> list7 = new List<string>(pinnedReferences.Length);
            foreach (Reference reference2 in references)
            {
                if (reference2 is LocalBranch item)
                {
                    list.Add(item);
                }
                else if (reference2 is RemoteBranch item2)
                {
                    list2.Add(item2);
                }
                else if (reference2 is Tag item3)
                {
                    list3.Add(item3);
                }
                if (pinnedReferences.ContainsItem(reference2.FullReference))
                {
                    list4.Add(reference2);
                    list7.Add(reference2.FullReference);
                }
                foreach (string item4 in list5)
                {
                    if (reference2.FullReference.StartsWith(item4))
                    {
                        dictionary[reference2.FullReference] = ReferenceFilterState.InheritedFilter;
                    }
                }
                if (hashSet.Contains(reference2.FullReference))
                {
                    dictionary[reference2.FullReference] = ReferenceFilterState.Filter;
                }
                foreach (string item5 in list6)
                {
                    if (reference2.FullReference.StartsWith(item5))
                    {
                        dictionary[reference2.FullReference] = ReferenceFilterState.InheritedHide;
                    }
                }
                if (hashSet2.Contains(reference2.FullReference))
                {
                    dictionary[reference2.FullReference] = ReferenceFilterState.Hide;
                }
            }
            foreach (string item6 in list5)
            {
                dictionary[item6] = ReferenceFilterState.Filter;
            }
            foreach (string item7 in list6)
            {
                dictionary[item7] = ReferenceFilterState.Hide;
            }
            LocalBranches = list.ToArray();
            RemoteBranches = list2.ToArray();
            Tags = list3.ToArray();
            ReferencesBySha = references.GroupIndexes((Reference reference) => reference.Sha);
            Pinned = list4.ToArray();
            PinnedReferences = pinnedReferences;
            Filter = dictionary;
            FilterReferences = filterReferences;
            HiddenReferences = hiddenReferences;
        }

        public bool IsPinned(Reference reference)
        {
            if (PinnedReferences.Length == 0)
            {
                return false;
            }
            return Array.IndexOf(PinnedReferences, reference.FullReference) >= 0;
        }

        public bool IsFiltered(Reference reference)
        {
            return IsFiltered(reference.FullReference);
        }

        public bool IsFiltered(string fullReference)
        {
            ReferenceFilterState filterState = GetFilterState(fullReference);
            if (filterState != ReferenceFilterState.Filter)
            {
                return filterState == ReferenceFilterState.InheritedFilter;
            }
            return true;
        }

        public bool IsHidden(Reference reference)
        {
            return IsHidden(reference.FullReference);
        }

        public bool IsHidden(string fullReference)
        {
            ReferenceFilterState filterState = GetFilterState(fullReference);
            if (filterState != ReferenceFilterState.Hide)
            {
                return filterState == ReferenceFilterState.InheritedHide;
            }
            return true;
        }

        public ReferenceFilterState GetFilterState(string pattern)
        {
            if (Filter.TryGetValue(pattern, out var value))
            {
                return value;
            }
            return ReferenceFilterState.None;
        }
    }
}
