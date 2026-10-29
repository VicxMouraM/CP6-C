using DominoQuina.Core.Estrategia;
using DominoQuina.Core.Modelos;
using DominoQuina.Core.Regras;
using DominoQuina.Core.Servicos;
using static DominoQuina.Tests.Apoio.Fabrica;

namespace DominoQuina.Tests.Unidade;

public class EstrategiaEDecisorTests
{
    private readonly EstrategiaPontuadora _estrategia = new();

    [Fact]
    public void Estrategia_PrefereAJogadaQuePontua()
    {
        // pontas 6 e 1. [1|4] na direita faz 6+4=10 (2 pts); [6|2] na esquerda faz 2+1=3 (0 pt).
        var estado = Estado(mao: Pecas((6, 2), (1, 4)), mesa: Pecas((6, 1)));

        var jogada = _estrategia.Escolher(estado);

        Assert.Equal(new Jogada(P(1, 4), LadoJogavel.Direita), jogada);
    }

    [Fact]
    public void Estrategia_AberturaObrigatoria_JogaAPedraIndicada()
    {
        var estado = Estado(proxima: ProximaAcaoPartida.JogarAbertura, mao: Pecas((5, 5), (6, 6)), abertura: P(6, 6));

        Assert.Equal(P(6, 6), _estrategia.Escolher(estado)!.Peca);
    }

    [Fact]
    public void Estrategia_SemEncaixe_RetornaNulo() =>
        Assert.Null(_estrategia.Escolher(Estado(mao: Pecas((0, 0)), mesa: Pecas((5, 6)))));

    [Fact]
    public void Estrategia_EscolheSempreUmaJogadaValida()
    {
        var aleatorio = new Random(42);
        for (var rodada = 0; rodada < 200; rodada++)
        {
            var todas = Enumerable.Range(0, 7).SelectMany(a => Enumerable.Range(a, 7 - a).Select(b => P(a, b))).OrderBy(_ => aleatorio.Next()).ToList();
            var mesa = new List<Peca> { todas[0] };
            var mao = todas.Skip(1).Take(7).ToList();

            var jogada = _estrategia.Escolher(Estado(mao: mao, mesa: mesa));

            if (jogada is not null)
            {
                Assert.Contains(jogada, MotorRegras.JogadasPossiveis(mao, mesa));
            }
            else
            {
                Assert.Empty(MotorRegras.JogadasPossiveis(mao, mesa));
            }
        }
    }

    [Fact]
    public void Risco_ConsideraApenasPedrasDesconhecidas()
    {
        // Pontas 5 e 0: o adversário pontuaria com [0|5] (5+5=10) — mas ela está na nossa mão.
        var risco = EstrategiaPontuadora.RiscoParaAdversario(new Pontas(5, 0), Pecas((0, 5)), Pecas((5, 3), (3, 0)));
        Assert.True(risco >= 1);
    }

    [Theory]
    [InlineData(ProximaAcaoPartida.Comprar, TipoAcao.Comprar)]
    [InlineData(ProximaAcaoPartida.Passar, TipoAcao.Passar)]
    [InlineData(ProximaAcaoPartida.Aguardar, TipoAcao.Aguardar)]
    [InlineData(ProximaAcaoPartida.Encerrada, TipoAcao.Encerrar)]
    public void Decisor_SegueAProximaAcaoInformadaPelaApi(ProximaAcaoPartida proxima, TipoAcao esperado) =>
        Assert.Equal(esperado, DecisorAcao.Decidir(Estado(proxima: proxima), _estrategia).Tipo);

    [Fact]
    public void Decisor_Jogar_ComEncaixe_DevolveAJogada()
    {
        var decisao = DecisorAcao.Decidir(Estado(mao: Pecas((1, 4)), mesa: Pecas((6, 1))), _estrategia);

        Assert.Equal(TipoAcao.Jogar, decisao.Tipo);
        Assert.NotNull(decisao.Jogada);
    }

    [Fact]
    public void Decisor_Jogar_SemEncaixe_CaiParaCompra() =>
        Assert.Equal(TipoAcao.Comprar, DecisorAcao.Decidir(Estado(mao: Pecas((0, 0)), mesa: Pecas((6, 1))), _estrategia).Tipo);

    [Fact]
    public void Decisor_SalaDeEsperaComDoisJogadores_Inicia() =>
        Assert.Equal(TipoAcao.Iniciar, DecisorAcao.Decidir(Estado(situacao: SituacaoPartida.AguardandoInicio, proxima: ProximaAcaoPartida.Aguardar), _estrategia).Tipo);

    [Fact]
    public void Decisor_SalaDeEsperaComUmJogador_Aguarda() =>
        Assert.Equal(TipoAcao.Aguardar, DecisorAcao.Decidir(Estado(situacao: SituacaoPartida.AguardandoInicio, proxima: ProximaAcaoPartida.Aguardar, participantes: 1), _estrategia).Tipo);

    [Fact]
    public void Decisor_SemIniciarAutomatico_Aguarda() =>
        Assert.Equal(TipoAcao.Aguardar, DecisorAcao.Decidir(Estado(situacao: SituacaoPartida.AguardandoInicio, proxima: ProximaAcaoPartida.Aguardar), _estrategia, iniciarAutomaticamente: false).Tipo);

    [Fact]
    public void Decisor_PartidaEncerrada_Encerra() =>
        Assert.Equal(TipoAcao.Encerrar, DecisorAcao.Decidir(Estado(situacao: SituacaoPartida.Encerrada, proxima: ProximaAcaoPartida.Aguardar), _estrategia).Tipo);
}
