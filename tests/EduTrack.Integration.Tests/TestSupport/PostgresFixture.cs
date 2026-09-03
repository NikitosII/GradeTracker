using Testcontainers.PostgreSql;

namespace EduTrack.Integration.Tests.TestSupport;

/// <summary>
/// Spins up a throwaway PostgreSQL 16 container once per test collection so the
/// integration tests run against the same database engine used in production.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("edutrack")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

/// <summary>
/// Shares a single <see cref="PostgresFixture"/> across every test class in the
/// collection so the container (and its migrations) are created just once.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
