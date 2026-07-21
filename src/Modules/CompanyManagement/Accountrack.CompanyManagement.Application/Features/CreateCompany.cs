using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.CompanyManagement.Application.Abstractions;
using Accountrack.CompanyManagement.Domain;
using Accountrack.Modules.Contracts.Company;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.CompanyManagement.Application.Features;

public sealed record CreateCompanyCommand(
    string Code,
    string Name,
    string FunctionalCurrency,
    int FiscalYearStartMonth,
    string TimeZone) : ICommand<Guid>;

public sealed class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FunctionalCurrency).NotEmpty().Length(3);
        RuleFor(x => x.FiscalYearStartMonth).InclusiveBetween(1, 12);
        RuleFor(x => x.TimeZone).NotEmpty();
    }
}

public sealed class CreateCompanyCommandHandler : ICommandHandler<CreateCompanyCommand, Guid>
{
    private readonly ICompanyRepository _companies;
    private readonly ITenantContext _tenant;
    private readonly ICompanyUnitOfWork _uow;
    private readonly IEnumerable<ICompanyFoundationSeeder> _foundationSeeders;
    private readonly IClock _clock;

    public CreateCompanyCommandHandler(
        ICompanyRepository companies, ITenantContext tenant, ICompanyUnitOfWork uow,
        IEnumerable<ICompanyFoundationSeeder> foundationSeeders, IClock clock)
    {
        _companies = companies;
        _tenant = tenant;
        _uow = uow;
        _foundationSeeders = foundationSeeders;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(CreateCompanyCommand request, CancellationToken cancellationToken)
    {
        if (await _companies.CodeExistsAsync(request.Code.Trim(), cancellationToken))
        {
            return CompanyErrors.CodeAlreadyExists;
        }

        var company = Domain.Company.Create(
            _tenant.TenantId,
            request.Code,
            request.Name,
            request.FunctionalCurrency,
            request.FiscalYearStartMonth,
            request.TimeZone);

        _companies.Add(company);
        await _uow.SaveChangesAsync(cancellationToken);

        // An additional company needs the same operating foundation as the first one (BR-CMP-1):
        // chart of accounts, fiscal periods, posting rules, baseline master data. Without it every
        // GL-posting action in that company fails. Seeders are idempotent.
        var foundation = new CompanyFoundation(
            company.TenantId, company.Id, company.FunctionalCurrency, _clock.UtcNow.Year,
            company.FiscalYearStartMonth);
        foreach (var seeder in _foundationSeeders.OrderBy(s => s.Order))
        {
            await seeder.SeedAsync(foundation, cancellationToken);
        }

        return company.Id;
    }
}
