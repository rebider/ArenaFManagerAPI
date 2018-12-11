using WitFX.MT4.Server.Implementation.Collections;

namespace WitFX.MT4.Server.Implementation.Extensions
{
    public static class ListIteratorExtensions
    {
        public static T1 first<T1, T2>(this List<(T1, T2)>.iterator it) => it.Current.Item1;
        public static T2 second<T1, T2>(this List<(T1, T2)>.iterator it) => it.Current.Item2;
        public static T1 first<T1, T2>(this (T1, T2) tuple) => tuple.Item1;
        public static T2 second<T1, T2>(this (T1, T2) tuple) => tuple.Item2;
    }
}
