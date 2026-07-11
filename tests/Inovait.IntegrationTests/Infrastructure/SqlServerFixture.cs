using Testcontainers.MsSql;

namespace Inovait.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SQL Server";
}

public sealed class SqlServerFixture : IAsyncLifetime
{
    internal const string ExternalConnectionVariable = "ConnectionStrings__InovaitTest";

    private readonly MsSqlContainer? _container;
    private readonly string? _externalConnectionString;
    private string? _connectionString;

    public SqlServerFixture()
    {
        var externalConnectionString = Environment.GetEnvironmentVariable(ExternalConnectionVariable);
        _externalConnectionString = string.IsNullOrWhiteSpace(externalConnectionString)
            ? null
            : externalConnectionString;

        if (_externalConnectionString is null)
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04")
                .Build();
        }
    }

    public bool UsesExternalSqlServer => _externalConnectionString is not null;

    public string ConnectionString => _connectionString
        ?? throw new InvalidOperationException("The SQL Server fixture has not been initialized.");

    public async ValueTask InitializeAsync()
    {
        if (_externalConnectionString is not null)
        {
            _connectionString = _externalConnectionString;
            return;
        }

        await _container!.StartAsync();
        _connectionString = _container.GetConnectionString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
