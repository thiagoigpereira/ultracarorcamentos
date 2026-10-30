using System.Reflection;

namespace Domain.Entities;

public class Marca
{
    public Guid Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;

    private readonly List<Modelo> _modelos = new();
    public IReadOnlyCollection<Modelo> Modelos => _modelos.AsReadOnly();

    private Marca() { } 

    public Marca(string nome)
    {
        Id = Guid.NewGuid();
        Nome = nome;
    }
}