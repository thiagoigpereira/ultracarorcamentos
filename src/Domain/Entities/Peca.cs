namespace Domain.Entities;

public class Peca : Entity
{
    public string CodigoFabricante { get; private set; } = string.Empty;
    public string Descricao { get; private set; } = string.Empty;
    public Guid TipoPecaId { get; private set; }
    public decimal Preco { get; private set; }
    public bool Preferencial { get; private set; }

    private readonly List<PecaCompatibilidade> _compatibilidades = new();
    public IReadOnlyCollection<PecaCompatibilidade> Compatibilidades => _compatibilidades.AsReadOnly();

    private Peca() { } 

    public Peca(string codigoFabricante, string descricao, Guid tipoPecaId, decimal preco, bool preferencial)
    {
        CodigoFabricante = codigoFabricante;
        Descricao = descricao;
        TipoPecaId = tipoPecaId;
        Preco = preco;
        Preferencial = preferencial;
    }
}
