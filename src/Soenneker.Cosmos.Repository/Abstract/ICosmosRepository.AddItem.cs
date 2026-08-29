using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Will throw exception if item id already exists
    /// </summary>
    /// <param name="document">Document to read, persist, or update.</param>
    /// <param name="useQueue">Whether to enqueue the write for background execution instead of awaiting Redis directly.</param>
    /// <param name="excludeResponse">exclude Response returned by the upstream operation.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Fully qualified Id string (partitionKey:documentId)</returns>
    ValueTask<string> AddItem(TDocument document, bool useQueue = false, bool excludeResponse = false, CancellationToken cancellationToken = default);
}
