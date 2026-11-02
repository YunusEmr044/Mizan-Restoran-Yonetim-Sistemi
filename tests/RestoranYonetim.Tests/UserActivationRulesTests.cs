using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

public class UserActivationRulesTests
{
    [Fact]
    public void IsDeactivated_OnlyForLongLockout()
    {
        Assert.False(UserActivationRules.IsDeactivated(null));
        Assert.False(UserActivationRules.IsDeactivated(DateTimeOffset.UtcNow.AddMinutes(5)));
        Assert.True(UserActivationRules.IsDeactivated(DateTimeOffset.MaxValue));
    }

    [Fact]
    public void BlockReason_SelfIsBlocked()
    {
        Assert.NotNull(UserActivationRules.BlockReason(isSelf: true, targetIsSuperAdmin: false, actorIsSuperAdmin: true, deactivating: true, activeSuperAdminCount: 2));
    }

    [Fact]
    public void BlockReason_NonSuperAdminCannotTouchSuperAdmin()
    {
        Assert.NotNull(UserActivationRules.BlockReason(false, targetIsSuperAdmin: true, actorIsSuperAdmin: false, deactivating: true, activeSuperAdminCount: 2));
        Assert.NotNull(UserActivationRules.BlockReason(false, targetIsSuperAdmin: true, actorIsSuperAdmin: false, deactivating: false, activeSuperAdminCount: 2));
    }

    [Fact]
    public void BlockReason_LastActiveSuperAdminCannotBeDeactivated()
    {
        Assert.NotNull(UserActivationRules.BlockReason(false, true, true, deactivating: true, activeSuperAdminCount: 1));
    }

    [Fact]
    public void BlockReason_ActivatingIgnoresSuperAdminCount()
    {
        Assert.Null(UserActivationRules.BlockReason(false, true, true, deactivating: false, activeSuperAdminCount: 1));
    }

    [Fact]
    public void BlockReason_NormalStaffAllowed()
    {
        Assert.Null(UserActivationRules.BlockReason(false, false, false, deactivating: true, activeSuperAdminCount: 1));
    }
}
