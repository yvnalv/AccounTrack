using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Application.Features;
using Accountrack.Accounting.Domain;
using Accountrack.Accounting.Application.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Accounting.UnitTests;

public class PostingRulesTests
{
    private readonly IPostingRuleRepository _rules = Substitute.For<IPostingRuleRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();

    private static readonly Guid DefaultRevenueAccount = Guid.NewGuid();
    private static readonly Guid ServiceRevenueAccount = Guid.NewGuid();
    private static readonly Guid ServiceCategory = Guid.NewGuid();

    [Fact]
    public async Task Resolver_falls_back_to_company_default_rule()
    {
        _rules.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<PostingRule>
        {
            PostingRule.CreateDefault(PostingRuleKeys.Revenue, DefaultRevenueAccount),
        });

        var result = await new PostingRuleResolver(_rules)
            .ResolveAsync("SalesInvoice", PostingRuleKeys.Revenue, PostingSelector.None, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DefaultRevenueAccount);
    }

    [Fact]
    public async Task Resolver_prefers_most_specific_selector_over_default()
    {
        _rules.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<PostingRule>
        {
            PostingRule.CreateDefault(PostingRuleKeys.Revenue, DefaultRevenueAccount),
            PostingRule.Create("SalesInvoice", PostingRuleKeys.Revenue, ServiceRevenueAccount, productCategoryId: ServiceCategory),
        });

        var result = await new PostingRuleResolver(_rules).ResolveAsync(
            "SalesInvoice", PostingRuleKeys.Revenue, new PostingSelector(ProductCategoryId: ServiceCategory), CancellationToken.None);

        result.Value.Should().Be(ServiceRevenueAccount);
    }

    [Fact]
    public async Task Resolver_does_not_apply_a_selector_rule_when_selector_differs()
    {
        _rules.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<PostingRule>
        {
            PostingRule.CreateDefault(PostingRuleKeys.Revenue, DefaultRevenueAccount),
            PostingRule.Create("SalesInvoice", PostingRuleKeys.Revenue, ServiceRevenueAccount, productCategoryId: ServiceCategory),
        });

        var result = await new PostingRuleResolver(_rules).ResolveAsync(
            "SalesInvoice", PostingRuleKeys.Revenue, new PostingSelector(ProductCategoryId: Guid.NewGuid()), CancellationToken.None);

        result.Value.Should().Be(DefaultRevenueAccount);
    }

    [Fact]
    public async Task Resolver_fails_loudly_when_no_rule_matches()
    {
        _rules.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<PostingRule>());

        var result = await new PostingRuleResolver(_rules)
            .ResolveAsync("SalesInvoice", PostingRuleKeys.Revenue, PostingSelector.None, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ACCOUNTING.POSTING_RULE_UNRESOLVED");
    }

    [Fact]
    public async Task Health_is_green_when_every_required_key_has_a_valid_default()
    {
        var (ruleList, accountMap) = BuildHealthyConfig();
        _rules.ListAsync(Arg.Any<CancellationToken>()).Returns(ruleList);
        _accounts.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(accountMap);

        var result = await new GetPostingRuleHealthQueryHandler(_rules, _accounts)
            .Handle(new GetPostingRuleHealthQuery(), CancellationToken.None);

        result.Value.IsHealthy.Should().BeTrue();
        result.Value.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Health_flags_a_missing_required_key()
    {
        var (ruleList, accountMap) = BuildHealthyConfig();
        ruleList.RemoveAll(r => r.RuleKey == PostingRuleKeys.GrIrClearing);
        _rules.ListAsync(Arg.Any<CancellationToken>()).Returns(ruleList);
        _accounts.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(accountMap);

        var result = await new GetPostingRuleHealthQueryHandler(_rules, _accounts)
            .Handle(new GetPostingRuleHealthQuery(), CancellationToken.None);

        result.Value.IsHealthy.Should().BeFalse();
        result.Value.Issues.Should().ContainSingle(i => i.RuleKey == PostingRuleKeys.GrIrClearing);
    }

    [Fact]
    public async Task Health_flags_a_control_key_pointing_at_a_non_control_account()
    {
        var (ruleList, accountMap) = BuildHealthyConfig();
        // Repoint ARControl at a plain (non-control) account.
        var plain = Account.CreateWithId(Guid.NewGuid(), "1000", "Cash", AccountType.Asset);
        accountMap = accountMap.ToDictionary(kv => kv.Key, kv => kv.Value);
        var arRule = ruleList.First(r => r.RuleKey == PostingRuleKeys.ArControl);
        arRule.Repoint(plain.Id);
        ((Dictionary<Guid, Account>)accountMap)[plain.Id] = plain;

        _rules.ListAsync(Arg.Any<CancellationToken>()).Returns(ruleList);
        _accounts.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(accountMap);

        var result = await new GetPostingRuleHealthQueryHandler(_rules, _accounts)
            .Handle(new GetPostingRuleHealthQuery(), CancellationToken.None);

        result.Value.IsHealthy.Should().BeFalse();
        result.Value.Issues.Should().Contain(i => i.RuleKey == PostingRuleKeys.ArControl);
    }

    private static (List<PostingRule> Rules, IReadOnlyDictionary<Guid, Account> Accounts) BuildHealthyConfig()
    {
        var rules = new List<PostingRule>();
        var accounts = new Dictionary<Guid, Account>();

        foreach (var key in PostingRuleKeys.Required)
        {
            Account account;
            if (PostingRuleKeys.ControlKeys.TryGetValue(key, out var control))
            {
                var (code, type) = control switch
                {
                    ControlType.AccountsReceivable => ("1100", AccountType.Asset),
                    ControlType.AccountsPayable => ("2100", AccountType.Liability),
                    _ => ("1200", AccountType.Asset),
                };
                account = Account.CreateWithId(Guid.NewGuid(), code, key, type, isControlAccount: true, controlType: control);
            }
            else
            {
                account = Account.CreateWithId(Guid.NewGuid(), "9" + key.Length.ToString("D3"), key, AccountType.Expense);
            }

            accounts[account.Id] = account;
            rules.Add(PostingRule.CreateDefault(key, account.Id));
        }

        return (rules, accounts);
    }
}
