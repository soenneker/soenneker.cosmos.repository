using System.Collections.Generic;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary> Will throw exception if item id already exists</summary>
    /// <returns>Fully qualified Id string (partitionKey:documentId)</returns>
    ValueTask<string> AddItem(TDocument document, bool useQueue = false, bool excludeResponse = false);

    /// <summary>
    /// Essentially just a helper that iterates over a list, calling <see cref="AddItem"/>
    /// </summary>
    ValueTask<List<TDocument>> AddItems(List<TDocument> documents, double? delayMs = null, bool useQueue = false, bool excludeResponse = false);
}