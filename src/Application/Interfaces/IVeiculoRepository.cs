using Domain.Entities;

namespace Application.Interfaces;

public interface IVeiculoRepository
{
    Task<Veiculo?> ObterPorPlacaEOficinaAsync(string placa, int oficinaId, CancellationToken ct = default);
}
