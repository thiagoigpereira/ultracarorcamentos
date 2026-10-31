using System.Reflection;

namespace Domain.Entities;

public class Marca : Entity
{
    public string Nome { get; private set; } = string.Empty;

    private readonly List<Modelo> _modelos = new();
    public IReadOnlyCollection<Modelo> Modelos => _modelos.AsReadOnly();

    private Marca() { } 

    public Marca(string nome)
    {
        Nome = nome;
    }
}
