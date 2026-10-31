namespace Domain.Entities;

public class DiagnosticoServico
{
    public Guid DiagnosticoId { get; private set; }
    public Diagnostico Diagnostico { get; private set; } = null!;

    public Guid ServicoId { get; private set; }
    public Servico Servico { get; private set; } = null!;

    private DiagnosticoServico() { } 

    public DiagnosticoServico(Guid diagnosticoId, Guid servicoId)
    {
        DiagnosticoId = diagnosticoId;
        ServicoId = servicoId;
    }
}
