namespace Domain.Entities;

public class Veiculo : Entity
{
    public int OficinaId { get; private set; }
    public string Placa { get; private set; }
    public Guid MarcaId { get; private set; }
    public Marca Marca { get; private set; } = null!;
    public Guid ModeloId { get; private set; }
    public Modelo Modelo { get; private set; } = null!;
    public int Ano { get; private set; }

    public Veiculo(int oficinaId, string placa, Guid marcaId, Guid modeloId, int ano)
    {
        OficinaId = oficinaId;
        Placa = placa;
        MarcaId = marcaId;
        ModeloId = modeloId;
        Ano = ano;
    }

    public Veiculo(Guid id, int oficinaId, string placa, Guid marcaId, Guid modeloId, int ano)
        : this(oficinaId, placa, marcaId, modeloId, ano)
    {
        Id = id;
    }
}
