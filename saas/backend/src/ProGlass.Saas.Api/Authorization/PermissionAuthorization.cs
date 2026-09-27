using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Api.Authorization;

/// <summary>
/// The requirement a permission check carries: the permission name, e.g.
/// "Settings.Manage". Granted when the authenticated user's ROLE carries
/// the permission in the WPF-verbatim catalog (RolePermissions).
/// </summary>
public sealed class PermissionRequirement(string permission)
    : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>
/// [HasPermission("Settings.Manage")] — the policy NAME is the permission
/// name itself; <see cref="PermissionPolicyProvider"/> materializes the
/// policy on demand, so permissions never need a per-permission
/// AddPolicy registration (there are 30+ in the catalog and they ship as
/// versioned code).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "permission:";

    public HasPermissionAttribute(string permission)
        : base(PolicyPrefix + permission)
    {
    }
}

/// <summary>
/// Dynamic policy provider: "permission:X.Y" becomes a policy requiring
/// permission X.Y. Unknown/other policy names fall through to the default
/// provider (plain [Authorize], named policies if any are ever added).
/// Valid permission names are the AppPermissions constants — discovered
/// via reflection once so the ported catalog stays the single source of
/// truth (no duplicated list to keep in sync).
/// </summary>
public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    private static readonly HashSet<string> KnownPermissions = GetKnownPermissions();

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        : base(options)
    {
    }

    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(HasPermissionAttribute.PolicyPrefix,
                StringComparison.Ordinal))
        {
            var permission = policyName[HasPermissionAttribute.PolicyPrefix.Length..];
            if (!KnownPermissions.Contains(permission))
                throw new InvalidOperationException(
                    $"Unknown permission \"{permission}\" — permissions come from " +
                    "ProGlass.Saas.Domain.Authorization.AppPermissions (WPF catalog, verbatim).");

            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
        return base.GetPolicyAsync(policyName);
    }

    private static HashSet<string> GetKnownPermissions() =>
        typeof(AppPermissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);
}

/// <summary>
/// Grants a requirement when the authenticated user's role carries the
/// permission (RolePermissions — the WPF grant map, verbatim; the
/// Administrator "*" wildcard is honored there). Unknown roles get
/// nothing. This replaces the WPF PermissionService dialog: denial is a
/// server-side 403 with an audit trail, not a UI message.
/// </summary>
public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        // JwtBearer maps "sub" → ClaimTypes.NameIdentifier; "role" stays
        // as-is (there is no mapping when the claim is not a URI type).
        var role = context.User.FindFirstValue("role");
        if (!string.IsNullOrEmpty(role)
            && RolePermissions.Has(role, requirement.Permission))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
