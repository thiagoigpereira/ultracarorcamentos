namespace Domain.Entities;

public class Servico : Entity
{
    public string Descricao { get; private set; } = string.Empty;
    public decimal TempoPadraoHoras { get; private set; }

    private readonly List<ServicoTipoPeca> _servicoTipoPecas = new();
    public IReadOnlyCollection<ServicoTipoPeca> ServicoTipoPecas => _servicoTipoPecas.AsReadOnly();

    public decimal ValorMaoDeObra { get; set; }

    private Servico() { } 

    public Servico(string descricao, decimal tempoPadraoHoras)
    {
        Descricao = descricao;
        TempoPadraoHoras = tempoPadraoHoras;
    }
}
