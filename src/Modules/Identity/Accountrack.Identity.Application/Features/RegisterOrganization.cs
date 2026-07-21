using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Identity.Application.Abstractions;
using Accountrack.Identity.Application.Contracts;
using Accountrack.Identity.Domain;
using Accountrack.Modules.Contracts.Company;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Identity.Application.Features;

/// <summary>
/// Public organization sign-up: provisions a brand-new tenant + first company, gives that company its
/// operating foundation (chart of accounts, fiscal periods, posting rules, baseline master data,
/// expense categories — BR-CMP-1), seeds the standard roles, creates the registrant as the tenant's
/// Administrator, and returns an auth token pair (auto sign-in). Anonymous endpoint — the only write
/// path that creates a tenant.
/// </summary>
public sealed record RegisterOrganizationCommand(
    string OrganizationName, string CompanyName, string FunctionalCurrency,
    string FullName, string Email, string Password) : ICommand<AuthResponse>;

public sealed class RegisterOrganizationValidator : AbstractValidator<RegisterOrganizationCommand>
{
    public RegisterOrganizationValidator()
    {
        RuleFor(x => x.OrganizationName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FunctionalCurrency).NotEmpty().Length(3);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}

public sealed class RegisterOrganizationHandler : ICommandHandler<RegisterOrganizationCommand, AuthResponse>
{
    private readonly ICompanyProvisioning _provisioning;
    private readonly IEnumerable<ICompanyFoundationSeeder> _foundationSeeders;
    private readonly IRoleRepository _roles;
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IIdentityUnitOfWork _uow;
    private readonly IClock _clock;

    public RegisterOrganizationHandler(
        ICompanyProvisioning provisioning,
        IEnumerable<ICompanyFoundationSeeder> foundationSeeders,
        IRoleRepository roles,
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IIdentityUnitOfWork uow,
        IClock clock)
    {
        _provisioning = provisioning;
        _foundationSeeders = foundationSeeders;
        _roles = roles;
        _users = users;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<AuthResponse>> Handle(RegisterOrganizationCommand request, CancellationToken ct)
    {
        var email = Email.Create(request.Email);

        // Check up-front so we don't provision a tenant we then can't attach an admin to.
        if (await _users.EmailExistsAsync(email.Value, ct))
        {
            return IdentityErrors.EmailAlreadyExists;
        }

        var tenantId = Guid.NewGuid();
        const int fiscalYearStartMonth = 1;
        var currency = request.FunctionalCurrency.Trim().ToUpperInvariant();

        var companyId = await _provisioning.ProvisionTenantAsync(
            tenantId, request.OrganizationName.Trim(), companyCode: "MAIN", request.CompanyName.Trim(),
            currency, fiscalYearStartMonth, timeZone: "Asia/Jakarta", ct);

        // Give the new company its operating foundation (BR-CMP-1): chart of accounts + fiscal periods
        // + posting rules, baseline master data, expense categories. Without this the tenant can sign in
        // but every GL-posting action (receive, invoice, pay, expense, stock adjustment) fails. Each
        // module contributes its own seeder, so boundaries stay intact (ADR-0007); all are idempotent,
        // and the startup backfill re-runs them for companies provisioned before this existed.
        var foundation = new CompanyFoundation(
            tenantId, companyId, currency, _clock.UtcNow.Year, fiscalYearStartMonth);
        foreach (var seeder in _foundationSeeders.OrderBy(s => s.Order))
        {
            await seeder.SeedAsync(foundation, ct);
        }

        // Seed the standard roles for the new tenant and make the registrant its Administrator.
        var permissionIdByCode = await _roles.GetPermissionIdByCodeAsync(ct);
        var roles = StandardRoleDefinitions.BuildSystemRoles(tenantId, permissionIdByCode).ToList();
        foreach (var role in roles)
        {
            _roles.Add(role);
        }

        var adminRole = roles.First(r => r.Name == SystemRoles.Administrator);

        var user = User.Create(tenantId, email, _passwordHasher.Hash(request.Password), request.FullName.Trim());
        user.AssignRole(adminRole.Id);
        user.GrantCompany(companyId);
        _users.Add(user);

        await _uow.SaveChangesAsync(ct);

        // Auto sign-in: full administrator access to the new company.
        var authData = await _users.GetAuthDataAsync(user.Id, ct);
        var response = AuthFactory.Issue(
            user, authData, familyId: Guid.NewGuid(), _tokenService, _refreshTokens, _clock);

        user.RecordLogin(_clock.UtcNow);
        await _uow.SaveChangesAsync(ct);

        return response;
    }
}
