namespace Domain.Entities;

public class ServicoTipoPeca
{
    public Guid ServicoId { get; private set; }
    public Servico Servico { get; private set; } = null!;

    public Guid TipoPecaId { get; private set; }
    public string TipoPecaNome { get; private set; } = string.Empty; 
    public int QuantidadePadrao { get; private set; }

    private ServicoTipoPeca() { } 

    public ServicoTipoPeca(Guid servicoId, Guid tipoPecaId, string tipoPecaNome, int quantidadePadrao)
    {
        ServicoId = servicoId;
        TipoPecaId = tipoPecaId;
        TipoPecaNome = tipoPecaNome;
        QuantidadePadrao = quantidadePadrao;
    }
}