using System;
using LanguageExt;
using static LanguageExt.Prelude;
using static Echo.Process;
using Echo;
using CacheState = LanguageExt.Map<string, (string value, System.DateTime time)>;

namespace Caching
{
    public static class ClassCaching
    {
        public static void Run()
        {
            Console.WriteLine("Class caching");

            var pid = spawn<Cache2>("cache");
            var proxy = proxy<ICache>(pid);

            // Add a new item to the cache
            proxy.Add("hello", "world");

            // Get an item from the cache
            var thing = proxy.Get("hello");

            Console.WriteLine(thing);

            // Find the number of items
            var count = proxy.Count();

            Console.WriteLine(count);

            // Remove an item from the cache
            proxy.Remove("hello");

            // Find the number of items
            count = proxy.Count();

            Console.WriteLine(count);

            proxy.Add("a", "X");
            proxy.Add("b", "Y");
            proxy.Add("c", "Z");

            // Find the number of items
            count = proxy.Count();

            Console.WriteLine(count);

            var item0 = proxy.ItemAt(0);
            Console.WriteLine(item0);

            var item1 = proxy.ItemAt(1);
            Console.WriteLine(item1);

            var item2 = proxy.ItemAt(2);
            Console.WriteLine(item2);

            // Flush the cache
            proxy.Flush(DateTime.Now);

            // Find the number of items
            count = proxy.Count();

            Console.WriteLine(count);
        }
    }

    public interface ICache
    {
        void Add(string key, string value);
        void Remove(string key);
        void Show(string key);
        string Get(string key);
        string ItemAt(int index);
        int Count();
        void Flush(DateTime cutOff);
    }

    public class Cache2 : ICache
    {
        CacheState state;

        public void Add(string key, string value) =>
            state = state.Add(key, (value, DateTime.UtcNow));

        public int Count() =>
            state.Count;

        public void Flush(DateTime cutOff) =>
            state = state.Filter(item => cutOff < item.time);

        public string Get(string key) =>
            (from pair in state.Find(key)
             let _ = state = Touch(state, key)
             select pair.value)
            .IfNone("");

        public string ItemAt(int index) =>
            (from pair in state.Skip(index).HeadOrNone()
             select Get(pair.Key))
            .IfNone("");

        public void Remove(string key) =>
            state = state.Remove(key);

        public void Show(string key) =>
            Console.WriteLine(state.Find(key).Map(toString).IfNone(""));

        public CacheState Touch(CacheState state, string key) =>
            state.TrySetItem(key, pair => (pair.value, DateTime.UtcNow));
    }
}
