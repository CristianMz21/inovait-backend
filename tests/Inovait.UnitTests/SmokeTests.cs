namespace Inovait.UnitTests;

public sealed class SmokeTests
{
    [Fact]
    public void Smoke_UnitSuite_LoadsNamespace()
    {
        var assemblyName = typeof(SmokeTests).Assembly.GetName().Name;
        Assert.NotNull(assemblyName);
        Assert.Contains("Inovait.UnitTests", assemblyName);
    }
}
