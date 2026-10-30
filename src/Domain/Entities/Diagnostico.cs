namespace Domain.Entities;

public class Diagnostico
{
    public Guid Id { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public string Descricao { get; private set; } = string.Empty;
    public string Sistema { get; private set; } = string.Empty;

    private readonly List<DiagnosticoServico> _diagnosticoServicos = new();
    public IReadOnlyCollection<DiagnosticoServico> DiagnosticoServicos => _diagnosticoServicos.AsReadOnly();

    private Diagnostico() { } 

    public Diagnostico(string codigo, string descricao, string sistema)
    {
        Id = Guid.NewGuid();
        Codigo = codigo;
        Descricao = descricao;
        Sistema = sistema;
    }
}