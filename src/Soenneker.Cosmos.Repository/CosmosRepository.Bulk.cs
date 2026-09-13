using System;
using System.Collections;
using System.Collections.Generic;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument>
{
    // One range and one closure per batch replace the per-document state array.
    private sealed class IndexRange(int count) : IReadOnlyList<int>
    {
        public int Count => count;

        public int this[int index]
        {
            get
            {
                ArgumentOutOfRangeException.ThrowIfNegative(index);
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, count);
                return index;
            }
        }

        public IEnumerator<int> GetEnumerator()
        {
            for (int i = 0; i < count; i++)
                yield return i;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
