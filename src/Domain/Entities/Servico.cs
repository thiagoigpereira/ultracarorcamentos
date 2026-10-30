namespace Domain.Entities;

public class Servico
{
    public Guid Id { get; private set; }
    public string Descricao { get; private set; } = string.Empty;
    public decimal TempoPadraoHoras { get; private set; }

    private readonly List<ServicoTipoPeca> _servicoTipoPecas = new();
    public IReadOnlyCollection<ServicoTipoPeca> ServicoTipoPecas => _servicoTipoPecas.AsReadOnly();

    private Servico() { } 

    public Servico(string descricao, decimal tempoPadraoHoras)
    {
        Id = Guid.NewGuid();
        Descricao = descricao;
        TempoPadraoHoras = tempoPadraoHoras;
    }
}