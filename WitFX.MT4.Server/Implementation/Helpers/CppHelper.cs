using System;
using System.Diagnostics;
using System.Threading;
using Sys = System.Collections.Generic;

namespace WitFX.MT4.Server.Implementation.Helpers
{
    /// <summary>
    /// https://msdn.microsoft.com/en-us/library/windows/desktop/ms687032(v=vs.85).aspx
    /// </summary>
    public enum WaitResult : long
    {
        WAIT_TIMEOUT = 0x00000102L,
        WAIT_OBJECT_0 = 0x00000000L,
        WAIT_ABANDONED = 0x00000080L,
        WAIT_FAILED = 0xFFFFFFFF
    }

    internal static class CppHelper
    {
        internal static void free<T>(T value) { }

        internal static EventWaitHandle CreateEvent(bool bManualReset, bool bInitialState)
        {
            if (bManualReset)
                return new ManualResetEvent(bInitialState);

            return new AutoResetEvent(bInitialState);
        }

        internal static void SetEvent(EventWaitHandle @event)
            => @event.Set();

        internal static void Sleep(int dwMilliseconds)
            => Thread.Sleep(dwMilliseconds);

        internal static int INFINITE = Timeout.Infinite;

        internal static WaitResult WaitForSingleObject(Thread thread, int timeout)
            => thread.Join(timeout) ? WaitResult.WAIT_OBJECT_0 : WaitResult.WAIT_TIMEOUT;

        internal static WaitResult WaitForSingleObject(EventWaitHandle @event, int timeout)
        {
            try
            {
                return @event.WaitOne(timeout) ? WaitResult.WAIT_OBJECT_0 : WaitResult.WAIT_TIMEOUT;
            }
            catch (AbandonedMutexException)
            {
                return WaitResult.WAIT_ABANDONED;
            }
            catch (InvalidOperationException)
            {
                return WaitResult.WAIT_FAILED;
            }
        }

        internal static void CloseHandle(Thread thread)
            => Debug.Assert(!thread.IsAlive);

        internal static void CloseHandle(EventWaitHandle @event)
            => @event.Close();

        internal static void COPY_STR_S(out string destination, string source)
            => destination = source;

        internal static string PrintF(string format, params object[] args)
        {
            // https://gist.github.com/dbrockman/2470835

            if (args == null || args.Length == 0)
                return format;

            int[] i = { 0 };

            string s = System.Text.RegularExpressions.Regex.Replace(
                format, "%[usdi%]",
                match => match.Value == "%%" ? "%" : i[0] < args.Length ? args[i[0]++].ToString() : match.Value);

            for (; i[0] < args.Length; i[0]++)
                s += " " + args[i[0]];

            Debug.Assert(s.IndexOf('%') == -1);
            return s;
        }

        //internal static void _snprintf(out string destination, string format, params object[] args)
        //    => destination = PrintF(format, args);

        internal static void memcpy(out string destination, string source)
            => destination = source;

        internal static void memcpy<T>(ref T destination, T source) where T : class
        {
            Debug.Assert(source != null);

            if (source == null)
                throw new ArgumentNullException(nameof(source));

            void CheckType(Type t)
            {
                Debug.Assert(!t.IsValueType);

                if (t.IsValueType)
                    throw new InvalidOperationException();

                Debug.Assert(t != typeof(object));
            }

            Type type;

            if (destination == null)
            {
                CheckType(type = source.GetType());
                destination = (T)Activator.CreateInstance(type);
            }
            else
                CheckType(type = destination.GetType());

            var props = type.GetFields();
            Debug.Assert(props.Length > 0);

            if (props.Length == 0)
                throw new InvalidOperationException();

            foreach (var prop in props)
            {
                var value = prop.GetValue(source);

                if (value != null)
                {
                    var valueType = value.GetType();
                    Debug.Assert(valueType.IsValueType || valueType == typeof(string));

                    if (!valueType.IsValueType && valueType != typeof(string))
                        throw new InvalidOperationException();
                }

                prop.SetValue(destination, value);
            }

            Debug.Assert(type.GetProperties().Length == 0);
        }

        internal static void strcpy(out string destination, string source)
            => destination = source;

        internal static bool strstr(string str1, string str2)
            => str1.IndexOf(str2) != -1;

        internal static double abs(double value) => Math.Abs(value);

        internal static Collections.List<T>.iterator find<T>(Collections.List<T>.iterator first, Collections.List<T>.iterator last, T val)
        {
            if (!first.IsAvailable)
                return first;

            var list = first.List;
            int lastIndex;

            if (last.IsAvailable)
            {
                Debug.Assert(last.List == list);
                lastIndex = last.Index;
            }
            else
                lastIndex = list.Count;

            var comparer = Sys.EqualityComparer<T>.Default;

            for (var i = first.Index; i < lastIndex; i++)
                if (comparer.Equals(list[i], val))
                    return new Collections.List<T>.iterator(list, i);

            return last;
        }

        internal static double pow(double x, double y) => Math.Pow(x, y);
        internal static double floor(double x) => Math.Floor(x);
        internal static int abs(int x) => Math.Abs(x);
    }
}
