using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    ValueTask<List<TDocument>> PatchItems(List<TDocument> documents, List<PatchOperation> operations, double? delayMs = null, bool useQueue = false, CancellationToken cancellationToken = default);

    ValueTask<TDocument?> PatchItem(string id, List<PatchOperation> operations, bool useQueue = false, CancellationToken cancellationToken = default);
}