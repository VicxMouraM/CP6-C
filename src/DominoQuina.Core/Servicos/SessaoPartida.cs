using DominoQuina.Core.Api;
using DominoQuina.Core.Modelos;
using DominoQuina.Core.Regras;

namespace DominoQuina.Core.Servicos;

/// <summary>Resultado de uma ação enviada à API.</summary>
/// <param name="Sucesso">Se a API aceitou a ação.</param>
/// <param name="Mensagem">Texto para o jogador.</param>
/// <param name="Erro">Rejeição da API, quando houver.</param>
public sealed record ResultadoAcao(bool Sucesso, string Mensagem, ApiException? Erro = null)
{
    /// <summary>Se a rejeição foi de regra (422): a partida não avançou.</summary>
    public bool RegraRejeitada => Erro?.RegraRejeitada == true;
}

/// <summary>
/// Mantém a cópia local do estado de <b>uma participação</b> e envia as ações do jogador.
/// <list type="bullet">
/// <item>Só aplica versões mais novas do que a conhecida (nunca volta no tempo).</item>
/// <item>Cada intenção recebe um UUID novo de idempotência e a <c>versaoEsperada</c> do último estado.</item>
/// <item>Em 409, adota o <c>estadoAutoritativo</c>; em 422, mostra a rejeição sem fingir sucesso.</item>
/// </list>
/// </summary>
public sealed class SessaoPartida : IDisposable
{
    private readonly IDominoApi _api;
    private readonly SemaphoreSlim _exclusao = new(1, 1);

    /// <summary>Cria a sessão para uma participação existente.</summary>
    public SessaoPartida(IDominoApi api, Guid partidaId, Guid jogadorId, EstadoPartidaParticipante? estadoInicial = null, TimeProvider? relogio = null)
    {
        _api = api;
        PartidaId = partidaId;
        JogadorId = jogadorId;
        Cronometro = new CronometroTurno(relogio);
        if (estadoInicial is not null)
        {
            AplicarEstado(estadoInicial);
        }
    }

    /// <summary>Disparado sempre que o estado ou a mensagem mudam.</summary>
    public event Action? Alterado;

    /// <summary>Partida.</summary>
    public Guid PartidaId { get; }

    /// <summary>Perfil que joga nesta sessão.</summary>
    public Guid JogadorId { get; }

    /// <summary>Último estado confirmado pela API.</summary>
    public EstadoPartidaParticipante? Estado { get; private set; }

    /// <summary>Cronômetro do turno (para a regra futura de desconto por demora).</summary>
    public CronometroTurno Cronometro { get; }

    /// <summary>Última mensagem para o jogador (sucesso ou rejeição).</summary>
    public string? UltimaMensagem { get; private set; }

    /// <summary>Se a última mensagem é um erro.</summary>
    public bool UltimaMensagemEhErro { get; private set; }

    /// <summary>Cria uma partida nova e devolve a sessão do criador.</summary>
    public static async Task<SessaoPartida> CriarPartidaAsync(IDominoApi api, Guid jogadorId, int pontuacaoAlvo, CancellationToken ct = default)
    {
        if (pontuacaoAlvo <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pontuacaoAlvo), "A pontuação alvo deve ser positiva.");
        }

        var estado = await api.CriarPartidaAsync(jogadorId, pontuacaoAlvo, ct);
        return new SessaoPartida(api, estado.PartidaId, jogadorId, estado);
    }

    /// <summary>Ingressa em uma partida listada e devolve a sessão do segundo jogador.</summary>
    public static async Task<SessaoPartida> EntrarAsync(IDominoApi api, PartidaDisponivel partida, Guid jogadorId, CancellationToken ct = default)
    {
        var confirmacao = await api.EntrarAsync(partida.PartidaId, new AcaoRequest(jogadorId, partida.VersaoEstado, Guid.NewGuid()), ct);
        var sessao = new SessaoPartida(api, partida.PartidaId, jogadorId, confirmacao.Estado);
        if (sessao.Estado is null)
        {
            await sessao.CarregarAsync(ct);
        }

        return sessao;
    }

    /// <summary>
    /// Aplica um estado recebido, desde que não seja mais antigo do que o atual.
    /// </summary>
    /// <returns><c>true</c> se o estado local mudou.</returns>
    public bool AplicarEstado(EstadoPartidaParticipante? novo)
    {
        if (novo is null || (novo.PartidaId != Guid.Empty && novo.PartidaId != PartidaId))
        {
            return false;
        }

        if (Estado is not null && novo.VersaoEstado < Estado.VersaoEstado)
        {
            return false;
        }

        var mudou = Estado is null || novo.VersaoEstado != Estado.VersaoEstado;
        Estado = novo;
        Cronometro.Observar(novo.EhMinhaVez, novo.VersaoEstado);
        if (mudou)
        {
            Alterado?.Invoke();
        }

        return mudou;
    }

    /// <summary>Busca a visão atual completa (usado ao abrir ou reconectar).</summary>
    public async Task CarregarAsync(CancellationToken ct = default)
    {
        var estado = await _api.ObterEstadoAsync(PartidaId, JogadorId, ct);
        if (Estado is not null && estado.VersaoEstado < Estado.VersaoEstado)
        {
            return;
        }

        Estado = null;
        AplicarEstado(estado);
    }

    /// <summary>Consulta <c>/atualizacoes</c>. 204 significa que nada mudou.</summary>
    /// <returns><c>true</c> se chegou estado novo.</returns>
    public async Task<bool> AtualizarAsync(CancellationToken ct = default)
    {
        if (Estado is null)
        {
            await CarregarAsync(ct);
            return Estado is not null;
        }

        var novo = await _api.ObterAtualizacaoAsync(PartidaId, JogadorId, Estado.VersaoEstado, ct);
        return AplicarEstado(novo);
    }

    /// <summary>Inicia a partida (exige duas participações).</summary>
    public Task<ResultadoAcao> IniciarAsync(CancellationToken ct = default) =>
        ExecutarAsync("Partida iniciada.", (versao, chave) => _api.IniciarAsync(PartidaId, new AcaoRequest(JogadorId, versao, chave), ct), ct);

    /// <summary>Joga uma pedra na ponta escolhida.</summary>
    public Task<ResultadoAcao> JogarAsync(Jogada jogada, CancellationToken ct = default) =>
        ExecutarAsync($"Você jogou {jogada}.", (versao, chave) => _api.JogarAsync(PartidaId,
            new JogarRequest(JogadorId, versao, chave, jogada.Peca.ValorA, jogada.Peca.ValorB, jogada.Lado), ct), ct);

    /// <summary>Compra uma pedra da cava.</summary>
    public Task<ResultadoAcao> ComprarAsync(CancellationToken ct = default) =>
        ExecutarAsync("Você comprou uma pedra.", (versao, chave) => _api.ComprarAsync(PartidaId, new AcaoRequest(JogadorId, versao, chave), ct), ct);

    /// <summary>Passa a vez.</summary>
    public Task<ResultadoAcao> PassarAsync(CancellationToken ct = default) =>
        ExecutarAsync("Você passou a vez.", (versao, chave) => _api.PassarAsync(PartidaId, new AcaoRequest(JogadorId, versao, chave), ct), ct);

    /// <summary>Resultado e histórico da partida.</summary>
    public Task<ResultadoHistoricoPartida> ObterResultadoAsync(CancellationToken ct = default) =>
        _api.ObterResultadoAsync(PartidaId, JogadorId, ct);

    /// <summary>Jogadas válidas segundo a leitura local (a API é quem valida).</summary>
    public IReadOnlyList<Jogada> JogadasPossiveis() =>
        Estado is null ? [] : MotorRegras.JogadasPossiveis(Estado.MinhaMao, Estado.Tabuleiro, Estado.PedraAberturaObrigatoria);

    private async Task<ResultadoAcao> ExecutarAsync(
        string mensagemSucesso, Func<long, Guid, Task<ConfirmacaoAcaoPartida>> acao, CancellationToken ct)
    {
        await _exclusao.WaitAsync(ct);
        try
        {
            var versao = Estado?.VersaoEstado ?? 0;
            var chave = Guid.NewGuid(); // uma intenção = uma chave
            var confirmacao = await acao(versao, chave);
            if (confirmacao.Estado is not null)
            {
                AplicarEstado(confirmacao.Estado);
            }
            else
            {
                await CarregarAsync(ct);
            }

            return Informar(new ResultadoAcao(true, confirmacao.AcaoIdempotente ? mensagemSucesso + " (repetição reconhecida)" : mensagemSucesso));
        }
        catch (ApiException ex)
        {
            if (ex.Conflito)
            {
                if (ex.EstadoAutoritativo is not null)
                {
                    AplicarEstado(ex.EstadoAutoritativo);
                }
                else
                {
                    await TentarRecarregarAsync(ct);
                }
            }

            return Informar(new ResultadoAcao(false, ex.Message, ex));
        }
        catch (HttpRequestException ex)
        {
            await TentarRecarregarAsync(ct);
            return Informar(new ResultadoAcao(false, $"Sem resposta da API ({ex.Message}). O estado foi recarregado; confira se a ação foi aplicada."));
        }
        finally
        {
            _exclusao.Release();
        }
    }

    private async Task TentarRecarregarAsync(CancellationToken ct)
    {
        try
        {
            await CarregarAsync(ct);
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException)
        {
            // Mantém a cópia atual; a próxima consulta periódica tenta de novo.
        }
    }

    /// <inheritdoc />
    public void Dispose() => _exclusao.Dispose();

    private ResultadoAcao Informar(ResultadoAcao resultado)
    {
        UltimaMensagem = resultado.Mensagem;
        UltimaMensagemEhErro = !resultado.Sucesso;
        Alterado?.Invoke();
        return resultado;
    }
}
