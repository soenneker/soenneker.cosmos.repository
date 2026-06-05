using Microsoft.Azure.Cosmos;

namespace Soenneker.Cosmos.Repository.Abstract.Utils;

/// <summary>
/// Defines the container level context
/// </summary>
public interface ICosmosRepositoryContext
{
    /// <summary>
    /// Should we create audit records for this repository event?
    /// </summary>
    bool AuditEnabled { get; }

    /// <summary>
    /// Name of the CosmosDB container
    /// </summary>
    string ContainerName { get; }

    /// <summary>
    /// Container partition key must be able to be parsed from entity Id.
    /// </summary>
    /// <param name="entityId">A uniquely identifying string consisting of one or two substrings concatenated together and separated by ':'.</param>
    PartitionKey ResolvePartitionKey(string entityId);
}