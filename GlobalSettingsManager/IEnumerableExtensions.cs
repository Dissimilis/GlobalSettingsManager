using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GlobalSettingsManager
{
    internal static class IEnumerableExtensions
    {
        public static List<T> ToNonNullList<T>(this IEnumerable<T> obj)
        {
            return obj == null ? new List<T>() : obj.ToList();
        }
        public static T[] ToNonNullArray<T>(this IEnumerable<T> obj)
        {
            return obj == null ? new T[0] : obj.ToArray();
        }
    }
}
