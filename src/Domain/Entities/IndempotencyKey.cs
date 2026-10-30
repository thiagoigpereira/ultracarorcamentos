namespace Domain.Entities;

public class IdempotencyKey
{
    public string Chave { get; private set; } = string.Empty;
    public int OficinaId { get; private set; }
    public string Rota { get; private set; } = string.Empty;
    public string HashRequest { get; private set; } = string.Empty;
    public string RespostaJson { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }
    public DateTime CriadoEm { get; private set; }

    private IdempotencyKey() { } 

    public IdempotencyKey(string chave, int oficinaId, string rota, string hashRequest, string respostaJson, int statusCode, DateTime criadoEm)
    {
        Chave = chave;
        OficinaId = oficinaId;
        Rota = rota;
        HashRequest = hashRequest;
        RespostaJson = respostaJson;
        StatusCode = statusCode;
        CriadoEm = criadoEm;
    }
}