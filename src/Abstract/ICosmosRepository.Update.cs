using System.Collections.Generic;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    // TODO: Add ModifiedAt within this method
    /// <returns>the same object passed to it</returns>
    ValueTask<TDocument> UpdateItem(TDocument document, bool useQueue = false, bool excludeResponse = false);

    ValueTask<List<TDocument>> UpdateItems(List<TDocument> documents, double? delayMs = null, bool useQueue = false, bool excludeResponse = false);

    /// <returns>the same object passed to it</returns>
    ValueTask<TDocument> UpdateItem(string id, TDocument document, bool useQueue = false, bool excludeResponse = false);
}