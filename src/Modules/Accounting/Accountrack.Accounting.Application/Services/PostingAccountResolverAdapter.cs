using Accountrack.Accounting.Application.Abstractions;
using Accountrack.SharedKernel.Results;
using ContractsResolver = Accountrack.Modules.Contracts.Accounting.IPostingAccountResolver;
using ContractsSelector = Accountrack.Modules.Contracts.Accounting.PostingSelector;
using DomainSelector = Accountrack.Accounting.Domain.PostingSelector;

namespace Accountrack.Accounting.Application.Services;

/// <summary>
/// Adapter exposing the posting-rule engine as the public <see cref="ContractsResolver"/> contract,
/// so other modules resolve GL accounts by purpose without depending on Accounting internals.
/// </summary>
public sealed class PostingAccountResolverAdapter : ContractsResolver
{
    private readonly IPostingRuleResolver _resolver;

    public PostingAccountResolverAdapter(IPostingRuleResolver resolver) => _resolver = resolver;

    public Task<Result<Guid>> ResolveAsync(string eventType, string ruleKey, ContractsSelector selector, CancellationToken ct) =>
        _resolver.ResolveAsync(
            eventType, ruleKey,
            new DomainSelector(selector.ProductCategoryId, selector.WarehouseId, selector.TaxCodeId, selector.BankAccountId),
            ct);
}
