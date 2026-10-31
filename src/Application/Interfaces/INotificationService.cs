namespace Application.Interfaces;

public interface INotificationService
{
    Task EnviarOrcamentoAsync(Guid orcamentoId, string linkCliente, string deduplicationKey, CancellationToken cancellationToken = default);
}
