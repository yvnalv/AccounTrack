using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Application.Contracts;
using Accountrack.Accounting.Domain;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Accounting.Application.Features;

// --- List ---

public sealed record GetPostingRulesQuery : IQuery<IReadOnlyList<PostingRuleDto>>;

public sealed class GetPostingRulesQueryHandler : IQueryHandler<GetPostingRulesQuery, IReadOnlyList<PostingRuleDto>>
{
    private readonly IPostingRuleRepository _rules;
    private readonly IAccountRepository _accounts;

    public GetPostingRulesQueryHandler(IPostingRuleRepository rules, IAccountRepository accounts)
    {
        _rules = rules;
        _accounts = accounts;
    }

    public async Task<Result<IReadOnlyList<PostingRuleDto>>> Handle(GetPostingRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await _rules.ListAsync(cancellationToken);
        var accounts = await _accounts.GetByIdsAsync(rules.Select(r => r.AccountId).Distinct().ToArray(), cancellationToken);

        var dtos = rules
            .OrderBy(r => r.RuleKey).ThenByDescending(r => r.Specificity)
            .Select(r =>
            {
                accounts.TryGetValue(r.AccountId, out var a);
                return new PostingRuleDto(
                    r.Id, r.EventType, r.RuleKey, r.AccountId, a?.Code ?? "", a?.Name ?? "",
                    r.ProductCategoryId, r.WarehouseId, r.TaxCodeId, r.BankAccountId, r.IsActive);
            })
            .ToList();

        return dtos;
    }
}

// --- Upsert ---

/// <summary>Creates or repoints a posting rule. Omit <c>EventType</c> for the company-wide default.</summary>
public sealed record SetPostingRuleCommand(
    string? EventType, string RuleKey, Guid AccountId,
    Guid? ProductCategoryId = null, Guid? WarehouseId = null, Guid? TaxCodeId = null, Guid? BankAccountId = null)
    : ICommand<Guid>;

public sealed class SetPostingRuleCommandValidator : AbstractValidator<SetPostingRuleCommand>
{
    public SetPostingRuleCommandValidator()
    {
        RuleFor(x => x.RuleKey).NotEmpty().MaximumLength(64);
        RuleFor(x => x.EventType).MaximumLength(128);
        RuleFor(x => x.AccountId).NotEmpty();
    }
}

public sealed class SetPostingRuleCommandHandler : ICommandHandler<SetPostingRuleCommand, Guid>
{
    private readonly IPostingRuleRepository _rules;
    private readonly IAccountRepository _accounts;
    private readonly IAccountingUnitOfWork _uow;

    public SetPostingRuleCommandHandler(IPostingRuleRepository rules, IAccountRepository accounts, IAccountingUnitOfWork uow)
    {
        _rules = rules;
        _accounts = accounts;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(SetPostingRuleCommand request, CancellationToken cancellationToken)
    {
        var account = await _accounts.GetByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
        {
            return AccountingErrors.AccountNotFound;
        }

        if (!account.IsActive || !account.AllowPosting)
        {
            return AccountingErrors.AccountNotPostable(account.Code);
        }

        var eventType = string.IsNullOrWhiteSpace(request.EventType) ? PostingRule.AnyEvent : request.EventType.Trim();
        var selector = new PostingSelector(request.ProductCategoryId, request.WarehouseId, request.TaxCodeId, request.BankAccountId);

        var existing = await _rules.FindAsync(eventType, request.RuleKey.Trim(), selector, cancellationToken);
        if (existing is not null)
        {
            existing.Repoint(request.AccountId);
            existing.Activate();
            await _uow.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }

        var rule = PostingRule.Create(
            eventType, request.RuleKey, request.AccountId,
            request.ProductCategoryId, request.WarehouseId, request.TaxCodeId, request.BankAccountId);
        _rules.Add(rule);
        await _uow.SaveChangesAsync(cancellationToken);
        return rule.Id;
    }
}

// --- Health check ---

public sealed record GetPostingRuleHealthQuery : IQuery<PostingRuleHealthDto>;

public sealed class GetPostingRuleHealthQueryHandler : IQueryHandler<GetPostingRuleHealthQuery, PostingRuleHealthDto>
{
    private readonly IPostingRuleRepository _rules;
    private readonly IAccountRepository _accounts;

    public GetPostingRuleHealthQueryHandler(IPostingRuleRepository rules, IAccountRepository accounts)
    {
        _rules = rules;
        _accounts = accounts;
    }

    public async Task<Result<PostingRuleHealthDto>> Handle(GetPostingRuleHealthQuery request, CancellationToken cancellationToken)
    {
        var rules = await _rules.ListAsync(cancellationToken);
        var accounts = await _accounts.GetByIdsAsync(rules.Select(r => r.AccountId).Distinct().ToArray(), cancellationToken);

        var issues = new List<PostingRuleHealthIssue>();

        foreach (var key in PostingRuleKeys.Required)
        {
            // The fallback the engine relies on: a company-wide default rule (no event, no selectors).
            var rule = rules.FirstOrDefault(r =>
                string.Equals(r.RuleKey, key, StringComparison.OrdinalIgnoreCase)
                && r.EventType == PostingRule.AnyEvent
                && r is { ProductCategoryId: null, WarehouseId: null, TaxCodeId: null, BankAccountId: null }
                && r.IsActive);

            if (rule is null)
            {
                issues.Add(new PostingRuleHealthIssue(key, "No default posting rule is configured."));
                continue;
            }

            if (!accounts.TryGetValue(rule.AccountId, out var account))
            {
                issues.Add(new PostingRuleHealthIssue(key, "Rule points to an account that does not exist."));
                continue;
            }

            if (!account.IsActive || !account.AllowPosting)
            {
                issues.Add(new PostingRuleHealthIssue(key, $"Account {account.Code} is inactive or not postable."));
            }

            if (PostingRuleKeys.ControlKeys.TryGetValue(key, out var requiredControl)
                && account.ControlType != requiredControl)
            {
                issues.Add(new PostingRuleHealthIssue(
                    key, $"Account {account.Code} must be a {requiredControl} control account."));
            }
        }

        return new PostingRuleHealthDto(issues.Count == 0, issues);
    }
}
