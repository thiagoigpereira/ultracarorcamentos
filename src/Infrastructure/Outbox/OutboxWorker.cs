using System.Text.Json;
using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Outbox;

public sealed class OutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxWorker> _logger;

    public OutboxWorker(IServiceScopeFactory scopeFactory, ILogger<OutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
            var notification = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var messages = await repository.ReivindicarPendentesAsync(
                50,
                Guid.NewGuid(),
                TimeSpan.FromMinutes(5),
                stoppingToken);
            foreach (var message in messages)
            {
                try
                {
                    var payload = JsonSerializer.Deserialize<OrcamentoEnviadoPayload>(message.Payload)
                        ?? throw new InvalidOperationException($"Payload inválido na mensagem de outbox {message.Id}.");
                    if (string.IsNullOrWhiteSpace(message.DeduplicationKey))
                        throw new InvalidOperationException($"Chave de deduplicação ausente na mensagem de outbox {message.Id}.");

                    await notification.EnviarOrcamentoAsync(
                        payload.OrcamentoId,
                        $"/public/v1/orcamentos/{payload.TokenPublico}",
                        message.DeduplicationKey,
                        stoppingToken);
                    message.MarkProcessed(DateTime.UtcNow);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Falha ao processar mensagem de outbox {MessageId}", message.Id);
                    message.MarkFailed(exception.Message);
                }
            }
            await repository.SalvarAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private sealed record OrcamentoEnviadoPayload(Guid OrcamentoId, int Versao, string TokenPublico);
}
