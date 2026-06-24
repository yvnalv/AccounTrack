using Accountrack.Application.Abstractions.Integration;

namespace Accountrack.Infrastructure.Common.Outbox;

/// <summary>
/// Scoped, settable <see cref="IAmbientTenant"/>. The outbox dispatcher sets it per message so the
/// originating tenant/company is restored for handlers running outside an HTTP request.
/// </summary>
public sealed class AmbientTenant : IAmbientTenant
{
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public bool IsSet { get; private set; }

    public void Set(Guid tenantId, Guid companyId)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        IsSet = true;
    }
}
