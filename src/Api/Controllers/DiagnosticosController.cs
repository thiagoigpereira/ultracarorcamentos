using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/diagnosticos")]
public class DiagnosticosController : ControllerBase
{
    private readonly IDiagnosticoRepository _diagnosticoRepository;

    public DiagnosticosController(IDiagnosticoRepository diagnosticoRepository)
    {
        _diagnosticoRepository = diagnosticoRepository;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterDiagnosticos([FromQuery] string? busca, CancellationToken ct)
    {
        var diagnosticos = await _diagnosticoRepository.BuscarAsync(busca, ct);
        var response = diagnosticos.Select(d => new
        {
            id = d.Id,
            codigo = d.Codigo,
            descricao = d.Descricao,
            sistema = d.Sistema
        });

        return Ok(response);
    }
}
