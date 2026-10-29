using DominoQuina.Core.Modelos;

namespace DominoQuina.Core.Regras;

/// <summary>Valores das duas pontas abertas da mesa.</summary>
/// <param name="Esquerda">Valor exposto na ponta esquerda.</param>
/// <param name="Direita">Valor exposto na ponta direita.</param>
public sealed record Pontas(int Esquerda, int Direita)
{
    /// <summary>Soma das pontas externas (base da pontuação).</summary>
    public int Soma => Esquerda + Direita;
}

/// <summary>Uma jogada possível: qual pedra e em qual ponta.</summary>
/// <param name="Peca">Pedra da mão.</param>
/// <param name="Lado">Ponta da mesa.</param>
public sealed record Jogada(Peca Peca, LadoJogavel Lado)
{
    /// <inheritdoc />
    public override string ToString() => $"{Peca} na {(Lado == LadoJogavel.Esquerda ? "esquerda" : "direita")}";
}

/// <summary>
/// Leitura local das regras para <b>exibir</b> e <b>sugerir</b> jogadas.
/// A API continua sendo a autoridade: ela valida a jogada e informa a pontuação oficial.
/// </summary>
public static class MotorRegras
{
    /// <summary>Divisor da pontuação: múltiplos de 5 ("quina").</summary>
    public const int Quina = 5;

    /// <summary>
    /// Orienta as pedras da mesa da esquerda para a direita, de modo que o lado direito
    /// de cada pedra encoste no lado esquerdo da seguinte. Funciona tanto se a API enviar as
    /// pedras já orientadas quanto se enviar na forma canônica.
    /// </summary>
    public static IReadOnlyList<Peca> OrientarTabuleiro(IReadOnlyList<Peca>? tabuleiro)
    {
        if (tabuleiro is null || tabuleiro.Count == 0)
        {
            return [];
        }

        if (tabuleiro.Count == 1)
        {
            return [tabuleiro[0]];
        }

        var orientadas = new List<Peca>(tabuleiro.Count);
        var primeira = tabuleiro[0];
        var segunda = tabuleiro[1];

        // O lado direito da primeira é o valor que ela compartilha com a segunda.
        var direitaPrimeira = segunda.Contem(primeira.ValorB) ? primeira.ValorB : primeira.ValorA;
        orientadas.Add(new Peca(primeira.Oposto(direitaPrimeira), direitaPrimeira));

        for (var i = 1; i < tabuleiro.Count; i++)
        {
            var anterior = orientadas[i - 1].ValorB;
            var atual = tabuleiro[i];
            orientadas.Add(atual.Contem(anterior) ? new Peca(anterior, atual.Oposto(anterior)) : atual);
        }

        return orientadas;
    }

    /// <summary>Pontas abertas da mesa, ou <c>null</c> se a mesa está vazia.</summary>
    public static Pontas? CalcularPontas(IReadOnlyList<Peca>? tabuleiro)
    {
        var orientadas = OrientarTabuleiro(tabuleiro);
        return orientadas.Count == 0 ? null : new Pontas(orientadas[0].ValorA, orientadas[^1].ValorB);
    }

    /// <summary>Indica se a pedra pode ser jogada na ponta indicada.</summary>
    public static bool Encaixa(Peca peca, Pontas? pontas, LadoJogavel lado) =>
        pontas is null || peca.Contem(lado == LadoJogavel.Esquerda ? pontas.Esquerda : pontas.Direita);

    /// <summary>
    /// Lista as jogadas válidas da mão. Com mesa vazia e pedra de abertura obrigatória,
    /// apenas essa pedra é aceita. Pedras que encaixam nas duas pontas geram duas jogadas
    /// (exceto quando as pontas são iguais, que geram uma só).
    /// </summary>
    public static IReadOnlyList<Jogada> JogadasPossiveis(IReadOnlyList<Peca>? mao, IReadOnlyList<Peca>? tabuleiro, Peca? aberturaObrigatoria = null)
    {
        if (mao is null || mao.Count == 0)
        {
            return [];
        }

        var pontas = CalcularPontas(tabuleiro);
        if (pontas is null)
        {
            var candidatas = aberturaObrigatoria is null ? mao : mao.Where(p => p.MesmaPedra(aberturaObrigatoria));
            return candidatas.Select(p => new Jogada(p, LadoJogavel.Esquerda)).ToList();
        }

        var jogadas = new List<Jogada>();
        foreach (var peca in mao)
        {
            if (peca.Contem(pontas.Esquerda))
            {
                jogadas.Add(new Jogada(peca, LadoJogavel.Esquerda));
            }

            if (peca.Contem(pontas.Direita) && !(pontas.Esquerda == pontas.Direita && peca.Contem(pontas.Esquerda)))
            {
                jogadas.Add(new Jogada(peca, LadoJogavel.Direita));
            }
        }

        return jogadas;
    }

    /// <summary>Pontas resultantes após aplicar a jogada.</summary>
    /// <exception cref="InvalidOperationException">Se a pedra não encaixa na ponta.</exception>
    public static Pontas AplicarJogada(Pontas? pontas, Jogada jogada)
    {
        if (pontas is null)
        {
            return new Pontas(jogada.Peca.ValorA, jogada.Peca.ValorB);
        }

        if (!Encaixa(jogada.Peca, pontas, jogada.Lado))
        {
            throw new InvalidOperationException($"{jogada.Peca} não encaixa na ponta escolhida.");
        }

        return jogada.Lado == LadoJogavel.Esquerda
            ? pontas with { Esquerda = jogada.Peca.Oposto(pontas.Esquerda) }
            : pontas with { Direita = jogada.Peca.Oposto(pontas.Direita) };
    }

    /// <summary>
    /// Pontos ganhos com a soma das pontas: múltiplo de 5 vale soma ÷ 5 (5→1, 10→2, 15→3...).
    /// </summary>
    public static int PontosPorSoma(int soma) => soma > 0 && soma % Quina == 0 ? soma / Quina : 0;

    /// <summary>Pontos que a jogada renderia, segundo a regra local (a API dá o valor oficial).</summary>
    public static int PontosDaJogada(IReadOnlyList<Peca>? tabuleiro, Jogada jogada) =>
        PontosPorSoma(AplicarJogada(CalcularPontas(tabuleiro), jogada).Soma);
}
