using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Outbox;

public sealed class LoggingNotificationService : INotificationService
{
    private readonly ILogger<LoggingNotificationService> _logger;

    public LoggingNotificationService(ILogger<LoggingNotificationService> logger)
    {
        _logger = logger;
    }

    public Task EnviarOrcamentoAsync(Guid orcamentoId, string linkCliente, string deduplicationKey, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Notificação {DeduplicationKey} do orçamento {OrcamentoId} enviada: {LinkCliente}", deduplicationKey, orcamentoId, linkCliente);
        return Task.CompletedTask;
    }
}
