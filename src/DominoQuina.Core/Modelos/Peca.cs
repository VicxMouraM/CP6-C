using System.Text.Json.Serialization;

namespace DominoQuina.Core.Modelos;

/// <summary>
/// Pedra de dominó (contrato <c>PecaPartida</c>). Os valores vão de 0 a 6.
/// </summary>
/// <param name="ValorA">Primeira ponta da pedra.</param>
/// <param name="ValorB">Segunda ponta da pedra.</param>
public sealed record Peca(
    [property: JsonPropertyName("valorA")] int ValorA,
    [property: JsonPropertyName("valorB")] int ValorB)
{
    /// <summary>Indica se é uma dupla (carroça), por exemplo [6|6].</summary>
    [JsonIgnore]
    public bool EhDupla => ValorA == ValorB;

    /// <summary>Soma dos dois lados da pedra.</summary>
    [JsonIgnore]
    public int Soma => ValorA + ValorB;

    /// <summary>Verifica se algum dos lados possui o valor informado.</summary>
    public bool Contem(int valor) => ValorA == valor || ValorB == valor;

    /// <summary>Retorna o lado oposto ao valor informado (que deve existir na pedra).</summary>
    public int Oposto(int valor) => ValorA == valor ? ValorB : ValorA;

    /// <summary>Compara a pedra ignorando a orientação: [6|1] é a mesma pedra que [1|6].</summary>
    public bool MesmaPedra(Peca? outra) =>
        outra is not null &&
        ((ValorA == outra.ValorA && ValorB == outra.ValorB) ||
         (ValorA == outra.ValorB && ValorB == outra.ValorA));

    /// <summary>Retorna a pedra com os lados trocados.</summary>
    public Peca Invertida() => new(ValorB, ValorA);

    /// <inheritdoc />
    public override string ToString() => $"[{ValorA}|{ValorB}]";
}
