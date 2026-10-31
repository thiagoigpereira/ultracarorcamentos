using Domain.Enums;

namespace Domain.Entities;

public class OrcamentoHistorico : Entity
{
    public Guid OrcamentoId { get; private set; }
    public StatusOrcamento? De { get; private set; }
    public StatusOrcamento Para { get; private set; }
    public string? UsuarioId { get; private set; }
    public string Canal { get; private set; } = string.Empty;
    public DateTime Data { get; private set; }
    public string? Detalhes { get; private set; }

    private OrcamentoHistorico() { } 

    public OrcamentoHistorico(Guid orcamentoId, StatusOrcamento? de, StatusOrcamento para, string? usuarioId, string canal, DateTime data, string? detalhes = null)
    {
        OrcamentoId = orcamentoId;
        De = de;
        Para = para;
        UsuarioId = usuarioId;
        Canal = canal;
        Data = data;
        Detalhes = detalhes;
    }
}
