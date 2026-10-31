namespace Domain.Entities;

public class Modelo : Entity
{
    public string Nome { get; private set; } = string.Empty;
    public Guid MarcaId { get; private set; }
    public Marca Marca { get; private set; } = null!;

    private Modelo() { } 

    public Modelo(string nome, Guid marcaId)
    {
        Nome = nome;
        MarcaId = marcaId;
    }
}
