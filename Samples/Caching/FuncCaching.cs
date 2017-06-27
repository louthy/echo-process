using System;
using LanguageExt;
using static LanguageExt.Prelude;
using static Echo.Process;
using Echo;

using CacheState = LanguageExt.Map<string, (string value, System.DateTime time)>;

namespace Caching
{
    public static class FuncCaching
    {
        public static void Run()
        {
            Console.WriteLine("Func caching");

            var pid = spawn<CacheState, object>("cache", CacheProcess.Setup, CacheProcess.Inbox);

            // Add a new item to the cache
            Cache.Add(pid, "hello", "world");

            // Get an item from the cache
            var thing = Cache.Get(pid, "hello", "");

            Console.WriteLine(thing);

            // Find the number of items
            var count = Cache.Count(pid);

            Console.WriteLine(count);

            // Remove an item from the cache
            Cache.Remove(pid, "hello");

            // Find the number of items
            count = Cache.Count(pid);

            Console.WriteLine(count);

            Cache.Add(pid, "a", "1");
            Cache.Add(pid, "b", "2");
            Cache.Add(pid, "c", "3");

            // Find the number of items
            count = Cache.Count(pid);

            Console.WriteLine(count);

            var item0 = Cache.ItemAt(pid, 0);
            Console.WriteLine(item0);

            var item1 = Cache.ItemAt(pid, 1);
            Console.WriteLine(item1);

            var item2 = Cache.ItemAt(pid, 2);
            Console.WriteLine(item2);

            // Flush the cache
            Cache.Flush(pid, DateTime.Now);

            // Find the number of items
            count = Cache.Count(pid);

            Console.WriteLine(count);
        }
    }

    public class Add    : NewType<Add, (string, string)> { public Add((string Key, string Value) pair)   : base(pair) { } }
    public class Get    : NewType<Get, (string, string)> { public Get((string Key, string Default) pair) : base(pair) { } }
    public class Remove : NewType<Remove, string>        { public Remove(string key)                     : base(key) { } }
    public class Show   : NewType<Show, string>          { public Show(string key)                       : base(key) { } }
    public class GetCount { }

    public static class Cache
    {
        public static Unit Add(ProcessId pid, string key, string value) =>
            tell(pid, Caching.Add.New((key, value)));

        public static string Get(ProcessId pid, string key, string defaultValue) =>
            ask<string>(pid, Caching.Get.New((key, defaultValue)));

        public static Unit Remove(ProcessId pid, string key) =>
            tell(pid, Caching.Remove.New(key));

        public static Unit Show<K>(ProcessId pid, string key) =>
            tell(pid, Caching.Show.New(key));

        public static string ItemAt(ProcessId pid, int index) =>
            ask<string>(pid, index);

        public static int Count(ProcessId pid) =>
            ask<int>(pid, unit);

        public static Unit Flush(ProcessId pid, DateTime cutOff) =>
            tell(pid, cutOff);
    }

    public static class CacheProcess
    {
        public static CacheState Setup() => default(CacheState);

        public static CacheState Inbox(CacheState state, object msg) =>
            msg is Add a      ? Add(state, ((string, string))a)
          : msg is Remove r   ? Remove(state, r)
          : msg is Show s     ? Show(state, s)
          : msg is Get g      ? Get(state, ((string, string))g)
          : msg is int i      ? GetIndex(state, i)
          : msg is Unit _     ? GetCount(state)
          : msg is DateTime d ? Flush(state, d)
          : state;

        static CacheState Add(CacheState state, (string key, string value) pair) =>
            state.AddOrUpdate(pair.key, (pair.value, DateTime.UtcNow));

        static CacheState Remove(CacheState state, Remove msg) =>
            state.Remove((string)msg);

        static CacheState GetIndex(CacheState state, int index) =>
            state.Skip(index)
                 .HeadOrNone()
                 .Match(
                     Some: item =>
                     {
                         reply(item.Value.value);
                         return state.SetItem(item.Key, (item.Value.value, DateTime.UtcNow));
                     },
                     None: () => state);

        static CacheState GetCount(CacheState state)
        {
            reply(state.Count);
            return state;
        }

        static CacheState Get(CacheState state, (string Key, string DefaultValue) pair) =>
            state.Find(pair.Key)
                    .Match(
                    Some: item =>
                    {
                        reply(item.value);
                        return state.SetItem(pair.Key, (item.value, DateTime.UtcNow));
                    },
                    None: () =>
                    {
                        reply(pair.DefaultValue);
                        return state;
                    });

        static CacheState Show(CacheState state, Show msg)
        {
            Console.WriteLine(state.Find((string)msg)
                                   .Map(toString)
                                   .IfNone($"Item doesn't exist '{msg}'"));
            return state;
        }

        static CacheState Flush(Map<string, (string, DateTime time)> state, DateTime cutOff) =>
            state.Filter(item => item.time < cutOff);
     }
}
