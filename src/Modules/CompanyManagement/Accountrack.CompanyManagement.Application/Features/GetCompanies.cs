using Accountrack.Application.Abstractions.Messaging;
using Accountrack.CompanyManagement.Application.Abstractions;
using Accountrack.CompanyManagement.Application.Contracts;
using Accountrack.CompanyManagement.Domain;
using Accountrack.SharedKernel.Results;

namespace Accountrack.CompanyManagement.Application.Features;

/// <summary>Lists companies in the current tenant.</summary>
public sealed record GetCompaniesQuery : IQuery<IReadOnlyList<CompanyDto>>;

public sealed class GetCompaniesQueryHandler : IQueryHandler<GetCompaniesQuery, IReadOnlyList<CompanyDto>>
{
    private readonly ICompanyRepository _companies;

    public GetCompaniesQueryHandler(ICompanyRepository companies) => _companies = companies;

    public async Task<Result<IReadOnlyList<CompanyDto>>> Handle(
        GetCompaniesQuery request, CancellationToken cancellationToken)
    {
        var companies = await _companies.ListForCurrentTenantAsync(cancellationToken);
        return Result.Success<IReadOnlyList<CompanyDto>>(companies.Select(c => c.ToDto()).ToList());
    }
}

public sealed record GetCompanyByIdQuery(Guid Id) : IQuery<CompanyDto>;

public sealed class GetCompanyByIdQueryHandler : IQueryHandler<GetCompanyByIdQuery, CompanyDto>
{
    private readonly ICompanyRepository _companies;

    public GetCompanyByIdQueryHandler(ICompanyRepository companies) => _companies = companies;

    public async Task<Result<CompanyDto>> Handle(GetCompanyByIdQuery request, CancellationToken cancellationToken)
    {
        var company = await _companies.GetByIdAsync(request.Id, cancellationToken);
        return company is null ? CompanyErrors.NotFound : company.ToDto();
    }
}
