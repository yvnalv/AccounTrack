using Accountrack.Identity.Infrastructure.Security;
using FluentAssertions;
using Xunit;

namespace Accountrack.Identity.UnitTests;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_does_not_return_the_plaintext()
    {
        var hash = _hasher.Hash("Sup3rSecret!");
        hash.Should().NotBeNullOrEmpty();
        hash.Should().NotBe("Sup3rSecret!");
    }

    [Fact]
    public void Verify_returns_true_for_the_correct_password()
    {
        var hash = _hasher.Hash("Sup3rSecret!");
        _hasher.Verify(hash, "Sup3rSecret!").Should().BeTrue();
    }

    [Fact]
    public void Verify_returns_false_for_a_wrong_password()
    {
        var hash = _hasher.Hash("Sup3rSecret!");
        _hasher.Verify(hash, "wrong").Should().BeFalse();
    }

    [Fact]
    public void Hashes_are_salted_and_differ_for_the_same_password()
    {
        _hasher.Hash("same").Should().NotBe(_hasher.Hash("same"));
    }
}
