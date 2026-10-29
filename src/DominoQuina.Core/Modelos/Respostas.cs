using System.Text.Json.Serialization;

namespace DominoQuina.Core.Modelos;

/// <summary>Sessão autenticada (contrato <c>SessaoResponse</c>).</summary>
public sealed class SessaoResponse : ModeloExtensivel
{
    /// <summary>Identificador do usuário dono do token.</summary>
    public Guid? UsuarioId { get; set; }

    /// <summary>E-mail da conta, quando informado pela API.</summary>
    public string? Email { get; set; }
}

/// <summary>Perfil de jogador da conta autenticada (contrato <c>PerfilJogador</c>).</summary>
public sealed class PerfilJogador : ModeloExtensivel
{
    /// <summary>Identificador atribuído pela API ao perfil.</summary>
    public Guid JogadorId { get; set; }

    /// <summary>Nome do perfil (1 a 150 caracteres).</summary>
    public string? Nome { get; set; }

    /// <summary>
    /// Indica se o perfil está reservado por algum cliente (estado de ativação informado pela API).
    /// </summary>
    [JsonIgnore]
    public bool? AtivoEmAlgumCliente => BuscarBooleano(
        ["ativo", "ativado", "clienteAtivo", "possuiAtivacao", "ativacaoAtiva", "emUso"],
        ["ativ"]);
}

/// <summary>Partida aguardando adversário (contrato <c>PartidaDisponivel</c>).</summary>
public sealed class PartidaDisponivel : ModeloExtensivel
{
    /// <summary>Identificador da partida.</summary>
    public Guid PartidaId { get; set; }

    /// <summary>Pontuação necessária para vencer.</summary>
    public int PontuacaoAlvo { get; set; }

    /// <summary>Versão atual do estado da partida.</summary>
    public long VersaoEstado { get; set; }
}

/// <summary>Resumo público de um participante (contrato <c>ResumoParticipantePartida</c>).</summary>
public sealed class ResumoParticipantePartida : ModeloExtensivel
{
    /// <summary>Perfil do participante.</summary>
    public Guid JogadorId { get; set; }

    /// <summary>Participação na partida, quando informada.</summary>
    public Guid? ParticipacaoId { get; set; }

    /// <summary>Nome do perfil, quando informado.</summary>
    public string? Nome { get; set; }

    /// <summary>Posição na mesa, quando informada.</summary>
    public int? Posicao { get; set; }

    /// <summary>Campo <c>pontuacao</c>, quando enviado com esse nome.</summary>
    public int? Pontuacao { get; set; }

    /// <summary>Campo <c>quantidadePedrasNaMao</c> (nome usado pela API oficial).</summary>
    public int? QuantidadePedrasNaMao { get; set; }

    /// <summary>Campo <c>ehVez</c>: indica se é a vez deste participante (nome usado pela API oficial).</summary>
    public bool? EhVez { get; set; }

    /// <summary>
    /// Pontuação acumulada na partida (placar). Usa <see cref="Pontuacao"/> ou procura nomes alternativos.
    /// </summary>
    /// <remarks>
    /// Propriedades calculadas não podem ter o mesmo nome de um campo JSON: um <c>[JsonIgnore]</c>
    /// com o mesmo nome faria o serializador descartar o valor em vez de mandá-lo a <see cref="ModeloExtensivel.CamposExtras"/>.
    /// </remarks>
    [JsonIgnore]
    public int? PontosNoPlacar => Pontuacao ?? BuscarInteiro(
        ["pontos", "placar", "pontuacaoTotal", "pontuacaoPartida", "pontuacaoAcumulada"],
        ["pont"], ["placar"]);

    /// <summary>Quantidade de pedras que o participante tem na mão (sem revelar quais).</summary>
    [JsonIgnore]
    public int? PedrasNaMao => QuantidadePedrasNaMao ?? BuscarInteiro(
        ["quantidadePedras", "quantidadePecas", "quantidadePecasNaMao",
         "pedrasNaMao", "pecasNaMao", "totalPedras", "totalPecas", "tamanhoMao", "quantidadeMao"],
        ["quant", "ped"], ["quant", "pec"], ["mao"], ["total", "ped"], ["total", "pec"]);

    /// <summary>Nome a exibir: nome do perfil ou o início do identificador.</summary>
    [JsonIgnore]
    public string NomeExibicao =>
        Nome ?? BuscarTexto(["nomeJogador", "apelido"], ["nome"]) ?? $"Jogador {JogadorId.ToString()[..8]}";
}

/// <summary>Visão privada da partida para um participante (contrato <c>EstadoPartidaParticipante</c>).</summary>
public sealed class EstadoPartidaParticipante : ModeloExtensivel
{
    /// <summary>Identificador da partida.</summary>
    public Guid PartidaId { get; set; }

    /// <summary>Versão do estado; cresce a cada ação aceita.</summary>
    public long VersaoEstado { get; set; }

    /// <summary>Situação da partida.</summary>
    public SituacaoPartida Situacao { get; set; }

    /// <summary>Número da rodada atual.</summary>
    public int NumeroRodada { get; set; }

    /// <summary>Jogador que tem a vez, se houver.</summary>
    public Guid? JogadorDoTurnoId { get; set; }

    /// <summary>Pedras do próprio participante. Nunca contém pedras do adversário.</summary>
    public List<Peca>? MinhaMao { get; set; }

    /// <summary>Pedras jogadas na mesa, da esquerda para a direita.</summary>
    public List<Peca>? Tabuleiro { get; set; }

    /// <summary>Resumo dos participantes.</summary>
    public List<ResumoParticipantePartida>? Participantes { get; set; }

    /// <summary>Vencedor da partida, quando encerrada.</summary>
    public Guid? VencedorId { get; set; }

    /// <summary>Próxima ação esperada deste participante.</summary>
    public ProximaAcaoPartida ProximaAcao { get; set; }

    /// <summary>Pedra que deve abrir a rodada, quando é a vez de abrir.</summary>
    public Peca? PedraAberturaObrigatoria { get; set; }

    /// <summary>Participação deste jogador na partida.</summary>
    public Guid ParticipacaoId { get; set; }

    /// <summary>Pontuação alvo, quando informada no estado.</summary>
    public int? PontuacaoAlvo { get; set; }

    /// <summary>Quantidade de pedras na cava (monte), quando informada.</summary>
    [JsonIgnore]
    public int? PedrasNaCava => BuscarInteiro(
        ["quantidadeCava", "pedrasNaCava", "cava", "quantidadePedrasCava", "tamanhoCava"], ["cava"], ["monte"]);

    /// <summary>Indica se o participante deve agir agora.</summary>
    [JsonIgnore]
    public bool EhMinhaVez => ProximaAcao is ProximaAcaoPartida.JogarAbertura or ProximaAcaoPartida.Jogar
        or ProximaAcaoPartida.Comprar or ProximaAcaoPartida.Passar;
}

/// <summary>Visão pública da partida para observadores (contrato <c>EstadoPartidaObservador</c>).</summary>
public sealed class EstadoPartidaObservador : ModeloExtensivel
{
    /// <summary>Identificador da partida.</summary>
    public Guid PartidaId { get; set; }

    /// <summary>Versão do estado.</summary>
    public long VersaoEstado { get; set; }

    /// <summary>Situação da partida.</summary>
    public SituacaoPartida Situacao { get; set; }

    /// <summary>Rodada atual.</summary>
    public int NumeroRodada { get; set; }

    /// <summary>Jogador da vez.</summary>
    public Guid? JogadorDoTurnoId { get; set; }

    /// <summary>Pedras da mesa.</summary>
    public List<Peca>? Tabuleiro { get; set; }

    /// <summary>Participantes.</summary>
    public List<ResumoParticipantePartida>? Participantes { get; set; }

    /// <summary>Vencedor, quando encerrada.</summary>
    public Guid? VencedorId { get; set; }
}

/// <summary>Resposta de uma ação aceita (contrato <c>ConfirmacaoAcaoPartida</c>).</summary>
public sealed class ConfirmacaoAcaoPartida : ModeloExtensivel
{
    /// <summary>Chave de idempotência da intenção.</summary>
    public Guid ChaveIdempotencia { get; set; }

    /// <summary>Verdadeiro quando a API reconheceu uma repetição de intenção já aceita.</summary>
    public bool AcaoIdempotente { get; set; }

    /// <summary>Estado confirmado após a ação.</summary>
    public EstadoPartidaParticipante? Estado { get; set; }
}

/// <summary>Evento do histórico da partida (contrato <c>EventoHistoricoPartida</c>).</summary>
public sealed class EventoHistoricoPartida : ModeloExtensivel
{
    /// <summary>Identificador do evento.</summary>
    public Guid EventoId { get; set; }

    /// <summary>Jogador autor do evento.</summary>
    public Guid JogadorId { get; set; }

    /// <summary>Pedra jogada, se houver.</summary>
    public Peca? PecaJogada { get; set; }

    /// <summary>Ponta em que a pedra foi jogada.</summary>
    public LadoJogavel? Lado { get; set; }

    /// <summary>Indica se o jogador passou a vez.</summary>
    public bool PassouVez { get; set; }

    /// <summary>Pontos concedidos pelo evento.</summary>
    public int PontosConcedidos { get; set; }

    /// <summary>Momento em que a API recebeu a ação.</summary>
    public DateTimeOffset RecebidoEmUtc { get; set; }
}

/// <summary>Resultado e histórico da partida (contrato <c>ResultadoHistoricoPartida</c>).</summary>
public sealed class ResultadoHistoricoPartida : ModeloExtensivel
{
    /// <summary>Identificador da partida.</summary>
    public Guid PartidaId { get; set; }

    /// <summary>Situação final ou atual.</summary>
    public SituacaoPartida Situacao { get; set; }

    /// <summary>Vencedor, quando encerrada.</summary>
    public Guid? VencedorId { get; set; }

    /// <summary>Participantes e placar.</summary>
    public List<ResumoParticipantePartida>? Participantes { get; set; }

    /// <summary>Eventos permitidos ao solicitante.</summary>
    public List<EventoHistoricoPartida>? Eventos { get; set; }
}

/// <summary>Envelope de erro da API (contrato <c>RespostaErro</c>).</summary>
public sealed class RespostaErro : ModeloExtensivel
{
    /// <summary>Código de negócio, por exemplo <c>ENCAIXE_INVALIDO</c> ou <c>JOGADOR_JA_ATIVO</c>.</summary>
    public string? Codigo { get; set; }

    /// <summary>Mensagem legível enviada pela API.</summary>
    public string? Mensagem { get; set; }

    /// <summary>Estado oficial enviado junto a um 409 (CONFLITO_DE_ESTADO).</summary>
    public EstadoPartidaParticipante? EstadoAutoritativo { get; set; }

    /// <summary>Código efetivo, procurando também em nomes alternativos.</summary>
    [JsonIgnore]
    public string? CodigoEfetivo => Codigo ?? BuscarTexto(["erro", "code", "error", "tipo"], ["cod"]);

    /// <summary>Mensagem efetiva, procurando também em nomes alternativos.</summary>
    [JsonIgnore]
    public string? MensagemEfetiva => Mensagem ?? BuscarTexto(["message", "detalhe", "detail", "title", "descricao"], ["mensag"]);
}
