namespace Inovait.IntegrationTests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EnvironmentVariableCollection
{
    public const string Name = "Environment variables";
}

[Collection(EnvironmentVariableCollection.Name)]
[Trait("Priority", "P0")]
public sealed class SqlServerFixtureTests
{
    [Fact]
    public async Task ExternalConnectionEnvironmentVariable_BypassesContainerLifecycle()
    {
        const string externalConnection =
            "Server=external-test;Database=inovait_test;Integrated Security=True;Encrypt=True";
        var previousValue = Environment.GetEnvironmentVariable(SqlServerFixture.ExternalConnectionVariable);

        try
        {
            Environment.SetEnvironmentVariable(
                SqlServerFixture.ExternalConnectionVariable,
                externalConnection);
            await using var fixture = new SqlServerFixture();

            await fixture.InitializeAsync();

            Assert.True(fixture.UsesExternalSqlServer);
            Assert.Equal(externalConnection, fixture.ConnectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                SqlServerFixture.ExternalConnectionVariable,
                previousValue);
        }
    }
}
