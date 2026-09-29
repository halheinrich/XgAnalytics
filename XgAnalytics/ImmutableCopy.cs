using System.Collections.Immutable;

namespace XgAnalytics;

/// <summary>
/// The one way this library's result records take in a collection: an
/// immutable copy, made where the record is built. The record then hands out
/// nothing its caller can change afterwards, and nothing a cast can write — an
/// <see cref="ImmutableArray{T}"/> seen as an <see cref="IReadOnlyList{T}"/>,
/// or an <see cref="ImmutableDictionary{TKey, TValue}"/> seen as an
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/>, is still immutable.
/// </summary>
internal static class ImmutableCopy
{
    /// <summary>
    /// An immutable copy of <paramref name="source"/> in its order, boxed once
    /// as the list the record holds and hands out.
    /// </summary>
    /// <param name="source">The collection a caller passed.</param>
    /// <param name="paramName">The member the refusal names.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    internal static IReadOnlyList<T> Of<T>(IEnumerable<T>? source, string paramName) =>
        source is null
            ? throw new ArgumentNullException(paramName)
            : ImmutableArray.CreateRange(source);

    /// <summary>
    /// An immutable copy of <paramref name="source"/>, under the key type's
    /// default equality, as the dictionary the record holds and hands out.
    /// </summary>
    /// <param name="source">The dictionary a caller passed.</param>
    /// <param name="paramName">The member the refusal names.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    internal static IReadOnlyDictionary<TKey, TValue> OfDictionary<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue>? source, string paramName)
        where TKey : notnull =>
        source is null
            ? throw new ArgumentNullException(paramName)
            : ImmutableDictionary.CreateRange(source);
}
