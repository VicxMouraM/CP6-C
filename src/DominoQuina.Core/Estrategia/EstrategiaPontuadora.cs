using DominoQuina.Core.Modelos;
using DominoQuina.Core.Regras;

namespace DominoQuina.Core.Estrategia;

/// <summary>
/// Estratégia gulosa com critérios de desempate:
/// <list type="number">
/// <item>maximiza os pontos imediatos (soma das pontas múltipla de 5);</item>
/// <item>evita deixar pontas que deem pontos fáceis ao adversário;</item>
/// <item>prefere descartar pedras pesadas e duplas (pesam no travamento e são difíceis de encaixar);</item>
/// <item>mantém na mão pedras que continuem encaixando nas novas pontas.</item>
/// </list>
/// </summary>
public sealed class EstrategiaPontuadora : IEstrategiaJogada
{
    /// <inheritdoc />
    public string Nome => "Pontuadora";

    /// <inheritdoc />
    public Jogada? Escolher(EstadoPartidaParticipante estado)
    {
        var mao = estado.MinhaMao ?? [];
        var tabuleiro = estado.Tabuleiro ?? [];

        if (estado.ProximaAcao == ProximaAcaoPartida.JogarAbertura && estado.PedraAberturaObrigatoria is { } abertura)
        {
            var naMao = mao.FirstOrDefault(p => p.MesmaPedra(abertura)) ?? abertura;
            return new Jogada(naMao, LadoJogavel.Esquerda);
        }

        var jogadas = MotorRegras.JogadasPossiveis(mao, tabuleiro, estado.PedraAberturaObrigatoria);
        return jogadas.Count == 0
            ? null
            : jogadas.OrderByDescending(j => Avaliar(j, mao, tabuleiro)).ThenBy(j => j.Lado).First();
    }

    /// <summary>Nota da jogada; quanto maior, melhor.</summary>
    internal static double Avaliar(Jogada jogada, IReadOnlyList<Peca> mao, IReadOnlyList<Peca> tabuleiro)
    {
        var pontasAntes = MotorRegras.CalcularPontas(tabuleiro);
        var pontasDepois = MotorRegras.AplicarJogada(pontasAntes, jogada);
        var pontos = MotorRegras.PontosPorSoma(pontasDepois.Soma);

        var restante = mao.Where(p => !p.MesmaPedra(jogada.Peca)).ToList();
        var flexibilidade = restante.Count(p => p.Contem(pontasDepois.Esquerda) || p.Contem(pontasDepois.Direita));

        var nota = pontos * 100.0;
        nota -= RiscoParaAdversario(pontasDepois, restante, tabuleiro) * 30.0;
        nota += jogada.Peca.Soma * 2.0;
        nota += jogada.Peca.EhDupla ? 6.0 : 0.0;
        nota += flexibilidade * 3.0;
        return nota;
    }

    /// <summary>
    /// Maior pontuação que o adversário poderia fazer na próxima jogada, considerando apenas
    /// pedras que não estão na nossa mão nem na mesa (estimativa conservadora).
    /// </summary>
    internal static int RiscoParaAdversario(Pontas pontas, IReadOnlyList<Peca> minhaMao, IReadOnlyList<Peca> tabuleiro)
    {
        var conhecidas = minhaMao.Concat(tabuleiro).ToList();
        var maior = 0;
        for (var a = 0; a <= 6; a++)
        {
            for (var b = a; b <= 6; b++)
            {
                var peca = new Peca(a, b);
                if (conhecidas.Any(p => p.MesmaPedra(peca)))
                {
                    continue;
                }

                foreach (var lado in new[] { LadoJogavel.Esquerda, LadoJogavel.Direita })
                {
                    if (MotorRegras.Encaixa(peca, pontas, lado))
                    {
                        var soma = MotorRegras.AplicarJogada(pontas, new Jogada(peca, lado)).Soma;
                        maior = Math.Max(maior, MotorRegras.PontosPorSoma(soma));
                    }
                }
            }
        }

        return maior;
    }
}
