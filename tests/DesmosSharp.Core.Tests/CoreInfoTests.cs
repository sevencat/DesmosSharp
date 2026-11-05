using DesmosSharp.Core;

namespace DesmosSharp.Core.Tests;

public class CoreInfoTests
{
    [Fact]
    public void Product_name_is_stable()
    {
        Assert.Equal("DesmosSharp", CoreInfo.ProductName);
    }

    [Fact]
    public void Assembly_is_loadable()
    {
        Assert.NotNull(typeof(CoreInfo).Assembly);
    }
}
