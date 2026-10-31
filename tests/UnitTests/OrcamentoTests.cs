using System.Reflection;
using Application.Common;
using Application.DTOs;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using UnitTests.Fakes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UnitTests;

public class OrcamentoTests
{
    [Fact]
    public void CriarOrcamento_DeveInicializarDadosBasicos()
    {
        var veiculoId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();

        var orcamento = new Orcamento(10, 20, veiculoId, clienteId);

        Assert.NotEqual(Guid.Empty, orcamento.Id);
        Assert.Equal(10, orcamento.OficinaId);
        Assert.Equal(20, orcamento.OrdemServicoId);
        Assert.Equal(veiculoId, orcamento.VeiculoId);
        Assert.Equal(clienteId, orcamento.ClienteId);
        Assert.Equal(StatusOrcamento.Rascunho, orcamento.Status);
        Assert.Equal(1, orcamento.Versao);
    }

    [Fact]
    public void AprovarTotal_DeveDefinirValorAprovado()
    {
        var orcamento = CriarOrcamentoEnviado(250m);

        orcamento.AprovarTotal();

        Assert.Equal(StatusOrcamento.AprovadoTotal, orcamento.Status);
        Assert.Equal(250m, orcamento.ValorAprovado);
    }

    [Fact]
    public void AprovarParcialmente_ComValorInvalidoDeveLancarExcecao()
    {
        var orcamento = CriarOrcamentoEnviado(250m);

        Assert.Throws<InvalidOperationException>(() => orcamento.AprovarParcialmente(250m));
        Assert.Throws<InvalidOperationException>(() => orcamento.AprovarParcialmente(0m));
    }

    [Fact]
    public void Recusar_DeveDefinirValorAprovadoComoZero()
    {
        var orcamento = CriarOrcamentoEnviado(250m);

        orcamento.Recusar();

        Assert.Equal(StatusOrcamento.Recusado, orcamento.Status);
        Assert.Equal(0m, orcamento.ValorAprovado);
    }

    [Fact]
    public void CancelarOrcamentoAprovado_DeveLancarExcecao()
    {
        var orcamento = CriarOrcamentoEnviado(250m);
        orcamento.AprovarTotal();

        Assert.Throws<InvalidOperationException>(() => orcamento.Cancelar());
    }

    [Fact]
    public void ResultadoOperacao_DeveRepresentarSucessoEFalha()
    {
        var resposta = new OrcamentoResponse(
            Guid.NewGuid(), 1, 2, Guid.NewGuid(), Guid.NewGuid(), "Rascunho", 1, 0m, 0m, false, null, string.Empty, new());

        var sucesso = ResultadoOperacao.OK(resposta);
        var falha = ResultadoOperacao.Falha("ERRO", "Mensagem");

        Assert.True(sucesso.Sucesso);
        Assert.Same(resposta, sucesso.Orcamento);
        Assert.False(falha.Sucesso);
        Assert.Equal("ERRO", falha.CodigoErro);
        Assert.Equal("Mensagem", falha.MensagemErro);
    }

    [Fact]
    public async Task CriarOrcamentoAsync_DeveUsarDadosDaRequisicao()
    {
        var service = new OrcamentoService(new FakeOrcamentoRepository(), new FakeOutboxRepository(), new FakeIdempotencyRepository(), new FakeUnitOfWork());
        var request = new CriarOrcamentoRequest(30, 40, Guid.NewGuid(), Guid.NewGuid());

        var orcamento = await service.CriarOrcamentoAsync(request);

        Assert.Equal(request.OficinaId, orcamento.OficinaId);
        Assert.Equal(request.OrdemServicoId, orcamento.OrdemServicoId);
        Assert.Equal(request.VeiculoId, orcamento.VeiculoId);
        Assert.Equal(request.ClienteId, orcamento.ClienteId);
    }

    [Fact]
    public void Enviar_RascunhoValido_DeveGerarTokenEAlterarStatus()
    {
        var orcamento = new Orcamento(1, 2, Guid.NewGuid(), Guid.NewGuid());
        orcamento.SubstituirItens(new[] { new OrcamentoItem(orcamento.Id, "Servico", Guid.NewGuid(), null, "Serviço", 1, 100m) });

        orcamento.Enviar(1001, DateTime.UtcNow, "Email");

        Assert.Equal(StatusOrcamento.Enviado, orcamento.Status);
        Assert.False(string.IsNullOrWhiteSpace(orcamento.TokenPublico));
        Assert.True(orcamento.TokenExpiraEm > DateTime.UtcNow.AddDays(6));
    }

    [Fact]
    public async Task T04_EnviarOrcamentoValido_DevePersistirEventoDeOutbox()
    {
        var repository = new FakeOrcamentoRepository();
        var outbox = new FakeOutboxRepository();
        var idempotency = new FakeIdempotencyRepository();
        var service = new OrcamentoService(repository, outbox, idempotency, new FakeUnitOfWork());
        var orcamento = CriarEnviavel();
        repository.Orcamentos.Add(orcamento);

        var resultado = await service.EnviarAsync(
            orcamento.Id,
            orcamento.OficinaId,
            orcamento.RowVersion,
            "t04",
            1001,
            new EnviarOrcamentoRequest("Email", "Teste"));

        Assert.True(resultado.Sucesso);
        Assert.Equal("Enviado", orcamento.Status.ToString());
        var evento = Assert.Single(outbox.Messages);
        Assert.Equal("OrcamentoEnviado", evento.Type);
        Assert.Contains(orcamento.Id.ToString(), evento.Payload);
        Assert.Equal($"{orcamento.Id:N}:1", evento.DeduplicationKey);
    }

    [Fact]
    public async Task ObterOuCriarPorOS_OrcamentoExistente_DeveRetornarSemCriarOutro()
    {
        var repository = new FakeOrcamentoRepository();
        var existing = new Orcamento(42, 123, Guid.NewGuid(), Guid.NewGuid());
        repository.Orcamentos.Add(existing);
        var service = CriarService(repository);

        var (result, created) = await service.ObterOuCriarPorOSAsync(
            42,
            123,
            new CriarOrcamentoRequest(42, 123, Guid.NewGuid(), Guid.NewGuid()));

        Assert.False(created);
        Assert.Same(existing, result);
        Assert.Single(repository.Orcamentos);
    }

    [Fact]
    public async Task ObterOuCriarPorOS_OrcamentoAusente_DeveCriarNovo()
    {
        var repository = new FakeOrcamentoRepository();
        var service = CriarService(repository);
        var request = new CriarOrcamentoRequest(42, 123, Guid.NewGuid(), Guid.NewGuid());

        var (result, created) = await service.ObterOuCriarPorOSAsync(42, 123, request);

        Assert.True(created);
        Assert.Equal(42, result.OficinaId);
        Assert.Equal(123, result.OrdemServicoId);
        Assert.Single(repository.Orcamentos);
    }

    [Fact]
    public async Task ObterPorIdAsync_OrcamentoInexistente_DeveRetornarNulo()
    {
        var service = CriarService(new FakeOrcamentoRepository());

        var result = await service.ObterPorIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task AtualizarItensAsync_VersaoAtual_DeveAtualizarItensETotal()
    {
        var repository = new FakeOrcamentoRepository();
        var orcamento = new Orcamento(42, 123, Guid.NewGuid(), Guid.NewGuid());
        repository.Orcamentos.Add(orcamento);
        var service = CriarService(repository);
        var item = new ItemOrcamentoInputDto("Servico", Guid.NewGuid(), null, "Mão de obra", 2, 75m);

        var result = await service.AtualizarItensAsync(
            orcamento.Id,
            orcamento.OficinaId,
            orcamento.RowVersion,
            1001,
            new AtualizarItensRequest(new List<ItemOrcamentoInputDto> { item }));

        Assert.True(result.Sucesso);
        Assert.Equal(150m, result.Orcamento!.Total);
        Assert.Equal(2, Assert.Single(result.Orcamento.Itens).Quantidade);
    }

    [Fact]
    public async Task AtualizarItensAsync_DescontoPercentual_DeveCalcularTotalERegistrarHistorico()
    {
        var repository = new FakeOrcamentoRepository();
        var budget = new Orcamento(42, 123, Guid.NewGuid(), Guid.NewGuid());
        repository.Orcamentos.Add(budget);
        var service = CriarService(repository);
        var items = new AtualizarItensRequest(
            new List<ItemOrcamentoInputDto>
            {
                new("Servico", Guid.NewGuid(), null, "Serviço", 1, 333.33m)
            },
            10m,
            DescontoPercentual: true);

        var result = await service.AtualizarItensAsync(budget.Id, budget.OficinaId, budget.RowVersion, 1001, items);

        Assert.True(result.Sucesso);
        Assert.Equal(300m, result.Orcamento!.Total);
        Assert.Equal(10m, result.Orcamento.Desconto);
        Assert.True(result.Orcamento.DescontoPercentual);
        Assert.Single(repository.Historicos);
    }

    [Fact]
    public async Task AtualizarItensAsync_OrcamentoInexistente_DeveRetornarFalha()
    {
        var service = CriarService(new FakeOrcamentoRepository());

        var result = await service.AtualizarItensAsync(
            Guid.NewGuid(),
            42,
            Guid.NewGuid().ToByteArray(),
            1001,
            new AtualizarItensRequest(new()));

        Assert.False(result.Sucesso);
        Assert.Equal("ORC_NAO_ENCONTRADO", result.CodigoErro);
    }

    [Fact]
    public async Task AtualizarItensAsync_VersaoDesatualizada_DeveRetornarFalha()
    {
        var repository = new FakeOrcamentoRepository();
        var orcamento = new Orcamento(42, 123, Guid.NewGuid(), Guid.NewGuid());
        repository.Orcamentos.Add(orcamento);
        var service = CriarService(repository);

        var result = await service.AtualizarItensAsync(
            orcamento.Id,
            orcamento.OficinaId,
            Guid.NewGuid().ToByteArray(),
            1001,
            new AtualizarItensRequest(new()));

        Assert.False(result.Sucesso);
        Assert.Equal("ORC_VERSAO_DESATUALIZADA", result.CodigoErro);
    }

    [Fact]
    public async Task AtualizarItensAsync_OrcamentoEnviado_DeveImpedirEdicao()
    {
        var repository = new FakeOrcamentoRepository();
        var orcamento = CriarEnviavel();
        orcamento.Enviar(1001, DateTime.UtcNow, "Email");
        repository.Orcamentos.Add(orcamento);
        var service = CriarService(repository);

        var result = await service.AtualizarItensAsync(
            orcamento.Id,
            orcamento.OficinaId,
            orcamento.RowVersion,
            1001,
            new AtualizarItensRequest(new()));

        Assert.False(result.Sucesso);
        Assert.Equal("ORC_EDICAO_BLOQUEADA", result.CodigoErro);
        Assert.Contains(repository.Historicos, history => history.Detalhes == "Tentativa de edição rejeitada: orçamento fora de rascunho.");
    }

    [Fact]
    public async Task EnviarAsync_OrcamentoInexistente_DeveRetornarFalha()
    {
        var service = CriarService(new FakeOrcamentoRepository());

        var result = await service.EnviarAsync(
            Guid.NewGuid(), 42, Guid.NewGuid().ToByteArray(), "key", 1001,
            new EnviarOrcamentoRequest("Email", ""));

        Assert.False(result.Sucesso);
        Assert.Equal("ORC_NAO_ENCONTRADO", result.CodigoErro);
    }

    [Fact]
    public async Task EnviarAsync_VersaoDesatualizada_DeveRetornarFalha()
    {
        var repository = new FakeOrcamentoRepository();
        var orcamento = CriarEnviavel();
        repository.Orcamentos.Add(orcamento);
        var service = CriarService(repository);

        var result = await service.EnviarAsync(
            orcamento.Id, orcamento.OficinaId, Guid.NewGuid().ToByteArray(), "key", 1001,
            new EnviarOrcamentoRequest("Email", ""));

        Assert.False(result.Sucesso);
        Assert.Equal("ORC_VERSAO_DESATUALIZADA", result.CodigoErro);
        Assert.Contains(repository.Historicos, history => history.Detalhes!.Contains("ETag desatualizado"));
    }

    [Fact]
    public async Task EnviarAsync_MesmaChaveComOutraRequisicao_DeveRetornarFalha()
    {
        const string key = "same-key";
        const string responseJson = "{\"status\":\"Enviado\"}";
        var repository = new FakeOrcamentoRepository();
        var idempotency = new FakeIdempotencyRepository();
        idempotency.Items.Add(new IdempotencyKey(key, 42, "/route", "different-hash", responseJson, 200, DateTime.UtcNow));
        var service = new OrcamentoService(repository, new FakeOutboxRepository(), idempotency, new FakeUnitOfWork());

        var result = await service.EnviarAsync(
            Guid.NewGuid(), 42, Array.Empty<byte>(), key, 1001,
            new EnviarOrcamentoRequest("Email", "original"));

        Assert.False(result.Sucesso);
        Assert.Equal("IDEMPOTENCY_KEY_REUTILIZADA", result.CodigoErro);
    }

    [Fact]
    public async Task EnviarAsync_MesmaChaveERequisicao_DeveRepetirRespostaCacheada()
    {
        const string key = "same-key";
        const string responseJson = "{\"status\":\"Enviado\"}";
        var request = new EnviarOrcamentoRequest("Email", "same request");
        var orcamentoId = Guid.NewGuid();
        var repository = new FakeOrcamentoRepository();
        var idempotency = new FakeIdempotencyRepository();
        idempotency.Items.Add(new IdempotencyKey(key, 42, $"/orcamentos/{orcamentoId}/envio", Hash(request), responseJson, 200, DateTime.UtcNow));
        var service = new OrcamentoService(repository, new FakeOutboxRepository(), idempotency, new FakeUnitOfWork());

        var result = await service.EnviarAsync(orcamentoId, 42, Array.Empty<byte>(), key, 1001, request);

        Assert.True(result.FoiReprocessado);
        Assert.Equal(responseJson, result.RespostaJson);
    }

    [Fact]
    public async Task EnviarAsync_MesmaChaveEConteudoEmOutroOrcamento_DeveRetornarFalha()
    {
        const string key = "same-key";
        var request = new EnviarOrcamentoRequest("Email", "same request");
        var repository = new FakeOrcamentoRepository();
        var idempotency = new FakeIdempotencyRepository();
        idempotency.Items.Add(new IdempotencyKey(key, 42, "/orcamentos/outro-id/envio", Hash(request), "cached-response", 200, DateTime.UtcNow));
        var service = new OrcamentoService(repository, new FakeOutboxRepository(), idempotency, new FakeUnitOfWork());

        var result = await service.EnviarAsync(Guid.NewGuid(), 42, Array.Empty<byte>(), key, 1001, request);

        Assert.False(result.Sucesso);
        Assert.Equal("IDEMPOTENCY_KEY_REUTILIZADA", result.CodigoErro);
    }

    [Fact]
    public async Task CriarRevisaoAsync_OrcamentoEnviado_DeveInvalidarTokenEIncrementarVersao()
    {
        var repository = new FakeOrcamentoRepository();
        var orcamento = CriarEnviavel();
        orcamento.Enviar(1001, DateTime.UtcNow, "Email");
        repository.Orcamentos.Add(orcamento);
        var service = CriarService(repository);

        var result = await service.CriarRevisaoAsync(orcamento.Id, 1001);

        Assert.Equal(StatusOrcamento.Rascunho, result.Status);
        Assert.Equal(2, result.Versao);
        Assert.Null(result.TokenPublico);
        Assert.Equal(StatusOrcamento.Rascunho, Assert.Single(repository.Historicos).Para);
    }

    [Fact]
    public async Task CriarRevisaoAsync_OrcamentoInexistente_DeveLancarExcecaoDeDominio()
    {
        var service = CriarService(new FakeOrcamentoRepository());

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            service.CriarRevisaoAsync(Guid.NewGuid(), 1001));

        Assert.Equal("ORC_NAO_ENCONTRADO", exception.Code);
    }

    [Fact]
    public async Task ObterHistoricoAsync_DeveRetornarEventosDoOrcamento()
    {
        var repository = new FakeOrcamentoRepository();
        var orcamentoId = Guid.NewGuid();
        var historico = new OrcamentoHistorico(
            orcamentoId, null, StatusOrcamento.Rascunho, "1001", "API", DateTime.UtcNow);
        repository.Historicos.Add(historico);
        var service = CriarService(repository);

        var result = await service.ObterHistoricoAsync(orcamentoId);

        Assert.Same(historico, Assert.Single(result));
    }

    [Fact]
    public void T05_Enviar_OrcamentoJaEnviado_DeveRetornarConflito()
    {
        var orcamento = CriarEnviavel();
        orcamento.Enviar(1001, DateTime.UtcNow, "Email");

        var exception = Assert.Throws<DomainException>(() => orcamento.Enviar(1001, DateTime.UtcNow, "Email"));

        Assert.Equal("ORC_JA_ENVIADO", exception.Code);
    }

    [Fact]
    public void T06_Enviar_ComPecaPendente_DeveBloquearEnvio()
    {
        var orcamento = new Orcamento(1, 2, Guid.NewGuid(), Guid.NewGuid());
        orcamento.SubstituirItens(new[] { new OrcamentoItem(orcamento.Id, "Peca", Guid.NewGuid(), null, "Peça", 1, 100m, true) });

        var exception = Assert.Throws<DomainException>(() => orcamento.Enviar(1001, DateTime.UtcNow, "Email"));

        Assert.Equal("ORC_PECA_PENDENTE", exception.Code);
    }

    [Fact]
    public void T07_Decidir_AprovandoPecaComServicoRecusado_DeveFalhar()
    {
        var orcamento = CriarComServicoEPeca();
        var parent = orcamento.Itens.First();
        var child = orcamento.Itens.Skip(1).First();
        orcamento.Enviar(1001, DateTime.UtcNow, "Email");

        var exception = Assert.Throws<DomainException>(() => orcamento.AplicarDecisao("Parcial", new Dictionary<Guid, string>
        {
            [parent.Id] = "Recusado",
            [child.Id] = "Aprovado"
        }));

        Assert.Equal("ORC_PECA_SEM_SERVICO", exception.Code);
    }

    [Fact]
    public void T08_Decidir_ParcialValido_DeveCalcularValorAprovado()
    {
        var orcamento = CriarComServicoEPeca();
        var parent = orcamento.Itens.First();
        var child = orcamento.Itens.Skip(1).First();
        orcamento.Enviar(1001, DateTime.UtcNow, "Email");

        orcamento.AplicarDecisao("Parcial", new Dictionary<Guid, string>
        {
            [parent.Id] = "Aprovado",
            [child.Id] = "Recusado"
        });

        Assert.Equal(StatusOrcamento.AprovadoParcial, orcamento.Status);
        Assert.Equal(parent.ValorTotal, orcamento.ValorAprovado);
    }

    [Fact]
    public void T09_Decidir_SegundaTentativa_DeveRetornarConflito()
    {
        var orcamento = CriarEnviavel();
        orcamento.Enviar(1001, DateTime.UtcNow, "Email");
        orcamento.AplicarDecisao("Total");

        var exception = Assert.Throws<DomainException>(() => orcamento.AplicarDecisao("Total"));

        Assert.Equal("ORC_DECISAO_JA_REGISTRADA", exception.Code);
    }

    [Fact]
    public void T10_Decidir_ComTokenExpirado_DeveRetornarConflito()
    {
        var orcamento = CriarEnviavel();
        orcamento.Enviar(1001, DateTime.UtcNow.AddDays(-8), "Email");

        var exception = Assert.Throws<DomainException>(() => orcamento.AplicarDecisao("Total"));

        Assert.Equal("ORC_LINK_EXPIRADO", exception.Code);
    }

    [Fact]
    public void T11_Calculo_DescontoDeDezPorCento_DeveArredondarParaDuasCasas()
    {
        var orcamento = new Orcamento(1, 2, Guid.NewGuid(), Guid.NewGuid());
        var desconto = Math.Round(333.33m * 0.10m, 2, MidpointRounding.AwayFromZero);
        orcamento.SubstituirItens(
            new[] { new OrcamentoItem(orcamento.Id, "Servico", Guid.NewGuid(), null, "Serviço", 1, 333.33m) },
            desconto);

        Assert.Equal(300.00m, orcamento.Total);
    }

    [Fact]
    public async Task T12_TransicaoAtomicaRecusada_NaoDeveCriarOutbox()
    {
        var repository = new FakeOrcamentoRepository { EnvioPermitido = false, UsuarioQueVenceuAoFalhar = 2002 };
        var outbox = new FakeOutboxRepository();
        var service = new OrcamentoService(repository, outbox, new FakeIdempotencyRepository(), new FakeUnitOfWork());
        var orcamento = CriarEnviavel();
        repository.Orcamentos.Add(orcamento);

        var resultado = await service.EnviarAsync(orcamento.Id, 42, orcamento.RowVersion, "key", 1001, new EnviarOrcamentoRequest("Email", ""));

        Assert.False(resultado.Sucesso);
        Assert.Equal("ORC_JA_ENVIADO", resultado.CodigoErro);
        Assert.Equal(2002, resultado.EnviadoPor!.Id);
        Assert.Contains(repository.Historicos, history => history.Detalhes!.Contains("já enviado"));
        Assert.Empty(outbox.Messages);
    }

    private static Orcamento CriarOrcamentoEnviado(decimal total)
    {
        var orcamento = new Orcamento(1, 2, Guid.NewGuid(), Guid.NewGuid());
        SetProperty(orcamento, nameof(Orcamento.Total), total);
        SetProperty(orcamento, nameof(Orcamento.Status), StatusOrcamento.Enviado);
        return orcamento;
    }

    private static Orcamento CriarEnviavel()
    {
        var orcamento = new Orcamento(1, 2, Guid.NewGuid(), Guid.NewGuid());
        orcamento.SubstituirItens(new[] { new OrcamentoItem(orcamento.Id, "Servico", Guid.NewGuid(), null, "Serviço", 1, 100m) });
        return orcamento;
    }

    private static OrcamentoService CriarService(FakeOrcamentoRepository repository)
        => new(repository, new FakeOutboxRepository(), new FakeIdempotencyRepository(), new FakeUnitOfWork());

    private static string Hash(object request)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));

    private static Orcamento CriarComServicoEPeca()
    {
        var orcamento = new Orcamento(1, 2, Guid.NewGuid(), Guid.NewGuid());
        var servico = new OrcamentoItem(orcamento.Id, "Servico", Guid.NewGuid(), null, "Serviço", 1, 100m);
        var peca = new OrcamentoItem(orcamento.Id, "Peca", Guid.NewGuid(), servico.Id, "Peça", 1, 50m);
        orcamento.SubstituirItens(new[] { servico, peca });
        return orcamento;
    }

    private static void SetProperty(object target, string propertyName, object value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        property?.SetValue(target, value);
    }
}
