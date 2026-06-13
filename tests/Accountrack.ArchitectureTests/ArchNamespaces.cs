namespace Accountrack.ArchitectureTests;

internal static class ArchNamespaces
{
    /// <summary>
    /// ASP.NET Core *web* namespaces. Deliberately excludes "Microsoft.AspNetCore.Identity",
    /// which is the (non-web) password-hashing crypto package from Microsoft.Extensions.Identity.Core.
    /// </summary>
    public static readonly string[] Web =
    {
        "Microsoft.AspNetCore.Http",
        "Microsoft.AspNetCore.Mvc",
        "Microsoft.AspNetCore.Builder",
        "Microsoft.AspNetCore.Routing",
        "Microsoft.AspNetCore.Hosting",
    };
}
