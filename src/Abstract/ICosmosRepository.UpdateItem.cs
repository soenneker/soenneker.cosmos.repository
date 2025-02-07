using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    // TODO: Add ModifiedAt within this method
    /// <returns>the same object passed to it</returns>
    ValueTask<TDocument> UpdateItem(TDocument document, bool useQueue = false, bool excludeResponse = false, CancellationToken cancellationToken = default);

    /// <returns>the same object passed to it</returns>
    ValueTask<TDocument> UpdateItem(string id, TDocument document, bool useQueue = false, bool excludeResponse = false, CancellationToken cancellationToken = default);
}