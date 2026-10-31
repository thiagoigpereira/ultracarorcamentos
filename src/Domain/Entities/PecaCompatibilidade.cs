namespace Domain.Entities;

public class PecaCompatibilidade
{
    public Guid PecaId { get; private set; }
    public Peca Peca { get; private set; } = null!;

    public Guid ModeloId { get; private set; }
    public Modelo Modelo { get; private set; } = null!;

    public int AnoInicio { get; private set; }
    public int AnoFim { get; private set; }

    private PecaCompatibilidade() { } 

    public PecaCompatibilidade(Guid pecaId, Guid modeloId, int anoInicio, int anoFim)
    {
        PecaId = pecaId;
        ModeloId = modeloId;
        AnoInicio = anoInicio;
        AnoFim = anoFim;
    }

    public bool EhCompativelCom(Guid modeloId, int ano)
    {
        return ModeloId == modeloId && ano >= AnoInicio && ano <= AnoFim;
    }
}
