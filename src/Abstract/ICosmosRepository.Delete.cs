using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Soenneker.Dtos.IdPartitionPair;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Hard deletes one item by Id (partition and document, or one guid if they're the same).
    /// Will not throw.
    /// </summary>
    ValueTask DeleteItem(string entityId, bool useQueue = false);

    ValueTask DeleteItem(string documentId, string partitionKey, bool useQueue = false);

    /// <remarks>TODO: Perhaps want to turn on Bulk support https://devblogs.microsoft.com/cosmosdb/introducing-bulk-support-in-the-net-sdk/</remarks>
    ValueTask DeleteAll(double? delayMs = null, bool useQueue = false);

    ValueTask DeleteItems(IQueryable<TDocument> queryable, double? delayMs = null, bool useQueue = false);

    ValueTask DeleteIds(List<IdPartitionPair> ids, double? delayMs = null, bool useQueue = false);
}