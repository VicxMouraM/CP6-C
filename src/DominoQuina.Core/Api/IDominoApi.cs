using DominoQuina.Core.Modelos;

namespace DominoQuina.Core.Api;

/// <summary>
/// Operações da API do Dominó Ponta de Quina. A implementação resolve sozinha o token
/// (Bearer) e a chave de ativação (<c>X-Jogador-Ativacao</c>) de cada perfil.
/// Toda falha de negócio chega como <see cref="ApiException"/>.
/// </summary>
public interface IDominoApi
{
    /// <summary>Solicita o envio do token ao e-mail FIAP (resposta 202, sem corpo).</summary>
    Task SolicitarTokenAsync(string email, CancellationToken ct = default);

    /// <summary>Consulta a sessão do token atual.</summary>
    Task<SessaoResponse> ObterSessaoAsync(CancellationToken ct = default);

    /// <summary>Revoga o token atual.</summary>
    Task RevogarTokenAsync(CancellationToken ct = default);

    /// <summary>Lista os perfis da conta.</summary>
    Task<IReadOnlyList<PerfilJogador>> ListarJogadoresAsync(CancellationToken ct = default);

    /// <summary>Cria um perfil na conta.</summary>
    Task<PerfilJogador> CriarJogadorAsync(string nome, CancellationToken ct = default);

    /// <summary>Ativa o perfil neste cliente com a chave informada.</summary>
    Task<PerfilJogador> AtivarClienteAsync(Guid jogadorId, string chaveAtivacao, CancellationToken ct = default);

    /// <summary>Libera o perfil deste cliente.</summary>
    Task<PerfilJogador> DesativarClienteAsync(Guid jogadorId, CancellationToken ct = default);

    /// <summary>Lista partidas com uma vaga.</summary>
    Task<IReadOnlyList<PartidaDisponivel>> ListarPartidasAsync(CancellationToken ct = default);

    /// <summary>Cria uma partida e registra o perfil como primeiro participante.</summary>
    Task<EstadoPartidaParticipante> CriarPartidaAsync(Guid jogadorId, int pontuacaoAlvo, CancellationToken ct = default);

    /// <summary>Ingressa como segundo participante.</summary>
    Task<ConfirmacaoAcaoPartida> EntrarAsync(Guid partidaId, AcaoRequest acao, CancellationToken ct = default);

    /// <summary>Inicia a partida (distribui as pedras).</summary>
    Task<ConfirmacaoAcaoPartida> IniciarAsync(Guid partidaId, AcaoRequest acao, CancellationToken ct = default);

    /// <summary>Consulta a visão privada do participante.</summary>
    Task<EstadoPartidaParticipante> ObterEstadoAsync(Guid partidaId, Guid jogadorId, CancellationToken ct = default);

    /// <summary>
    /// Consulta atualizações. Retorna <c>null</c> quando nada mudou desde <paramref name="versaoConhecida"/> (204).
    /// </summary>
    Task<EstadoPartidaParticipante?> ObterAtualizacaoAsync(Guid partidaId, Guid jogadorId, long? versaoConhecida, CancellationToken ct = default);

    /// <summary>Visão pública da partida (sem mãos).</summary>
    Task<EstadoPartidaObservador> VisualizarAsync(Guid partidaId, CancellationToken ct = default);

    /// <summary>Joga uma pedra.</summary>
    Task<ConfirmacaoAcaoPartida> JogarAsync(Guid partidaId, JogarRequest jogada, CancellationToken ct = default);

    /// <summary>Compra uma pedra da cava.</summary>
    Task<ConfirmacaoAcaoPartida> ComprarAsync(Guid partidaId, AcaoRequest acao, CancellationToken ct = default);

    /// <summary>Passa a vez.</summary>
    Task<ConfirmacaoAcaoPartida> PassarAsync(Guid partidaId, AcaoRequest acao, CancellationToken ct = default);

    /// <summary>Resultado e histórico da partida.</summary>
    Task<ResultadoHistoricoPartida> ObterResultadoAsync(Guid partidaId, Guid jogadorId, CancellationToken ct = default);
}
