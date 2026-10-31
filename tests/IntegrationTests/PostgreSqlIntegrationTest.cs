using Npgsql;
using Testcontainers.PostgreSql;
using Xunit.Sdk;

namespace IntegrationTests;

public sealed class PostgreSqlIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder().Build();

    public async Task InitializeAsync()
    {
        try
        {
            await _container.StartAsync();
        }
        catch (Exception exception) when (exception is Docker.DotNet.DockerApiException or InvalidOperationException)
        {
            throw SkipException.ForSkip("Docker não está disponível para os testes de integração.");
        }
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task PostgreSql_ContainerDisponivel_DeveExecutarConsulta()
    {
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT 1", connection);
        var result = await command.ExecuteScalarAsync();

        Assert.Equal(1, result);
    }
}
