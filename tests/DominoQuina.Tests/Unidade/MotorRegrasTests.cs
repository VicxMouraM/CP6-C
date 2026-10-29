using DominoQuina.Core.Modelos;
using DominoQuina.Core.Regras;
using static DominoQuina.Tests.Apoio.Fabrica;

namespace DominoQuina.Tests.Unidade;

public class MotorRegrasTests
{
    [Theory]
    [InlineData(5, 1)]
    [InlineData(10, 2)]
    [InlineData(15, 3)]
    [InlineData(20, 4)]
    [InlineData(0, 0)]
    [InlineData(7, 0)]
    [InlineData(12, 0)]
    public void PontosPorSoma_SegueATabelaDoEnunciado(int soma, int esperado) =>
        Assert.Equal(esperado, MotorRegras.PontosPorSoma(soma));

    [Fact]
    public void CalcularPontas_MesaVazia_RetornaNulo() =>
        Assert.Null(MotorRegras.CalcularPontas([]));

    [Fact]
    public void CalcularPontas_UmaPedra_UsaOsDoisLados()
    {
        var pontas = MotorRegras.CalcularPontas([P(6, 1)]);
        Assert.Equal(new Pontas(6, 1), pontas);
        Assert.Equal(7, pontas!.Soma);
    }

    [Fact]
    public void OrientarTabuleiro_PedrasCanonicas_EncadeiaCorretamente()
    {
        // Mesa real: [3|6][6|6][6|1] mas enviada sem orientação: [3|6] [6|6] [1|6]
        var orientadas = MotorRegras.OrientarTabuleiro(Pecas((6, 3), (6, 6), (1, 6)));

        Assert.Equal(Pecas((3, 6), (6, 6), (6, 1)), orientadas);
        Assert.Equal(new Pontas(3, 1), MotorRegras.CalcularPontas(Pecas((6, 3), (6, 6), (1, 6))));
    }

    [Fact]
    public void OrientarTabuleiro_PedrasJaOrientadas_NaoAltera()
    {
        var mesa = Pecas((2, 5), (5, 5), (5, 0), (0, 4));
        Assert.Equal(mesa, MotorRegras.OrientarTabuleiro(mesa));
    }

    [Fact]
    public void JogadasPossiveis_MesaVaziaComAberturaObrigatoria_SoPermiteAAbertura()
    {
        var jogadas = MotorRegras.JogadasPossiveis(Pecas((1, 2), (6, 6), (3, 4)), [], P(6, 6));

        var jogada = Assert.Single(jogadas);
        Assert.Equal(P(6, 6), jogada.Peca);
    }

    [Fact]
    public void JogadasPossiveis_PedraQueEncaixaNasDuasPontas_GeraDuasJogadas()
    {
        var mesa = Pecas((2, 6), (6, 4)); // pontas 2 e 4
        var jogadas = MotorRegras.JogadasPossiveis(Pecas((2, 4), (5, 5)), mesa);

        Assert.Equal(2, jogadas.Count);
        Assert.Contains(new Jogada(P(2, 4), LadoJogavel.Esquerda), jogadas);
        Assert.Contains(new Jogada(P(2, 4), LadoJogavel.Direita), jogadas);
    }

    [Fact]
    public void JogadasPossiveis_PontasIguais_NaoDuplicaJogada()
    {
        var jogadas = MotorRegras.JogadasPossiveis(Pecas((3, 1)), Pecas((3, 3)));
        Assert.Single(jogadas);
    }

    [Fact]
    public void JogadasPossiveis_SemEncaixe_RetornaVazio() =>
        Assert.Empty(MotorRegras.JogadasPossiveis(Pecas((0, 0), (1, 1)), Pecas((5, 6))));

    [Fact]
    public void AplicarJogada_AtualizaApenasAPontaEscolhida()
    {
        var resultado = MotorRegras.AplicarJogada(new Pontas(6, 1), new Jogada(P(1, 4), LadoJogavel.Direita));
        Assert.Equal(new Pontas(6, 4), resultado);
    }

    [Fact]
    public void AplicarJogada_PedraQueNaoEncaixa_LancaExcecao() =>
        Assert.Throws<InvalidOperationException>(() =>
            MotorRegras.AplicarJogada(new Pontas(6, 1), new Jogada(P(2, 3), LadoJogavel.Esquerda)));

    [Fact]
    public void PontosDaJogada_SomaMultiplaDeCinco_PontuaPeloAutor()
    {
        // pontas 6 e 1; jogar [1|4] à direita → 6 + 4 = 10 → 2 pontos
        Assert.Equal(2, MotorRegras.PontosDaJogada(Pecas((6, 1)), new Jogada(P(1, 4), LadoJogavel.Direita)));
    }

    [Fact]
    public void Peca_MesmaPedra_IgnoraOrientacao()
    {
        Assert.True(P(6, 1).MesmaPedra(P(1, 6)));
        Assert.False(P(6, 1).MesmaPedra(P(6, 2)));
        Assert.False(P(6, 1).MesmaPedra(null));
        Assert.Equal(6, P(6, 1).Oposto(1));
        Assert.True(P(4, 4).EhDupla);
    }
}
