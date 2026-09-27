using ProGlass.Saas.Domain.Tenancy;
using Xunit;

namespace ProGlass.Saas.Domain.Tests;

public class AmbientTenantContextTests
{
    [Fact]
    public void Unset_context_fails_closed()
    {
        var context = new AmbientTenantContext();
        Assert.Equal(Guid.Empty, context.TenantId);
        Assert.Null(context.UserId);
        Assert.Equal("", context.Username);
    }

    [Fact]
    public void SetTenant_and_SetUser_populate_identity()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var context = new AmbientTenantContext();

        context.SetTenant(tenantId);
        context.SetUser(userId, "owner-a");

        Assert.Equal(tenantId, context.TenantId);
        Assert.Equal(userId, context.UserId);
        Assert.Equal("owner-a", context.Username);
    }

    [Fact]
    public void Empty_tenant_id_is_rejected()
    {
        var context = new AmbientTenantContext();
        Assert.Throws<ArgumentException>(() => context.SetTenant(Guid.Empty));
    }

    [Fact]
    public void Clear_resets_everything()
    {
        var context = new AmbientTenantContext();
        context.SetTenant(Guid.NewGuid());
        context.SetUser(Guid.NewGuid(), "owner-a");

        context.Clear();

        Assert.Equal(Guid.Empty, context.TenantId);
        Assert.Null(context.UserId);
        Assert.Equal("", context.Username);
    }
}
