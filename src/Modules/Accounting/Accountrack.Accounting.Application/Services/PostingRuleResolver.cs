using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Domain;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Accounting.Application.Services;

/// <summary>
/// Default <see cref="IPostingRuleResolver"/>: loads the company's rules and selects the
/// most-specific rule matching the event + selectors, falling back to the company-wide default.
/// If nothing resolves, fails loudly (never a silent wrong account) per docs/POSTING_RULES.md §1.
/// </summary>
public sealed class PostingRuleResolver : IPostingRuleResolver
{
    private readonly IPostingRuleRepository _rules;

    public PostingRuleResolver(IPostingRuleRepository rules) => _rules = rules;

    public async Task<Result<Guid>> ResolveAsync(
        string eventType, string ruleKey, PostingSelector selector, CancellationToken ct)
    {
        var all = await _rules.ListAsync(ct);

        var match = all
            .Where(r => string.Equals(r.RuleKey, ruleKey, StringComparison.OrdinalIgnoreCase) && r.Matches(eventType, selector))
            .OrderByDescending(r => r.Specificity)
            .FirstOrDefault();

        if (match is null)
        {
            return AccountingErrors.PostingRuleUnresolved(eventType, ruleKey);
        }

        return match.AccountId;
    }
}
