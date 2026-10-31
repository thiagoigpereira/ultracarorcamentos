using Application.DTOs;

namespace Application.Interfaces;

public interface IOrcamentoPublicoService
{
    Task<ResultadoConsultaPublica> ObterPorTokenAsync(string token, CancellationToken ct = default);
    Task<ResultadoDecisao> RegistrarDecisaoAsync(string token, DecisaoClienteRequest request, CancellationToken ct = default);
}
