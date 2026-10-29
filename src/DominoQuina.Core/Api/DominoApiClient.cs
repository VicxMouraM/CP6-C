using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DominoQuina.Core.Armazenamento;
using DominoQuina.Core.Modelos;

namespace DominoQuina.Core.Api;

/// <summary>
/// Cliente HTTP da API. Monta cada requisição com:
/// <list type="bullet">
/// <item><c>Authorization: Bearer</c> com o token guardado;</item>
/// <item><c>X-Jogador-Ativacao</c> com a chave do perfil, nas rotas privadas do jogador;</item>
/// <item>repetição automática apenas para requisições idempotentes (mesma chave de idempotência), em falhas de rede.</item>
/// </list>
/// </summary>
public sealed class DominoApiClient : IDominoApi
{
    /// <summary>Nome do cabeçalho com a chave de ativação do perfil.</summary>
    public const string CabecalhoAtivacao = "X-Jogador-Ativacao";

    private static readonly TimeSpan[] s_esperasRepeticao = [TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(900)];

    private readonly HttpClient _http;
    private readonly ICredenciaisCliente _credenciais;

    /// <summary>Cria o cliente.</summary>
    /// <param name="http">HttpClient (pode vir do IHttpClientFactory).</param>
    /// <param name="credenciais">Fonte do endereço, token e chaves de ativação.</param>
    public DominoApiClient(HttpClient http, ICredenciaisCliente credenciais)
    {
        _http = http;
        _credenciais = credenciais;
    }

    /// <inheritdoc />
    public async Task SolicitarTokenAsync(string email, CancellationToken ct = default)
    {
        using var resposta = await EnviarAsync(HttpMethod.Post, "api/identidade/token", new SolicitarTokenRequest(email),
            autenticar: false, jogadorId: null, idempotente: false, ct);
        if (resposta.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new ApiException(HttpStatusCode.BadRequest,
                new RespostaErro { Codigo = "EMAIL_INVALIDO", Mensagem = "Use um e-mail válido terminado em @fiap.com.br." }, null);
        }

        await GarantirSucessoAsync(resposta, ct);
    }

    /// <inheritdoc />
    public Task<SessaoResponse> ObterSessaoAsync(CancellationToken ct = default) =>
        LerAsync<SessaoResponse>(HttpMethod.Get, "api/identidade/sessao", null, null, idempotente: true, ct);

    /// <inheritdoc />
    public async Task RevogarTokenAsync(CancellationToken ct = default)
    {
        using var resposta = await EnviarAsync(HttpMethod.Post, "api/identidade/tokens/revogacao", null, true, null, false, ct);
        await GarantirSucessoAsync(resposta, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PerfilJogador>> ListarJogadoresAsync(CancellationToken ct = default) =>
        await LerAsync<List<PerfilJogador>>(HttpMethod.Get, "api/jogadores", null, null, true, ct);

    /// <inheritdoc />
    public Task<PerfilJogador> CriarJogadorAsync(string nome, CancellationToken ct = default) =>
        LerAsync<PerfilJogador>(HttpMethod.Post, "api/jogadores", new CriarJogadorRequest(nome), null, false, ct);

    /// <inheritdoc />
    public Task<PerfilJogador> AtivarClienteAsync(Guid jogadorId, string chaveAtivacao, CancellationToken ct = default) =>
        // A mesma chave pode ser reenviada com segurança: a API reconhece a ativação existente.
        LerAsync<PerfilJogador>(HttpMethod.Post, $"api/jogadores/{jogadorId}/ativar-cliente",
            new AtivarJogadorRequest(chaveAtivacao), null, true, ct);

    /// <inheritdoc />
    public Task<PerfilJogador> DesativarClienteAsync(Guid jogadorId, CancellationToken ct = default) =>
        LerAsync<PerfilJogador>(HttpMethod.Delete, $"api/jogadores/{jogadorId}/desativar-cliente", null, jogadorId, true, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PartidaDisponivel>> ListarPartidasAsync(CancellationToken ct = default) =>
        await LerAsync<List<PartidaDisponivel>>(HttpMethod.Get, "api/partidas", null, null, true, ct);

    /// <inheritdoc />
    public Task<EstadoPartidaParticipante> CriarPartidaAsync(Guid jogadorId, int pontuacaoAlvo, CancellationToken ct = default) =>
        // Sem chave de idempotência no contrato: não repetimos automaticamente para não criar duas mesas.
        LerAsync<EstadoPartidaParticipante>(HttpMethod.Post, "api/partidas",
            new CriarPartidaRequest(pontuacaoAlvo, jogadorId), jogadorId, false, ct);

    /// <inheritdoc />
    public Task<ConfirmacaoAcaoPartida> EntrarAsync(Guid partidaId, AcaoRequest acao, CancellationToken ct = default) =>
        LerAsync<ConfirmacaoAcaoPartida>(HttpMethod.Post, $"api/partidas/{partidaId}/entrar", acao, acao.JogadorId, true, ct);

    /// <inheritdoc />
    public Task<ConfirmacaoAcaoPartida> IniciarAsync(Guid partidaId, AcaoRequest acao, CancellationToken ct = default) =>
        LerAsync<ConfirmacaoAcaoPartida>(HttpMethod.Post, $"api/partidas/{partidaId}/iniciar", acao, acao.JogadorId, true, ct);

    /// <inheritdoc />
    public Task<EstadoPartidaParticipante> ObterEstadoAsync(Guid partidaId, Guid jogadorId, CancellationToken ct = default) =>
        LerAsync<EstadoPartidaParticipante>(HttpMethod.Get, $"api/partidas/{partidaId}?jogadorId={jogadorId}", null, jogadorId, true, ct);

    /// <inheritdoc />
    public async Task<EstadoPartidaParticipante?> ObterAtualizacaoAsync(Guid partidaId, Guid jogadorId, long? versaoConhecida, CancellationToken ct = default)
    {
        var rota = $"api/partidas/{partidaId}/atualizacoes?jogadorId={jogadorId}";
        if (versaoConhecida is { } versao)
        {
            rota += $"&versaoConhecida={versao}";
        }

        using var resposta = await EnviarAsync(HttpMethod.Get, rota, null, true, jogadorId, true, ct);
        if (resposta.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        await GarantirSucessoAsync(resposta, ct);
        return await DesserializarAsync<EstadoPartidaParticipante>(resposta, ct);
    }

    /// <inheritdoc />
    public Task<EstadoPartidaObservador> VisualizarAsync(Guid partidaId, CancellationToken ct = default) =>
        LerAsync<EstadoPartidaObservador>(HttpMethod.Get, $"api/partidas/{partidaId}/visualizar", null, null, true, ct);

    /// <inheritdoc />
    public Task<ConfirmacaoAcaoPartida> JogarAsync(Guid partidaId, JogarRequest jogada, CancellationToken ct = default) =>
        LerAsync<ConfirmacaoAcaoPartida>(HttpMethod.Post, $"api/partidas/{partidaId}/jogar", jogada, jogada.JogadorId, true, ct);

    /// <inheritdoc />
    public Task<ConfirmacaoAcaoPartida> ComprarAsync(Guid partidaId, AcaoRequest acao, CancellationToken ct = default) =>
        LerAsync<ConfirmacaoAcaoPartida>(HttpMethod.Post, $"api/partidas/{partidaId}/comprar", acao, acao.JogadorId, true, ct);

    /// <inheritdoc />
    public Task<ConfirmacaoAcaoPartida> PassarAsync(Guid partidaId, AcaoRequest acao, CancellationToken ct = default) =>
        LerAsync<ConfirmacaoAcaoPartida>(HttpMethod.Post, $"api/partidas/{partidaId}/passar", acao, acao.JogadorId, true, ct);

    /// <inheritdoc />
    public Task<ResultadoHistoricoPartida> ObterResultadoAsync(Guid partidaId, Guid jogadorId, CancellationToken ct = default) =>
        LerAsync<ResultadoHistoricoPartida>(HttpMethod.Get, $"api/partidas/{partidaId}/resultado?jogadorId={jogadorId}", null, jogadorId, true, ct);

    private async Task<T> LerAsync<T>(HttpMethod metodo, string rota, object? corpo, Guid? jogadorId, bool idempotente, CancellationToken ct)
    {
        using var resposta = await EnviarAsync(metodo, rota, corpo, autenticar: true, jogadorId, idempotente, ct);
        await GarantirSucessoAsync(resposta, ct);
        return await DesserializarAsync<T>(resposta, ct);
    }

    private async Task<HttpResponseMessage> EnviarAsync(
        HttpMethod metodo, string rota, object? corpo, bool autenticar, Guid? jogadorId, bool idempotente, CancellationToken ct)
    {
        var tentativas = idempotente ? s_esperasRepeticao.Length + 1 : 1;
        for (var tentativa = 1; ; tentativa++)
        {
            using var requisicao = MontarRequisicao(metodo, rota, corpo, autenticar, jogadorId);
            try
            {
                return await _http.SendAsync(requisicao, ct);
            }
            catch (Exception ex) when (tentativa < tentativas && EhFalhaTransitoria(ex, ct))
            {
                // A resposta pode ter se perdido: repetir com o mesmo corpo (mesma chave de idempotência)
                // não duplica a ação na API.
                await Task.Delay(s_esperasRepeticao[tentativa - 1], ct);
            }
        }
    }

    private HttpRequestMessage MontarRequisicao(HttpMethod metodo, string rota, object? corpo, bool autenticar, Guid? jogadorId)
    {
        var requisicao = new HttpRequestMessage(metodo, new Uri(new Uri(_credenciais.UrlBase.TrimEnd('/') + "/"), rota));
        requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (autenticar)
        {
            var token = _credenciais.Token
                ?? throw new ApiException(HttpStatusCode.Unauthorized, new RespostaErro { Codigo = "NAO_AUTENTICADO", Mensagem = "Nenhum token configurado neste cliente." }, null);
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (jogadorId is { } id)
        {
            var chave = _credenciais.ObterChaveAtivacao(id)
                ?? throw new InvalidOperationException($"O perfil {id} não está ativado neste cliente. Ative-o antes de jogar.");
            requisicao.Headers.Add(CabecalhoAtivacao, chave);
        }

        if (corpo is not null)
        {
            requisicao.Content = JsonContent.Create(corpo, corpo.GetType(), options: OpcoesJson.Padrao);
        }

        return requisicao;
    }

    private static bool EhFalhaTransitoria(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested);

    private static async Task GarantirSucessoAsync(HttpResponseMessage resposta, CancellationToken ct)
    {
        if (resposta.IsSuccessStatusCode)
        {
            return;
        }

        var corpo = await resposta.Content.ReadAsStringAsync(ct);
        RespostaErro? erro = null;
        if (!string.IsNullOrWhiteSpace(corpo) && corpo.TrimStart().StartsWith('{'))
        {
            try
            {
                erro = JsonSerializer.Deserialize<RespostaErro>(corpo, OpcoesJson.Padrao);
            }
            catch (JsonException)
            {
                erro = null;
            }
        }

        TimeSpan? aguardar = resposta.Headers.RetryAfter?.Delta
            ?? (resposta.Headers.RetryAfter?.Date is { } data ? data - DateTimeOffset.UtcNow : null);
        throw new ApiException(resposta.StatusCode, erro, corpo, aguardar);
    }

    private static async Task<T> DesserializarAsync<T>(HttpResponseMessage resposta, CancellationToken ct)
    {
        var valor = await resposta.Content.ReadFromJsonAsync<T>(OpcoesJson.Padrao, ct);
        return valor ?? throw new ApiException(resposta.StatusCode, null, "Resposta sem corpo.");
    }
}
