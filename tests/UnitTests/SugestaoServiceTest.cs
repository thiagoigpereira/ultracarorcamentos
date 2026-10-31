using System.Reflection;
using Application.DTOs;
using Application.Services;
using Domain.Entities;
using Domain.Exceptions;
using UnitTests.Fakes;

namespace UnitTests;

public class SugestaoServiceTests
{
    [Fact]
    public async Task GerarSugestaoAsync_DeveExigirDiagnostico()
    {
        var service = new SugestaoService(new FakeSugestaoRepository());
        var request = new CriarSugestaoRequest(Guid.NewGuid(), new List<Guid>());

        var exception = await Assert.ThrowsAsync<DomainException>(() => service.GerarSugestaoAsync(request));

        Assert.Equal("DIAGNOSTICO_OBRIGATORIO", exception.Code);
    }

    [Fact]
    public async Task GerarSugestaoAsync_DeveInformarQuandoVeiculoNaoExiste()
    {
        var service = new SugestaoService(new FakeSugestaoRepository());
        var request = new CriarSugestaoRequest(Guid.NewGuid(), new List<Guid> { Guid.NewGuid() });

        var exception = await Assert.ThrowsAsync<DomainException>(() => service.GerarSugestaoAsync(request));

        Assert.Equal("VEICULO_NAO_ENCONTRADO", exception.Code);
    }

    [Fact]
    public async Task GerarSugestaoAsync_DeveInformarQuandoDiagnosticoNaoExiste()
    {
        var repository = new FakeSugestaoRepository();
        var marca = new Marca("Volkswagen");
        var modelo = new Modelo("Gol", marca.Id);
        repository.Veiculo = new Veiculo(1, "ABC1D23", marca.Id, modelo.Id, 2020);
        var service = new SugestaoService(repository);
        var request = new CriarSugestaoRequest(repository.Veiculo.Id, new List<Guid> { Guid.NewGuid() });

        var exception = await Assert.ThrowsAsync<DomainException>(() => service.GerarSugestaoAsync(request));

        Assert.Equal("DIAGNOSTICO_NAO_ENCONTRADO", exception.Code);
    }

    [Fact]
    public async Task MotorSugestao_DiagnosticoFreioGol2019_RetornaServicoPastilhasEpecasCompativeis()
    {
        var repository = new FakeSugestaoRepository();
        var marca = new Marca("Volkswagen");
        var modelo = new Modelo("Gol 1.0", marca.Id);
        var veiculo = new Veiculo(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            42,
            "ABC1D23",
            marca.Id,
            modelo.Id,
            2019);
        SetProperty(veiculo, nameof(Veiculo.Marca), marca);
        SetProperty(veiculo, nameof(Veiculo.Modelo), modelo);
        repository.Veiculo = veiculo;

        var diagnostico = new Diagnostico(
            Guid.Parse("22222222-2222-2222-2222-111111111111"),
            "DIAG-FREIO-01",
            "Ruído na frenagem",
            "Freio");
        var servico = new Servico("Substituição de pastilhas dianteiras", 1.0m);
        var tipoPecaId = Guid.NewGuid();
        AddToCollection(
            servico,
            "_servicoTipoPecas",
            new ServicoTipoPeca(servico.Id, tipoPecaId, "Jogo de pastilhas dianteiras", 1));

        var diagnosticoServico = new DiagnosticoServico(diagnostico.Id, servico.Id);
        SetProperty(diagnosticoServico, nameof(DiagnosticoServico.Servico), servico);
        AddToCollection(diagnostico, "_diagnosticoServicos", diagnosticoServico);
        repository.Diagnosticos.Add(diagnostico);

        var compativel = new Peca("BB-1234", "Pastilha Bosch", tipoPecaId, 189.90m, true);
        var incompativel = new Peca("BB-2017", "Pastilha fora de aplicação", tipoPecaId, 99.90m, true);
        repository.PecasPorTipo[tipoPecaId] = new List<Peca> { compativel, incompativel };
        repository.CompatibilidadesPorPeca[compativel.Id] = new List<PecaCompatibilidade>
        {
            new(compativel.Id, modelo.Id, 2015, 2025)
        };
        repository.CompatibilidadesPorPeca[incompativel.Id] = new List<PecaCompatibilidade>
        {
            new(incompativel.Id, modelo.Id, 2010, 2017)
        };

        var service = new SugestaoService(repository);

        var response = await service.GerarSugestaoAsync(
            new CriarSugestaoRequest(veiculo.Id, new List<Guid> { diagnostico.Id }));

        Assert.Equal(2019, response.Veiculo.Ano);
        Assert.Equal("Gol 1.0", response.Veiculo.Modelo);

        var servicoSugerido = Assert.Single(response.Servicos);
        Assert.Equal("Substituição de pastilhas dianteiras", servicoSugerido.Descricao);

        var sugestaoPeca = Assert.Single(servicoSugerido.Pecas);
        Assert.Equal("Jogo de pastilhas dianteiras", sugestaoPeca.TipoPeca);
        Assert.False(sugestaoPeca.PecaPendente);
        Assert.Equal("BB-1234", Assert.Single(sugestaoPeca.Opcoes).Codigo);
    }

    [Fact]
    public async Task MotorSugestao_PecaCompativelAte2017EmVeiculo2019_RetornaPecaPendente()
    {
        var repository = CriarRepositorioComServico(out var tipoPecaId, out var diagnosticoId, ano: 2019);
        var peca = new Peca("BB-2017", "Pastilha fora de aplicação", tipoPecaId, 99.90m, true);
        repository.PecasPorTipo[tipoPecaId] = new List<Peca> { peca };
        repository.CompatibilidadesPorPeca[peca.Id] = new List<PecaCompatibilidade>
        {
            new(peca.Id, repository.Veiculo!.ModeloId, 2010, 2017)
        };
        var service = new SugestaoService(repository);

        var response = await service.GerarSugestaoAsync(
            new CriarSugestaoRequest(repository.Veiculo!.Id, new List<Guid> { diagnosticoId }));

        Assert.Equal(2019, response.Veiculo.Ano);
        var servico = Assert.Single(response.Servicos);
        var sugestaoPeca = Assert.Single(servico.Pecas);
        Assert.True(sugestaoPeca.PecaPendente);
        Assert.Empty(sugestaoPeca.Opcoes);
    }

    [Fact]
    public async Task T03_DiagnosticosComMesmoServico_DeveDeduplicarServico()
    {
        var repository = CriarRepositorioComServico(out var tipoPecaId, out var diagnosticoId);
        var servico = repository.Diagnosticos.Single().DiagnosticoServicos.Single().Servico;
        var diagnosticoDuplicado = new Diagnostico("D2", "Ruido secundario", "Freio");
        var vinculoDuplicado = new DiagnosticoServico(diagnosticoDuplicado.Id, servico.Id);
        SetProperty(vinculoDuplicado, nameof(DiagnosticoServico.Servico), servico);
        AddToCollection(diagnosticoDuplicado, "_diagnosticoServicos", vinculoDuplicado);
        repository.Diagnosticos.Add(diagnosticoDuplicado);
        repository.PecasPorTipo[tipoPecaId] = new List<Peca>
        {
            new("P1", "Peca comum", tipoPecaId, 50m, false),
            new("P2", "Peca preferencial cara", tipoPecaId, 100m, true),
            new("P3", "Peca preferencial barata", tipoPecaId, 75m, true)
        };
        foreach (var peca in repository.PecasPorTipo[tipoPecaId])
        {
            repository.CompatibilidadesPorPeca[peca.Id] = new List<PecaCompatibilidade>
            {
                new(peca.Id, repository.Veiculo!.ModeloId, 2015, 2025)
            };
        }
        var service = new SugestaoService(repository);
        var request = new CriarSugestaoRequest(repository.Veiculo!.Id, new List<Guid> { diagnosticoId, diagnosticoDuplicado.Id });

        var response = await service.GerarSugestaoAsync(request);

        var servicoResponse = Assert.Single(response.Servicos);
        Assert.Equal("Jogo de pastilhas", servicoResponse.Pecas[0].TipoPeca);
        Assert.Equal(new[] { "P3", "P2", "P1" }, servicoResponse.Pecas[0].Opcoes.Select(p => p.Codigo));
        Assert.False(servicoResponse.Pecas[0].PecaPendente);
    }

    [Fact]
    public async Task GerarSugestaoAsync_DeveMarcarPecaPendenteQuandoNaoHouverOpcoes()
    {
        var repository = CriarRepositorioComServico(out _, out var diagnosticoId);
        var service = new SugestaoService(repository);
        var request = new CriarSugestaoRequest(repository.Veiculo!.Id, new List<Guid> { diagnosticoId });

        var response = await service.GerarSugestaoAsync(request);

        Assert.True(Assert.Single(Assert.Single(response.Servicos).Pecas).PecaPendente);
    }

    private static FakeSugestaoRepository CriarRepositorioComServico(out Guid tipoPecaId, out Guid diagnosticoId, int ano = 2020)
    {
        var repository = new FakeSugestaoRepository();
        var marca = new Marca("Volkswagen");
        var modelo = new Modelo("Gol", marca.Id);
        var veiculo = new Veiculo(1, "ABC1D23", marca.Id, modelo.Id, ano);
        SetProperty(veiculo, nameof(Veiculo.Marca), marca);
        SetProperty(veiculo, nameof(Veiculo.Modelo), modelo);
        repository.Veiculo = veiculo;

        var servico = new Servico("Troca de pastilhas", 1m) { ValorMaoDeObra = 120m };
        tipoPecaId = Guid.NewGuid();
        AddToCollection(servico, "_servicoTipoPecas", new ServicoTipoPeca(servico.Id, tipoPecaId, "Jogo de pastilhas", 1));

        var diagnostico = new Diagnostico("D1", "Ruido", "Freio");
        diagnosticoId = diagnostico.Id;
        var diagnosticoServico = new DiagnosticoServico(diagnostico.Id, servico.Id);
        SetProperty(diagnosticoServico, nameof(DiagnosticoServico.Servico), servico);
        AddToCollection(diagnostico, "_diagnosticoServicos", diagnosticoServico);
        repository.Diagnosticos.Add(diagnostico);

        return repository;
    }

    private static void AddToCollection(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        var collection = field?.GetValue(target) as System.Collections.IList;
        collection?.Add(value);
    }

    private static void SetProperty(object target, string propertyName, object value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        property?.SetValue(target, value);
    }
}
