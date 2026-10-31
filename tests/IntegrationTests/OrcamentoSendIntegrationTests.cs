using System.Net;
using System.Net.Http.Json;
using Application.DTOs;
using Domain.Entities;
using Infrastructure.Persistence.Context;
using Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit.Sdk;

namespace IntegrationTests;

public sealed class OrcamentoSendIntegrationTests : IClassFixture<PostgreSqlApiFixture>
{
    private readonly PostgreSqlApiFixture _fixture;

    public OrcamentoSendIntegrationTests(PostgreSqlApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task T13_EnviosConcorrentes_DeveCriarUmOutboxERetornarUmSucessoENoveConflitos()
    {
        var (orcamentoId, etag) = await CriarOrcamentoAsync();
        const int usuarioId = 1001;
        var requests = Enumerable.Range(0, 10)
            .Select(index => EnviarAsync(orcamentoId, etag, $"t13-{Guid.NewGuid():N}-{index}", usuarioId))
            .ToArray();

        var responses = await Task.WhenAll(requests);

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(9, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        foreach (var response in responses.Where(response =>
                     response.StatusCode is not HttpStatusCode.OK and not HttpStatusCode.Conflict))
        {
            Assert.Fail($"Unexpected response: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        }

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var outboxMessage = await dbContext.OutboxMessages.SingleAsync(message =>
            message.Type == "OrcamentoEnviado" && message.Payload.Contains(orcamentoId.ToString()));
        Assert.Equal($"{orcamentoId:N}:1", outboxMessage.DeduplicationKey);

        foreach (var response in responses)
            response.Dispose();
    }

    [Fact]
    public async Task T14_ReenvioComMesmaChave_DeveRepetirRespostaSemDuplicarOutbox()
    {
        var (orcamentoId, etag) = await CriarOrcamentoAsync();
        const string idempotencyKey = "t14-same-key";
        const int usuarioId = 1002;

        using var firstResponse = await EnviarAsync(orcamentoId, etag, idempotencyKey, usuarioId);
        using var replayResponse = await EnviarAsync(orcamentoId, etag, idempotencyKey, usuarioId);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.Equal(
            await firstResponse.Content.ReadAsStringAsync(),
            await replayResponse.Content.ReadAsStringAsync());

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var outboxCount = await dbContext.OutboxMessages.CountAsync(message =>
            message.Type == "OrcamentoEnviado" && message.Payload.Contains(orcamentoId.ToString()));

        Assert.Equal(1, outboxCount);
    }

    [Fact]
    public async Task CriacaoConcorrenteParaMesmaOS_DeveRetornarOrcamentoUnico()
    {
        var ordemServicoId = Random.Shared.Next(1, int.MaxValue);
        var requests = Enumerable.Range(0, 2).Select(_ =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ordens-servico/{ordemServicoId}/orcamento")
            {
                Content = JsonContent.Create(new CriarOrcamentoRequest(42, ordemServicoId, Guid.NewGuid(), Guid.NewGuid()))
            };
            request.Headers.TryAddWithoutValidation("X-Oficina-Id", "42");
            return _fixture.Client.SendAsync(request);
        });

        var responses = await Task.WhenAll(requests);
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        var budgets = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<OrcamentoResponse>()));
        Assert.NotNull(budgets[0]);
        Assert.NotNull(budgets[1]);
        Assert.Equal(budgets[0]!.Id, budgets[1]!.Id);

        await using var dbContext = CriarDbContext();
        Assert.Equal(1, await dbContext.Orcamentos.CountAsync(budget =>
            budget.OficinaId == 42 && budget.OrdemServicoId == ordemServicoId));
        foreach (var response in responses)
            response.Dispose();
    }

    [Fact]
    public async Task ClaimsConcorrentesDoOutbox_NaoDevemReivindicarMensagensSobrepostas()
    {
        var messages = Enumerable.Range(0, 10)
            .Select(index => new OutboxMessage("Teste", $"payload-{Guid.NewGuid():N}-{index}", DateTime.UtcNow.AddMilliseconds(index)))
            .ToArray();
        await using (var seedContext = CriarDbContext())
        {
            await seedContext.OutboxMessages.AddRangeAsync(messages);
            await seedContext.SaveChangesAsync();
        }

        await using var firstContext = CriarDbContext();
        await using var secondContext = CriarDbContext();
        var firstRepository = new OutboxRepository(firstContext);
        var secondRepository = new OutboxRepository(secondContext);
        var results = await Task.WhenAll(
            firstRepository.ReivindicarPendentesAsync(10, Guid.NewGuid(), TimeSpan.FromMinutes(5)),
            secondRepository.ReivindicarPendentesAsync(10, Guid.NewGuid(), TimeSpan.FromMinutes(5)));

        var firstIds = results[0].Select(message => message.Id).ToHashSet();
        var secondIds = results[1].Select(message => message.Id).ToHashSet();
        Assert.Empty(firstIds.Intersect(secondIds));
        Assert.All(messages, message => Assert.Contains(message.Id, firstIds.Union(secondIds)));
        Assert.All(results.SelectMany(result => result), message => Assert.NotNull(message.ProcessingLeaseId));
    }

    [Fact]
    public async Task EdicoesConcorrentesComMesmoETag_ApenasUmaDeveSerAceita()
    {
        var (orcamentoId, etag) = await CriarOrcamentoAsync();
        var requests = new[] { "Edição A", "Edição B" }.Select(description =>
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/orcamentos/{orcamentoId}/itens")
            {
                Content = JsonContent.Create(new AtualizarItensRequest(new List<ItemOrcamentoInputDto>
                {
                    new("Servico", Guid.NewGuid(), null, description, 1, 200m)
                }))
            };
            request.Headers.TryAddWithoutValidation("X-Oficina-Id", "42");
            request.Headers.TryAddWithoutValidation("If-Match", etag);
            request.Headers.TryAddWithoutValidation("X-User-Id", "1003");
            return _fixture.Client.SendAsync(request);
        });

        var responses = await Task.WhenAll(requests);
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.PreconditionFailed));

        await using var dbContext = CriarDbContext();
        var item = await dbContext.OrcamentoItens.SingleAsync(current => current.OrcamentoId == orcamentoId);
        Assert.Contains(item.Descricao, new[] { "Edição A", "Edição B" });
        Assert.Equal(1, await dbContext.OrcamentoHistoricos.CountAsync(history =>
            history.OrcamentoId == orcamentoId && history.Detalhes == "Tentativa de edição rejeitada: ETag desatualizado."));
        foreach (var response in responses)
            response.Dispose();
    }

    [Fact]
    public async Task EdicaoComOutraOficina_DeveRetornarNaoEncontradoSemAlterarOrcamento()
    {
        var (orcamentoId, etag) = await CriarOrcamentoAsync();
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/orcamentos/{orcamentoId}/itens")
        {
            Content = JsonContent.Create(new AtualizarItensRequest(new List<ItemOrcamentoInputDto>
            {
                new("Servico", Guid.NewGuid(), null, "Não autorizado", 1, 200m)
            }))
        };
        request.Headers.TryAddWithoutValidation("X-Oficina-Id", "43");
        request.Headers.TryAddWithoutValidation("X-User-Id", "1007");
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        using var response = await _fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var dbContext = CriarDbContext();
        var item = await dbContext.OrcamentoItens.SingleAsync(current => current.OrcamentoId == orcamentoId);
        Assert.Equal("Serviço de integração", item.Descricao);
    }

    [Fact]
    public async Task EdicaoSemUsuarioNumerico_DeveRetornarRequisicaoInvalida()
    {
        var (orcamentoId, etag) = await CriarOrcamentoAsync();
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/orcamentos/{orcamentoId}/itens")
        {
            Content = JsonContent.Create(new AtualizarItensRequest(new List<ItemOrcamentoInputDto>()))
        };
        request.Headers.TryAddWithoutValidation("X-Oficina-Id", "42");
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        using var response = await _fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DecisoesPublicasConcorrentes_DevemPersistirDecisaoEVincularConflitoAoHistorico()
    {
        var (orcamentoId, etag) = await CriarOrcamentoAsync();
        using (var sendResponse = await EnviarAsync(orcamentoId, etag, $"public-{Guid.NewGuid():N}", 1004))
            Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);

        await using var tokenContext = CriarDbContext();
        var token = await tokenContext.Orcamentos
            .Where(budget => budget.Id == orcamentoId)
            .Select(budget => budget.TokenPublico)
            .SingleAsync();
        Assert.NotNull(token);

        var requests = Enumerable.Range(0, 2).Select(_ => _fixture.Client.PostAsJsonAsync(
            $"/public/v1/orcamentos/{token}/decisao",
            new DecisaoClienteRequest("Total", null, null)));
        var responses = await Task.WhenAll(requests);
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));

        await using var dbContext = CriarDbContext();
        var budget = await dbContext.Orcamentos.SingleAsync(current => current.Id == orcamentoId);
        Assert.Equal(Domain.Enums.StatusOrcamento.AprovadoTotal, budget.Status);
        var publicHistory = await dbContext.OrcamentoHistoricos
            .Where(history => history.OrcamentoId == orcamentoId && history.Canal == "LinkPublico")
            .ToListAsync();
        Assert.Equal(2, publicHistory.Count);
        Assert.Contains(publicHistory, history => history.Detalhes == "Decisão do cliente: Total.");
        Assert.Contains(publicHistory, history => history.Detalhes!.Contains("Decisão do cliente rejeitada"));
        foreach (var response in responses)
            response.Dispose();
    }

    [Fact]
    public async Task RecusaComMotivo_DevePersistirMotivoEUsuarioNumericoNoHistorico()
    {
        var (orcamentoId, etag) = await CriarOrcamentoAsync();
        using (var sendResponse = await EnviarAsync(orcamentoId, etag, $"refusal-{Guid.NewGuid():N}", 1005))
            Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);

        await using var tokenContext = CriarDbContext();
        var token = await tokenContext.Orcamentos
            .Where(budget => budget.Id == orcamentoId)
            .Select(budget => budget.TokenPublico)
            .SingleAsync();
        Assert.NotNull(token);

        const string motivo = "Cliente fará o reparo em outra ocasião.";
        using var decisionResponse = await _fixture.Client.PostAsJsonAsync(
            $"/public/v1/orcamentos/{token}/decisao",
            new DecisaoClienteRequest("Recusa", null, motivo));
        Assert.Equal(HttpStatusCode.OK, decisionResponse.StatusCode);

        await using var dbContext = CriarDbContext();
        var budget = await dbContext.Orcamentos.SingleAsync(current => current.Id == orcamentoId);
        Assert.Equal(motivo, budget.MotivoRecusa);
        Assert.Contains(await dbContext.OrcamentoHistoricos.Where(history => history.OrcamentoId == orcamentoId).ToListAsync(),
            history => history.UsuarioId == "1005");
        var publicHistory = await dbContext.OrcamentoHistoricos.SingleAsync(history =>
            history.OrcamentoId == orcamentoId && history.Canal == "LinkPublico");
        Assert.Equal(motivo, publicHistory.Detalhes);

        using var publicResponse = await _fixture.Client.GetAsync($"/public/v1/orcamentos/{token}");
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        var publicJson = await publicResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(motivo, publicJson.GetProperty("motivoRecusa").GetString());
    }

    [Fact]
    public async Task DescontoPercentual_DevePersistirEDevolverValorCalculado()
    {
        var (orcamentoId, etag) = await CriarOrcamentoAsync();
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/orcamentos/{orcamentoId}/itens")
        {
            Content = JsonContent.Create(new AtualizarItensRequest(
                new List<ItemOrcamentoInputDto>
                {
                    new("Servico", Guid.NewGuid(), null, "Serviço com desconto", 1, 333.33m)
                },
                10m,
                DescontoPercentual: true))
        };
        request.Headers.TryAddWithoutValidation("X-Oficina-Id", "42");
        request.Headers.TryAddWithoutValidation("X-User-Id", "1006");
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        using var response = await _fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<OrcamentoResponse>();
        Assert.NotNull(result);
        Assert.Equal(300m, result.Total);
        Assert.Equal(10m, result.Desconto);
        Assert.True(result.DescontoPercentual);

        await using var dbContext = CriarDbContext();
        var persisted = await dbContext.Orcamentos.SingleAsync(budget => budget.Id == orcamentoId);
        Assert.Equal(300m, persisted.Total);
        Assert.Equal(10m, persisted.Desconto);
        Assert.True(persisted.DescontoPercentual);
        Assert.Contains(await dbContext.OrcamentoHistoricos.Where(history => history.OrcamentoId == orcamentoId).ToListAsync(),
            history => history.UsuarioId == "1006");
    }

    private AppDbContext CriarDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        return new AppDbContext(options);
    }

    private async Task<(Guid Id, string ETag)> CriarOrcamentoAsync()
    {
        var ordemServicoId = Random.Shared.Next(1, int.MaxValue);
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ordens-servico/{ordemServicoId}/orcamento")
        {
            Content = JsonContent.Create(new CriarOrcamentoRequest(42, ordemServicoId, Guid.NewGuid(), Guid.NewGuid()))
        };
        createRequest.Headers.TryAddWithoutValidation("X-Oficina-Id", "42");
        using var response = await _fixture.Client.SendAsync(createRequest, new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token);

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected Created but got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<OrcamentoResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(response.Headers.ETag?.ToString()));
        Assert.Equal($"\"{body.RowVersionBase64}\"", response.Headers.ETag!.ToString());

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var storedVersion = await dbContext.Orcamentos
                .Where(budget => budget.Id == body.Id)
                .Select(budget => budget.RowVersion)
                .SingleAsync();
            Assert.Equal(body.RowVersionBase64, Convert.ToBase64String(storedVersion));
        }

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/orcamentos/{body.Id}/itens")
        {
            Content = JsonContent.Create(new AtualizarItensRequest(new List<ItemOrcamentoInputDto>
            {
                new("Servico", Guid.NewGuid(), null, "Serviço de integração", 1, 100m)
            }))
        };
        updateRequest.Headers.TryAddWithoutValidation("X-Oficina-Id", "42");
        updateRequest.Headers.TryAddWithoutValidation("If-Match", response.Headers.ETag!.ToString());
        updateRequest.Headers.TryAddWithoutValidation("X-User-Id", "1000");
        using var updateResponse = await _fixture.Client.SendAsync(updateRequest);
        Assert.True(
            updateResponse.StatusCode == HttpStatusCode.OK,
            $"Expected item update to succeed but got {(int)updateResponse.StatusCode}: {await updateResponse.Content.ReadAsStringAsync()}");
        Assert.False(string.IsNullOrWhiteSpace(updateResponse.Headers.ETag?.ToString()));
        return (body.Id, updateResponse.Headers.ETag!.ToString());
    }

    private async Task<HttpResponseMessage> EnviarAsync(Guid orcamentoId, string etag, string idempotencyKey, int usuarioId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/orcamentos/{orcamentoId}/envio")
        {
            Content = JsonContent.Create(new EnviarOrcamentoRequest("Email", "Teste de integração"))
        };
        request.Headers.TryAddWithoutValidation("X-Oficina-Id", "42");
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        request.Headers.TryAddWithoutValidation("X-User-Id", usuarioId.ToString());
        return await _fixture.Client.SendAsync(request);
    }
}

public sealed class PostgreSqlApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().Build();

    public string ConnectionString => _postgres.GetConnectionString();
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            await _postgres.StartAsync();
        }
        catch (Exception exception) when (exception is Docker.DotNet.DockerApiException or InvalidOperationException)
        {
            throw SkipException.ForSkip("Docker não está disponível para os testes de integração.");
        }

        var previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _postgres.GetConnectionString());
        try
        {
            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString()
                    }));
            });

            Client = Factory.CreateClient();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", previousConnectionString);
        }
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (Factory is not null)
            await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
