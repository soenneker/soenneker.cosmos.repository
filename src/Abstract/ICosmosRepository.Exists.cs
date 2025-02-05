using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    [Pure]
    ValueTask<bool> Exists(string id, CancellationToken cancellationToken = default);

    [Pure]
    ValueTask<bool> Exists(IQueryable<TDocument> query, CancellationToken cancellationToken = default);

    [Pure]
    ValueTask<bool> ExistsByPartitionKey(string partitionKey, CancellationToken cancellationToken = default);
}