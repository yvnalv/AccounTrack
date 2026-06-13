using Accountrack.SharedKernel.Results;

namespace Accountrack.MasterData.Domain;

public static class MasterDataErrors
{
    public static Error CodeExists(string entity) =>
        Error.Conflict($"MASTERDATA.{entity.ToUpperInvariant()}_CODE_EXISTS", $"A {entity} with this code already exists.");

    public static Error NotFound(string entity) =>
        Error.NotFound($"MASTERDATA.{entity.ToUpperInvariant()}_NOT_FOUND", $"{entity} not found.");

    public static readonly Error UomNotFound =
        Error.Validation("MASTERDATA.UOM_NOT_FOUND", "The specified unit of measure does not exist.");
}
