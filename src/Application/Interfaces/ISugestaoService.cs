using Application.DTOs;

namespace Application.Interfaces;

public interface ISugestaoService
{
    Task<SugestaoResponse> GerarSugestaoAsync(CriarSugestaoRequest request, CancellationToken cancellationToken = default);
}
