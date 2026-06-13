using Accountrack.Identity.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Accountrack.Identity.Infrastructure.Security;

/// <summary>
/// Password hashing via ASP.NET Core Identity's PBKDF2 hasher (SECURITY.md §1).
/// Argon2id is a planned upgrade; the interface keeps callers decoupled from the algorithm.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _inner = new();
    private static readonly object Dummy = new();

    public string Hash(string password) => _inner.HashPassword(Dummy, password);

    public bool Verify(string passwordHash, string providedPassword)
    {
        var result = _inner.VerifyHashedPassword(Dummy, passwordHash, providedPassword);
        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
