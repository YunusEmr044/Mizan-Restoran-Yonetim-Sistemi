using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

public class RateLimitKeysTests
{
    [Fact]
    public void TablesBehindSameIpGetSeparateBuckets()
    {
        Assert.NotEqual(RateLimitKeys.TableAction("85.1.2.3", "masa-a"), RateLimitKeys.TableAction("85.1.2.3", "masa-b"));
    }

    [Fact]
    public void SameTableSameIpSharesBucket()
    {
        Assert.Equal(RateLimitKeys.TableAction("85.1.2.3", "masa-a"), RateLimitKeys.TableAction("85.1.2.3", "masa-a"));
    }

    [Fact]
    public void DifferentIpsOnSameTableAreSeparate()
    {
        Assert.NotEqual(RateLimitKeys.TableAction("85.1.2.3", "masa-a"), RateLimitKeys.TableAction("85.1.2.4", "masa-a"));
    }

    [Fact]
    public void MissingValuesStillProduceAKey()
    {
        Assert.Equal("unknown|", RateLimitKeys.TableAction(null, null));
    }
}
