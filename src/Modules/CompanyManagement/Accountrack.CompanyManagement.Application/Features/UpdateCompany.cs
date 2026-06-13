using Accountrack.Application.Abstractions.Messaging;
using Accountrack.CompanyManagement.Application.Abstractions;
using Accountrack.CompanyManagement.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.CompanyManagement.Application.Features;

public sealed record UpdateCompanyCommand(
    Guid Id,
    string Name,
    string? LegalName,
    string? TaxId,
    string TimeZone) : ICommand;

public sealed class UpdateCompanyCommandValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TimeZone).NotEmpty();
    }
}

public sealed class UpdateCompanyCommandHandler : ICommandHandler<UpdateCompanyCommand>
{
    private readonly ICompanyRepository _companies;
    private readonly ICompanyUnitOfWork _uow;

    public UpdateCompanyCommandHandler(ICompanyRepository companies, ICompanyUnitOfWork uow)
    {
        _companies = companies;
        _uow = uow;
    }

    public async Task<Result> Handle(UpdateCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = await _companies.GetByIdAsync(request.Id, cancellationToken);
        if (company is null)
        {
            return CompanyErrors.NotFound;
        }

        company.UpdateProfile(request.Name, request.LegalName, request.TaxId, request.TimeZone);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
