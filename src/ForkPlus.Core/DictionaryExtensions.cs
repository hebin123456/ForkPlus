using System;
using System.Collections.Generic;

namespace ForkPlus
{
    public static class DictionaryExtensions
    {
        public static TResult[] Map<TKey, TValue, TResult>(this Dictionary<TKey, TValue> source, Func<KeyValuePair<TKey, TValue>, TResult> selector)
        {
            TResult[] array = new TResult[source.Count];
            int num = 0;
            foreach (KeyValuePair<TKey, TValue> item in source)
            {
                array[num] = selector(item);
                num++;
            }
            return array;
        }
    }
}
