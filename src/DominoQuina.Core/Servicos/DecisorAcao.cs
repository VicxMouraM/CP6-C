using DominoQuina.Core.Estrategia;
using DominoQuina.Core.Modelos;
using DominoQuina.Core.Regras;

namespace DominoQuina.Core.Servicos;

/// <summary>Tipos de ação que o jogador autônomo pode tomar.</summary>
public enum TipoAcao
{
    /// <summary>Nada a fazer agora.</summary>
    Aguardar,

    /// <summary>Iniciar a partida.</summary>
    Iniciar,

    /// <summary>Jogar uma pedra.</summary>
    Jogar,

    /// <summary>Comprar da cava.</summary>
    Comprar,

    /// <summary>Passar a vez.</summary>
    Passar,

    /// <summary>A partida acabou.</summary>
    Encerrar,
}

/// <summary>Ação decidida a partir de um estado.</summary>
/// <param name="Tipo">O que fazer.</param>
/// <param name="Jogada">Pedra e ponta, quando <see cref="TipoAcao.Jogar"/>.</param>
public sealed record AcaoDecidida(TipoAcao Tipo, Jogada? Jogada = null);

/// <summary>
/// Decide a próxima ação olhando apenas o estado confirmado pela API (função pura, fácil de testar).
/// </summary>
public static class DecisorAcao
{
    /// <summary>Decide o que fazer com o estado atual.</summary>
    /// <param name="estado">Último estado confirmado.</param>
    /// <param name="estrategia">Estratégia para escolher a pedra.</param>
    /// <param name="iniciarAutomaticamente">Se deve iniciar a partida quando houver dois participantes.</param>
    public static AcaoDecidida Decidir(EstadoPartidaParticipante estado, IEstrategiaJogada estrategia, bool iniciarAutomaticamente = true)
    {
        if (estado.Situacao == SituacaoPartida.Encerrada || estado.ProximaAcao == ProximaAcaoPartida.Encerrada)
        {
            return new AcaoDecidida(TipoAcao.Encerrar);
        }

        if (estado.Situacao == SituacaoPartida.AguardandoInicio)
        {
            var participantes = estado.Participantes?.Count ?? 0;
            return iniciarAutomaticamente && participantes >= 2
                ? new AcaoDecidida(TipoAcao.Iniciar)
                : new AcaoDecidida(TipoAcao.Aguardar);
        }

        switch (estado.ProximaAcao)
        {
            case ProximaAcaoPartida.JogarAbertura:
            case ProximaAcaoPartida.Jogar:
                var jogada = estrategia.Escolher(estado);
                return jogada is null ? new AcaoDecidida(TipoAcao.Comprar) : new AcaoDecidida(TipoAcao.Jogar, jogada);

            case ProximaAcaoPartida.Comprar:
                return new AcaoDecidida(TipoAcao.Comprar);

            case ProximaAcaoPartida.Passar:
                return new AcaoDecidida(TipoAcao.Passar);

            default:
                return new AcaoDecidida(TipoAcao.Aguardar);
        }
    }
}
