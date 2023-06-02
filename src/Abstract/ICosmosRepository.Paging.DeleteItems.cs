using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Soenneker.Constants.Data;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    ValueTask DeleteAllPaged(int pageSize = DataConstants.DefaultCosmosPageSize, double? delayMs = null, bool useQueue = false);

    ValueTask DeleteItemsPaged<T>(QueryDefinition queryDefinition, int pageSize = DataConstants.DefaultCosmosPageSize, double? delayMs = null, bool useQueue = false);
}