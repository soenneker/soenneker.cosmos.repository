using Microsoft.Azure.Cosmos;

namespace Soenneker.Cosmos.Repository.Abstract.Utils;

/// <summary>
/// Defines the container level context
/// </summary>
public interface ICosmosRepositoryContext
{
    bool AuditEnabled { get; }

    string ContainerName { get; }

    /// <summary>
    /// Container partition key must be able to be parsed from entity Id.
    /// </summary>
    /// <param name="entityId">A uniquely identifying string consisting of one or two substrings concatenated together and separated by ':'.</param>
    PartitionKey ResolvePartitionKey(string entityId);
}