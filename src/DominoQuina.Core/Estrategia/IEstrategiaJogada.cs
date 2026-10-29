using DominoQuina.Core.Modelos;
using DominoQuina.Core.Regras;

namespace DominoQuina.Core.Estrategia;

/// <summary>Escolhe uma jogada entre as possíveis. Usada pelo jogador autônomo e pela dica da interface.</summary>
public interface IEstrategiaJogada
{
    /// <summary>Nome exibido da estratégia.</summary>
    string Nome { get; }

    /// <summary>Escolhe a jogada, ou <c>null</c> se nenhuma for possível.</summary>
    Jogada? Escolher(EstadoPartidaParticipante estado);
}
