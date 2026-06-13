using Accountrack.Application.Abstractions.Messaging;
using Accountrack.CompanyManagement.Application.Abstractions;
using Accountrack.CompanyManagement.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.CompanyManagement.Application.Features;

/// <summary>Upserts a company setting (key/value) for the given company.</summary>
public sealed record SetCompanySettingCommand(Guid CompanyId, string Key, string Value) : ICommand;

public sealed class SetCompanySettingCommandValidator : AbstractValidator<SetCompanySettingCommand>
{
    public SetCompanySettingCommandValidator()
    {
        RuleFor(x => x.CompanyId).NotEmpty();
        RuleFor(x => x.Key).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Value).NotNull();
    }
}

public sealed class SetCompanySettingCommandHandler : ICommandHandler<SetCompanySettingCommand>
{
    private readonly ICompanyRepository _companies;
    private readonly ICompanyUnitOfWork _uow;

    public SetCompanySettingCommandHandler(ICompanyRepository companies, ICompanyUnitOfWork uow)
    {
        _companies = companies;
        _uow = uow;
    }

    public async Task<Result> Handle(SetCompanySettingCommand request, CancellationToken cancellationToken)
    {
        var company = await _companies.GetByIdAsync(request.CompanyId, cancellationToken);
        if (company is null)
        {
            return CompanyErrors.NotFound;
        }

        var existing = await _companies.GetSettingAsync(request.CompanyId, request.Key.Trim(), cancellationToken);
        if (existing is null)
        {
            _companies.AddSetting(new CompanySetting(request.Key, request.Value));
        }
        else
        {
            existing.SetValue(request.Value);
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
