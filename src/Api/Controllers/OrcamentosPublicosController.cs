using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("public/v1/orcamentos")]
public sealed class OrcamentosPublicosController : ControllerBase
{
    private readonly IOrcamentoPublicoService _service;

    public OrcamentosPublicosController(IOrcamentoPublicoService service)
    {
        _service = service;
    }

    [HttpGet("{token}")]
    public async Task<IActionResult> Obter(string token, CancellationToken ct)
    {
        var result = await _service.ObterPorTokenAsync(token, ct);
        if (result.Sucesso)
            return Ok(result.Dados);
        if (result.CodigoErro == "ORC_LINK_EXPIRADO")
            return StatusCode(StatusCodes.Status410Gone, new { code = result.CodigoErro });
        return NotFound(new { code = result.CodigoErro });
    }

    [HttpPost("{token}/decisao")]
    public async Task<IActionResult> Decidir(string token, [FromBody] DecisaoClienteRequest request, CancellationToken ct)
    {
        var result = await _service.RegistrarDecisaoAsync(token, request, ct);
        if (result.Sucesso)
            return Ok(result);
        if (result.CodigoErro == "ORC_LINK_EXPIRADO")
            return StatusCode(StatusCodes.Status410Gone, result);
        if (result.CodigoErro == "ORC_DECISAO_JA_REGISTRADA")
            return Conflict(result);
        return UnprocessableEntity(result);
    }
}
