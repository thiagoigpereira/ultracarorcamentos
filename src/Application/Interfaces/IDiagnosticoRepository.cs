using Domain.Entities;

namespace Application.Interfaces;

public interface IDiagnosticoRepository
{
    Task<IEnumerable<Diagnostico>> BuscarAsync(string? termo, CancellationToken ct = default);
}
