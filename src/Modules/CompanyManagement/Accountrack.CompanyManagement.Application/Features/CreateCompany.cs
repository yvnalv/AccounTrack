using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.CompanyManagement.Application.Abstractions;
using Accountrack.CompanyManagement.Domain;
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

    public CreateCompanyCommandHandler(
        ICompanyRepository companies, ITenantContext tenant, ICompanyUnitOfWork uow)
    {
        _companies = companies;
        _tenant = tenant;
        _uow = uow;
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

        return company.Id;
    }
}
