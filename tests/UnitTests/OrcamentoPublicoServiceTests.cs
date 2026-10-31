using Application.DTOs;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using UnitTests.Fakes;

namespace UnitTests;

public sealed class OrcamentoPublicoServiceTests
{
    [Fact]
    public async Task ObterPorTokenAsync_TokenValido_DeveRetornarOrcamento()
    {
        var (repository, budget) = CriarRepositorioComOrcamentoEnviado();
        var service = new OrcamentoPublicoService(repository);

        var result = await service.ObterPorTokenAsync(budget.TokenPublico!);

        Assert.True(result.Sucesso);
        Assert.NotNull(result.Dados);
    }

    [Fact]
    public async Task ObterPorTokenAsync_TokenInexistente_DeveRetornarNaoEncontrado()
    {
        var service = new OrcamentoPublicoService(new FakeOrcamentoRepository());

        var result = await service.ObterPorTokenAsync("missing-token");

        Assert.False(result.Sucesso);
        Assert.Equal("ORC_NAO_ENCONTRADO", result.CodigoErro);
    }

    [Fact]
    public async Task ObterPorTokenAsync_TokenExpirado_DeveRetornarExpirado()
    {
        var (repository, budget) = CriarRepositorioComOrcamentoEnviado(DateTime.UtcNow.AddDays(-8));
        var service = new OrcamentoPublicoService(repository);

        var result = await service.ObterPorTokenAsync(budget.TokenPublico!);

        Assert.False(result.Sucesso);
        Assert.Equal("ORC_LINK_EXPIRADO", result.CodigoErro);
    }

    [Fact]
    public async Task RegistrarDecisaoAsync_AprovacaoTotal_DevePersistirStatusAprovado()
    {
        var (repository, budget) = CriarRepositorioComOrcamentoEnviado();
        var service = new OrcamentoPublicoService(repository);

        var result = await service.RegistrarDecisaoAsync(
            budget.TokenPublico!,
            new DecisaoClienteRequest("Total", null, null));

        Assert.True(result.Sucesso);
        Assert.Equal(StatusOrcamento.AprovadoTotal, budget.Status);
        Assert.Equal(150m, budget.ValorAprovado);
        Assert.Single(repository.Historicos);
    }

    [Fact]
    public async Task RegistrarDecisaoAsync_Recusa_ComMotivo_DevePersistirMotivoEHistorico()
    {
        var (repository, budget) = CriarRepositorioComOrcamentoEnviado();
        var service = new OrcamentoPublicoService(repository);

        var result = await service.RegistrarDecisaoAsync(
            budget.TokenPublico!,
            new DecisaoClienteRequest("Recusa", null, "Vou fazer o reparo depois."));

        Assert.True(result.Sucesso);
        Assert.Equal("Vou fazer o reparo depois.", budget.MotivoRecusa);
        Assert.Single(repository.Historicos);
    }

    [Fact]
    public async Task RegistrarDecisaoAsync_TokenInexistente_DeveRetornarNaoEncontrado()
    {
        var service = new OrcamentoPublicoService(new FakeOrcamentoRepository());

        var result = await service.RegistrarDecisaoAsync(
            "missing-token",
            new DecisaoClienteRequest("Total", null, null));

        Assert.False(result.Sucesso);
        Assert.Equal("ORC_NAO_ENCONTRADO", result.CodigoErro);
    }

    [Fact]
    public async Task RegistrarDecisaoAsync_TokenExpirado_DeveRetornarExpirado()
    {
        var (repository, budget) = CriarRepositorioComOrcamentoEnviado(DateTime.UtcNow.AddDays(-8));
        var service = new OrcamentoPublicoService(repository);

        var result = await service.RegistrarDecisaoAsync(
            budget.TokenPublico!,
            new DecisaoClienteRequest("Total", null, null));

        Assert.False(result.Sucesso);
        Assert.Equal("ORC_LINK_EXPIRADO", result.CodigoErro);
    }

    [Fact]
    public async Task RegistrarDecisaoAsync_DecisaoInvalida_DeveRetornarErroDeDominio()
    {
        var (repository, budget) = CriarRepositorioComOrcamentoEnviado();
        var service = new OrcamentoPublicoService(repository);

        var result = await service.RegistrarDecisaoAsync(
            budget.TokenPublico!,
            new DecisaoClienteRequest("Parcial", null, null));

        Assert.False(result.Sucesso);
        Assert.Equal("ORC_DECISAO_INVALIDA", result.CodigoErro);
    }

    [Fact]
    public async Task RegistrarDecisaoAsync_DecisaoJaRegistrada_DeveRetornarErroDeConflito()
    {
        var (repository, budget) = CriarRepositorioComOrcamentoEnviado();
        var service = new OrcamentoPublicoService(repository);
        var request = new DecisaoClienteRequest("Total", null, null);
        await service.RegistrarDecisaoAsync(budget.TokenPublico!, request);

        var result = await service.RegistrarDecisaoAsync(budget.TokenPublico!, request);

        Assert.False(result.Sucesso);
        Assert.Equal("ORC_DECISAO_JA_REGISTRADA", result.CodigoErro);
        Assert.Equal(2, repository.Historicos.Count);
    }

    private static (FakeOrcamentoRepository Repository, Orcamento Budget) CriarRepositorioComOrcamentoEnviado(DateTime? enviadoEm = null)
    {
        var repository = new FakeOrcamentoRepository();
        var budget = new Orcamento(42, 123, Guid.NewGuid(), Guid.NewGuid());
        budget.SubstituirItens(new[]
        {
            new OrcamentoItem(budget.Id, "Servico", Guid.NewGuid(), null, "Serviço", 1, 150m)
        });
        budget.Enviar(1001, enviadoEm ?? DateTime.UtcNow, "Email");
        repository.Orcamentos.Add(budget);
        return (repository, budget);
    }
}
