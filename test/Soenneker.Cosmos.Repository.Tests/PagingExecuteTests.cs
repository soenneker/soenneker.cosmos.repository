using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Soenneker.Documents.Document;

namespace Soenneker.Cosmos.Repository.Tests;

public class PagingExecuteTests
{
    [Test]
    public async Task ExecuteOnFeedIteratorProcessesEveryPageOnce(CancellationToken cancellationToken)
    {
        var iterator = new TestFeedIterator<int>([[1, 2], [3], [4, 5]]);
        var processedPages = new List<List<int>>();

        await CosmosRepository<Document>.ExecuteOnFeedIterator(iterator, page =>
        {
            processedPages.Add([.. page]);
            return ValueTask.CompletedTask;
        }, cancellationToken);

        await Assert.That(iterator.ReadCount).IsEqualTo(3);
        await Assert.That(processedPages).IsEquivalentTo(new List<List<int>>
        {
            new() {1, 2},
            new() {3},
            new() {4, 5}
        });
    }

    private sealed class TestFeedIterator<T>(IReadOnlyList<IReadOnlyList<T>> pages) : FeedIterator<T>
    {
        private int _pageIndex;

        public int ReadCount { get; private set; }

        public override bool HasMoreResults => _pageIndex < pages.Count;

        public override Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCount++;
            return Task.FromResult<FeedResponse<T>>(new TestFeedResponse<T>(pages[_pageIndex++]));
        }
    }

    private sealed class TestFeedResponse<T>(IReadOnlyList<T> items) : FeedResponse<T>
    {
        public override string ContinuationToken => null!;
        public override int Count => items.Count;
        public override string IndexMetrics => null!;
        public override Headers Headers => null!;
        public override IEnumerable<T> Resource => items;
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;
        public override CosmosDiagnostics Diagnostics => null!;

        public override IEnumerator<T> GetEnumerator()
        {
            return items.GetEnumerator();
        }
    }
}
