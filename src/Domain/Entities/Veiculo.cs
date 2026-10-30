namespace Domain.Entities;

public class Veiculo
{
    public Guid Id { get; private set; }
    public int OficinaId { get; private set; }
    public string Placa { get; private set; } = string.Empty;
    public Guid MarcaId { get; private set; }
    public Marca Marca { get; private set; } = null!;
    public Guid ModeloId { get; private set; }
    public Modelo Modelo { get; private set; } = null!;
    public int Ano { get; private set; }

    private Veiculo() { } 

    public Veiculo(int oficinaId, string placa, Guid marcaId, Guid modeloId, int ano)
    {
        Id = Guid.NewGuid();
        OficinaId = oficinaId;
        Placa = placa.ToUpper().Replace("-", "").Trim();
        MarcaId = marcaId;
        ModeloId = modeloId;
        Ano = ano;
    }
}