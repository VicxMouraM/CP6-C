namespace DominoQuina.Core.Modelos;

/// <summary>Situação geral da partida (contrato <c>SituacaoPartida</c>).</summary>
public enum SituacaoPartida
{
    /// <summary>Partida criada, aguardando o segundo jogador ou o início.</summary>
    AguardandoInicio = 0,

    /// <summary>Partida em andamento.</summary>
    EmAndamento = 1,

    /// <summary>Partida encerrada; há vencedor.</summary>
    Encerrada = 2,
}

/// <summary>Próxima ação esperada do participante (contrato <c>ProximaAcaoPartida</c>).</summary>
public enum ProximaAcaoPartida
{
    /// <summary>Não é a vez do participante: aguardar.</summary>
    Aguardar = 0,

    /// <summary>Jogar a pedra de abertura obrigatória.</summary>
    JogarAbertura = 1,

    /// <summary>Jogar uma pedra da mão.</summary>
    Jogar = 2,

    /// <summary>Comprar uma pedra da cava.</summary>
    Comprar = 3,

    /// <summary>Passar a vez.</summary>
    Passar = 4,

    /// <summary>Partida encerrada.</summary>
    Encerrada = 5,
}

/// <summary>Ponta do tabuleiro onde a pedra será encaixada (contrato <c>LadoJogavel</c>).</summary>
public enum LadoJogavel
{
    /// <summary>Ponta esquerda (lado = 0).</summary>
    Esquerda = 0,

    /// <summary>Ponta direita (lado = 1).</summary>
    Direita = 1,
}
