using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary> Will throw exception if item id already exists</summary>
    /// <returns>Fully qualified Id string (partitionKey:documentId)</returns>
    ValueTask<string> AddItem(TDocument document, bool useQueue = false, bool excludeResponse = false, CancellationToken cancellationToken = default);
}