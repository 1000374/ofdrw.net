using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Collections.Generic
{
#if NET40
    public interface IReadOnlyCollection<out T> : IEnumerable<T>
    {
        int Count { get; }
    }

    public interface IReadOnlyList<out T> : IReadOnlyCollection<T>
    {
        T this[int index] { get; }
    }

    public interface IReadOnlyDictionary<TKey, TValue> : IReadOnlyCollection<KeyValuePair<TKey, TValue>>
    {
        TValue this[TKey key] { get; }
        IEnumerable<TKey> Keys { get; }
        IEnumerable<TValue> Values { get; }
        bool ContainsKey(TKey key);
        bool TryGetValue(TKey key, out TValue value);
    }

    internal sealed class ReadOnlyListAdapter<T> : IReadOnlyList<T>
    {
        private readonly IList<T> _inner;

        public ReadOnlyListAdapter(IList<T> inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public int Count => _inner.Count;

        public T this[int index] => _inner[index];

        public IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed class ReadOnlyCollectionAdapter<T> : IReadOnlyCollection<T>
    {
        private readonly ICollection<T> _inner;

        public ReadOnlyCollectionAdapter(ICollection<T> inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public int Count => _inner.Count;

        public IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed class ReadOnlyDictionaryAdapter<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    {
        private readonly IDictionary<TKey, TValue> _inner;

        public ReadOnlyDictionaryAdapter(IDictionary<TKey, TValue> inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public int Count => _inner.Count;

        public TValue this[TKey key] => _inner[key];

        public IEnumerable<TKey> Keys => _inner.Keys;

        public IEnumerable<TValue> Values => _inner.Values;

        public bool ContainsKey(TKey key) => _inner.ContainsKey(key);

        public bool TryGetValue(TKey key, out TValue value) => _inner.TryGetValue(key, out value);

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _inner.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal static class ReadOnlyCollectionExtensions
    {
        public static IReadOnlyList<T> AsReadOnlyList<T>(this IList<T> list)
        {
            if (list is null) throw new ArgumentNullException(nameof(list));
            return list as IReadOnlyList<T> ?? new ReadOnlyListAdapter<T>(list);
        }

        public static IReadOnlyCollection<T> AsReadOnlyCollection<T>(this ICollection<T> collection)
        {
            if (collection is null) throw new ArgumentNullException(nameof(collection));
            return collection as IReadOnlyCollection<T> ?? new ReadOnlyCollectionAdapter<T>(collection);
        }

        public static IReadOnlyDictionary<TKey, TValue> AsReadOnlyDictionary<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            if (dictionary is null) throw new ArgumentNullException(nameof(dictionary));
            return dictionary as IReadOnlyDictionary<TKey, TValue> ?? new ReadOnlyDictionaryAdapter<TKey, TValue>(dictionary);
        }
    }
#else
    internal static class ReadOnlyCollectionExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlyList<T> AsReadOnlyList<T>(this List<T> list) => list;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlyList<T> AsReadOnlyList<T>(this T[] array) => array;

        public static IReadOnlyList<T> AsReadOnlyList<T>(this IList<T> list)
        {
            if (list is IReadOnlyList<T> readOnly) return readOnly;
            return new List<T>(list);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlyCollection<T> AsReadOnlyCollection<T>(this List<T> list) => list;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlyCollection<T> AsReadOnlyCollection<T>(this T[] array) => array;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlyCollection<TKey> AsReadOnlyCollection<TKey, TValue>(this Dictionary<TKey, TValue>.KeyCollection keys) => keys;

        public static IReadOnlyCollection<T> AsReadOnlyCollection<T>(this ICollection<T> collection)
        {
            if (collection is IReadOnlyCollection<T> readOnly) return readOnly;
            return new List<T>(collection);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlyDictionary<TKey, TValue> AsReadOnlyDictionary<TKey, TValue>(this Dictionary<TKey, TValue> dictionary) => dictionary;

        public static IReadOnlyDictionary<TKey, TValue> AsReadOnlyDictionary<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            if (dictionary is IReadOnlyDictionary<TKey, TValue> readOnly) return readOnly;
            return new Dictionary<TKey, TValue>(dictionary);
        }
    }
#endif
}

namespace Ofdrw.Net.Core.Compatibility
{
    internal static class ArrayEmpty<T>
    {
#if NET40
        internal static readonly T[] Value = new T[0];
        internal static readonly IReadOnlyList<T> ReadOnlyList = new ReadOnlyListAdapter<T>(Value);
#else
        internal static T[] Value => System.Array.Empty<T>();
        internal static IReadOnlyList<T> ReadOnlyList => System.Array.Empty<T>();
#endif
    }

    internal static class TaskCompat
    {
#if NET40
        private static readonly Task s_completedTask = TaskEx.FromResult(0);
        public static Task CompletedTask => s_completedTask;

        public static Task<T> FromResult<T>(T result)
            => TaskEx.FromResult(result);

        public static Task Delay(int millisecondsTimeout, CancellationToken cancellationToken = default)
            => TaskEx.Delay(millisecondsTimeout, cancellationToken);

        public static Task WhenAll(params Task[] tasks)
            => TaskEx.WhenAll(tasks);

        public static Task<Task> WhenAny(params Task[] tasks)
            => TaskEx.WhenAny(tasks);
#else
        public static Task CompletedTask => Task.CompletedTask;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<T> FromResult<T>(T result)
            => Task.FromResult(result);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task Delay(int millisecondsTimeout, CancellationToken cancellationToken = default)
            => Task.Delay(millisecondsTimeout, cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task WhenAll(params Task[] tasks)
            => Task.WhenAll(tasks);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<Task> WhenAny(params Task[] tasks)
            => Task.WhenAny(tasks);
#endif
    }
}
