using System.Net;
using System.Text.Json;
using DominoQuina.Core.Api;
using DominoQuina.Core.Armazenamento;
using DominoQuina.Core.Modelos;
using DominoQuina.Core.Regras;
using DominoQuina.Core.Servicos;
using DominoQuina.Tests.Apoio;
using static DominoQuina.Tests.Apoio.Fabrica;

namespace DominoQuina.Tests.Unidade;

public class SessaoEPerfisTests
{
    private static string Json(object o) => JsonSerializer.Serialize(o, OpcoesJson.Padrao);

    private static (SessaoPartida Sessao, HandlerFalso Handler) CriarSessao(EstadoPartidaParticipante inicial, Func<HttpRequestMessage, string?, int, HttpResponseMessage> responder)
    {
        var credenciais = new CredenciaisEmMemoria { UrlBase = "https://api.teste", Token = "t" };
        credenciais.SalvarChaveAtivacao(IdJogador, new string('b', 64));
        var handler = new HandlerFalso(responder);
        var api = new DominoApiClient(new HttpClient(handler), credenciais);
        return (new SessaoPartida(api, IdPartida, IdJogador, inicial), handler);
    }

    [Fact]
    public void AplicarEstado_IgnoraVersaoMaisAntiga()
    {
        var (sessao, _) = CriarSessao(Estado(versao: 10), (_, _, _) => new HttpResponseMessage(HttpStatusCode.OK));

        Assert.False(sessao.AplicarEstado(Estado(versao: 9)));
        Assert.Equal(10, sessao.Estado!.VersaoEstado);
        Assert.True(sessao.AplicarEstado(Estado(versao: 11)));
        Assert.Equal(11, sessao.Estado!.VersaoEstado);
    }

    [Fact]
    public void AplicarEstado_DeOutraPartida_EhIgnorado()
    {
        var (sessao, _) = CriarSessao(Estado(versao: 1), (_, _, _) => new HttpResponseMessage(HttpStatusCode.OK));
        Assert.False(sessao.AplicarEstado(Estado(versao: 50, partidaId: Guid.NewGuid())));
    }

    [Fact]
    public async Task Jogar_EnviaVersaoEsperadaEChaveNovaPorIntencao()
    {
        var (sessao, handler) = CriarSessao(Estado(versao: 4, mao: Pecas((1, 4)), mesa: Pecas((6, 1))), (_, _, n) =>
            HandlerFalso.Json(HttpStatusCode.OK, Json(new ConfirmacaoAcaoPartida { Estado = Estado(versao: 4 + n, proxima: ProximaAcaoPartida.Aguardar) })));

        var r1 = await sessao.JogarAsync(new Jogada(P(1, 4), LadoJogavel.Direita));
        var r2 = await sessao.ComprarAsync();

        Assert.True(r1.Sucesso);
        Assert.True(r2.Sucesso);
        var corpo1 = JsonDocument.Parse(handler.Recebidas[0].Corpo!).RootElement;
        var corpo2 = JsonDocument.Parse(handler.Recebidas[1].Corpo!).RootElement;
        Assert.Equal(4, corpo1.GetProperty("versaoEsperada").GetInt64());
        Assert.Equal(5, corpo2.GetProperty("versaoEsperada").GetInt64());
        Assert.Equal(1, corpo1.GetProperty("lado").GetInt32());
        Assert.NotEqual(corpo1.GetProperty("chaveIdempotencia").GetString(), corpo2.GetProperty("chaveIdempotencia").GetString());
        Assert.Equal(6, sessao.Estado!.VersaoEstado);
    }

    [Fact]
    public async Task Conflito409_AdotaOEstadoAutoritativo()
    {
        var autoritativo = Estado(versao: 20, proxima: ProximaAcaoPartida.Aguardar);
        var (sessao, _) = CriarSessao(Estado(versao: 4), (_, _, _) =>
            HandlerFalso.Json(HttpStatusCode.Conflict, Json(new RespostaErro { Codigo = "CONFLITO_DE_ESTADO", EstadoAutoritativo = autoritativo })));

        var resultado = await sessao.PassarAsync();

        Assert.False(resultado.Sucesso);
        Assert.Equal(20, sessao.Estado!.VersaoEstado);
        Assert.Equal(ProximaAcaoPartida.Aguardar, sessao.Estado.ProximaAcao);
    }

    [Fact]
    public async Task Rejeicao422_NaoAvancaAPartidaEMostraOMotivo()
    {
        var (sessao, _) = CriarSessao(Estado(versao: 4, mao: Pecas((2, 3)), mesa: Pecas((6, 1))), (_, _, _) =>
            HandlerFalso.Json(HttpStatusCode.UnprocessableEntity, """{"codigo":"ENCAIXE_INVALIDO"}"""));

        var resultado = await sessao.JogarAsync(new Jogada(P(2, 3), LadoJogavel.Esquerda));

        Assert.False(resultado.Sucesso);
        Assert.True(resultado.RegraRejeitada);
        Assert.Equal(4, sessao.Estado!.VersaoEstado);
        Assert.True(sessao.UltimaMensagemEhErro);
        Assert.Contains("não encaixa", sessao.UltimaMensagem);
    }

    [Fact]
    public async Task Ativar_GuardaAChaveAntesDeEnviarEReutilizaNaRepeticao()
    {
        var credenciais = new CredenciaisEmMemoria { UrlBase = "https://api.teste", Token = "t" };
        var chavesNoMomentoDoEnvio = new List<string?>();
        var handler = new HandlerFalso((_, corpo, _) =>
        {
            chavesNoMomentoDoEnvio.Add(credenciais.ObterChaveAtivacao(IdJogador));
            return HandlerFalso.Json(HttpStatusCode.OK, $$"""{"jogadorId":"{{IdJogador}}","nome":"Ana"}""");
        });
        var perfis = new GerenciadorPerfis(new DominoApiClient(new HttpClient(handler), credenciais), credenciais);

        await perfis.AtivarAsync(IdJogador);
        await perfis.AtivarAsync(IdJogador);

        var chave = credenciais.ObterChaveAtivacao(IdJogador);
        Assert.True(GeradorChaveAtivacao.EhValida(chave));
        Assert.All(chavesNoMomentoDoEnvio, c => Assert.Equal(chave, c));
        Assert.All(handler.Recebidas, r => Assert.Contains(chave!, r.Corpo));
    }

    [Fact]
    public async Task Ativar_JogadorJaAtivo_DescartaAChaveRecemGerada()
    {
        var credenciais = new CredenciaisEmMemoria { UrlBase = "https://api.teste", Token = "t" };
        var handler = new HandlerFalso((_, _, _) => HandlerFalso.Json(HttpStatusCode.Conflict, """{"codigo":"JOGADOR_JA_ATIVO"}"""));
        var perfis = new GerenciadorPerfis(new DominoApiClient(new HttpClient(handler), credenciais), credenciais);

        var ex = await Assert.ThrowsAsync<ApiException>(() => perfis.AtivarAsync(IdJogador));

        Assert.True(ex.JogadorJaAtivo);
        Assert.Null(credenciais.ObterChaveAtivacao(IdJogador));
    }

    [Fact]
    public async Task Desativar_EnviaAChaveAtualEDepoisAEsquece()
    {
        var credenciais = new CredenciaisEmMemoria { UrlBase = "https://api.teste", Token = "t" };
        credenciais.SalvarChaveAtivacao(IdJogador, new string('c', 64));
        var handler = new HandlerFalso((_, _, _) => HandlerFalso.Json(HttpStatusCode.OK, $$"""{"jogadorId":"{{IdJogador}}"}"""));
        var perfis = new GerenciadorPerfis(new DominoApiClient(new HttpClient(handler), credenciais), credenciais);

        await perfis.DesativarAsync(IdJogador);

        var req = handler.Recebidas.Single().Requisicao;
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.Equal(new string('c', 64), req.Headers.GetValues(DominoApiClient.CabecalhoAtivacao).Single());
        Assert.Null(credenciais.ObterChaveAtivacao(IdJogador));
    }

    [Fact]
    public async Task EntrarComTokenInvalido_NaoGuardaOToken()
    {
        var credenciais = new CredenciaisEmMemoria { UrlBase = "https://api.teste" };
        var handler = new HandlerFalso((_, _, _) => HandlerFalso.Json(HttpStatusCode.Unauthorized, """{"codigo":"NAO_AUTENTICADO"}"""));
        var perfis = new GerenciadorPerfis(new DominoApiClient(new HttpClient(handler), credenciais), credenciais);

        await Assert.ThrowsAsync<ApiException>(() => perfis.EntrarComTokenAsync("ruim"));

        Assert.Null(credenciais.Token);
    }
}
