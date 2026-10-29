using System.Net;
using DominoQuina.Core.Api;
using DominoQuina.Core.Armazenamento;
using DominoQuina.Core.Modelos;
using DominoQuina.Tests.Apoio;
using static DominoQuina.Tests.Apoio.Fabrica;

namespace DominoQuina.Tests.Unidade;

public class DominoApiClientTests
{
    private const string EstadoJson = """{"partidaId":"aaaaaaaa-0000-0000-0000-000000000001","versaoEstado":3,"situacao":1,"proximaAcao":2}""";
    private static readonly string ConfirmacaoJson = $$"""{"chaveIdempotencia":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","acaoIdempotente":false,"estado":{{EstadoJson}}}""";

    private static (DominoApiClient Api, HandlerFalso Handler, CredenciaisEmMemoria Credenciais) Criar(Func<HttpRequestMessage, string?, int, HttpResponseMessage> responder)
    {
        var credenciais = new CredenciaisEmMemoria { UrlBase = "https://api.teste", Token = "token-123" };
        credenciais.SalvarChaveAtivacao(IdJogador, new string('a', 64));
        var handler = new HandlerFalso(responder);
        return (new DominoApiClient(new HttpClient(handler), credenciais), handler, credenciais);
    }

    [Fact]
    public async Task RotasPrivadas_EnviamBearerEChaveDeAtivacao()
    {
        var (api, handler, _) = Criar((_, _, _) => HandlerFalso.Json(HttpStatusCode.OK, EstadoJson));

        await api.ObterEstadoAsync(IdPartida, IdJogador);

        var (req, _) = Assert.Single(handler.Recebidas);
        Assert.Equal("Bearer", req.Headers.Authorization!.Scheme);
        Assert.Equal("token-123", req.Headers.Authorization.Parameter);
        Assert.Equal(new string('a', 64), req.Headers.GetValues(DominoApiClient.CabecalhoAtivacao).Single());
        Assert.Equal($"https://api.teste/api/partidas/{IdPartida}?jogadorId={IdJogador}", req.RequestUri!.ToString());
    }

    [Fact]
    public async Task Atualizacoes_204_RetornaNulo()
    {
        var (api, handler, _) = Criar((_, _, _) => new HttpResponseMessage(HttpStatusCode.NoContent));

        var estado = await api.ObterAtualizacaoAsync(IdPartida, IdJogador, 3);

        Assert.Null(estado);
        Assert.Contains("versaoConhecida=3", handler.Recebidas[0].Requisicao.RequestUri!.Query);
    }

    [Fact]
    public async Task Rejeicao422_ViraApiExceptionComCodigo()
    {
        var (api, _, _) = Criar((_, _, _) => HandlerFalso.Json(HttpStatusCode.UnprocessableEntity, """{"codigo":"ENCAIXE_INVALIDO","mensagem":"não encaixa"}"""));

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            api.JogarAsync(IdPartida, new JogarRequest(IdJogador, 3, Guid.NewGuid(), 6, 1, LadoJogavel.Direita)));

        Assert.True(ex.RegraRejeitada);
        Assert.Equal("ENCAIXE_INVALIDO", ex.Codigo);
        Assert.Contains("não encaixa", ex.Message);
    }

    [Fact]
    public async Task Token_EmailForaDaFiap_ExplicaOErro()
    {
        var (api, handler, _) = Criar((_, _, _) => HandlerFalso.Json(HttpStatusCode.BadRequest, "false"));

        var ex = await Assert.ThrowsAsync<ApiException>(() => api.SolicitarTokenAsync("alguem@gmail.com"));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("@fiap.com.br", ex.Message);
        Assert.Null(handler.Recebidas[0].Requisicao.Headers.Authorization);
    }

    [Fact]
    public async Task Token_429_InformaTempoDeEspera()
    {
        var (api, _, _) = Criar((_, _, _) =>
        {
            var r = HandlerFalso.Json(HttpStatusCode.TooManyRequests, """{"codigo":"LIMITE_EXCEDIDO"}""");
            r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(5));
            return r;
        });

        var ex = await Assert.ThrowsAsync<ApiException>(() => api.SolicitarTokenAsync("aluno@fiap.com.br"));

        Assert.Equal(TimeSpan.FromMinutes(5), ex.AguardarAntesDeRepetir);
    }

    [Fact]
    public async Task FalhaDeRede_EmAcaoIdempotente_RepeteComAMesmaChave()
    {
        var (api, handler, _) = Criar((_, _, n) => n == 1
            ? throw new HttpRequestException("conexão caiu")
            : HandlerFalso.Json(HttpStatusCode.OK, ConfirmacaoJson));
        var chave = Guid.NewGuid();

        var confirmacao = await api.ComprarAsync(IdPartida, new AcaoRequest(IdJogador, 3, chave));

        Assert.Equal(2, handler.Recebidas.Count);
        Assert.Equal(handler.Recebidas[0].Corpo, handler.Recebidas[1].Corpo);
        Assert.Contains(chave.ToString(), handler.Recebidas[1].Corpo);
        Assert.Equal(3, confirmacao.Estado!.VersaoEstado);
    }

    [Fact]
    public async Task CriarPartida_NaoRepeteAutomaticamente()
    {
        var (api, handler, _) = Criar((_, _, _) => throw new HttpRequestException("caiu"));

        await Assert.ThrowsAsync<HttpRequestException>(() => api.CriarPartidaAsync(IdJogador, 50));

        Assert.Single(handler.Recebidas);
    }

    [Fact]
    public async Task PerfilSemChave_NaoEnviaRequisicao()
    {
        var (api, handler, _) = Criar((_, _, _) => HandlerFalso.Json(HttpStatusCode.OK, EstadoJson));

        await Assert.ThrowsAsync<InvalidOperationException>(() => api.ObterEstadoAsync(IdPartida, Guid.NewGuid()));

        Assert.Empty(handler.Recebidas);
    }

    [Fact]
    public async Task SemToken_FalhaComo401SemChamarAApi()
    {
        var (api, handler, credenciais) = Criar((_, _, _) => HandlerFalso.Json(HttpStatusCode.OK, "[]"));
        credenciais.Token = null;

        var ex = await Assert.ThrowsAsync<ApiException>(() => api.ListarJogadoresAsync());

        Assert.True(ex.NaoAutenticado);
        Assert.Empty(handler.Recebidas);
    }
}
