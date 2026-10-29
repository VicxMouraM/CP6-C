using System.Net;
using System.Text;
using DominoQuina.Core.Modelos;

namespace DominoQuina.Tests.Apoio;

/// <summary>Atalhos para montar dados de teste.</summary>
internal static class Fabrica
{
    public static Peca P(int a, int b) => new(a, b);

    public static List<Peca> Pecas(params (int A, int B)[] valores) => valores.Select(v => new Peca(v.A, v.B)).ToList();

    public static EstadoPartidaParticipante Estado(
        long versao = 5,
        ProximaAcaoPartida proxima = ProximaAcaoPartida.Jogar,
        SituacaoPartida situacao = SituacaoPartida.EmAndamento,
        List<Peca>? mao = null,
        List<Peca>? mesa = null,
        Peca? abertura = null,
        int participantes = 2,
        Guid? partidaId = null) => new()
    {
        PartidaId = partidaId ?? IdPartida,
        VersaoEstado = versao,
        Situacao = situacao,
        NumeroRodada = 1,
        ProximaAcao = proxima,
        MinhaMao = mao ?? [],
        Tabuleiro = mesa ?? [],
        PedraAberturaObrigatoria = abertura,
        Participantes = Enumerable.Range(0, participantes).Select(i => new ResumoParticipantePartida { JogadorId = i == 0 ? IdJogador : Guid.NewGuid() }).ToList(),
    };

    public static readonly Guid IdPartida = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid IdJogador = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
}

/// <summary>HttpMessageHandler programável: registra as requisições e responde com a função dada.</summary>
internal sealed class HandlerFalso : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string?, int, HttpResponseMessage> _responder;

    public HandlerFalso(Func<HttpRequestMessage, string?, int, HttpResponseMessage> responder) => _responder = responder;

    public List<(HttpRequestMessage Requisicao, string? Corpo)> Recebidas { get; } = [];

    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var corpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Recebidas.Add((request, corpo));
        return _responder(request, corpo, Recebidas.Count);
    }
}

/// <summary>Relógio controlável para o cronômetro de turno.</summary>
internal sealed class RelogioFalso : TimeProvider
{
    private DateTimeOffset _agora = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _agora;

    public void Avancar(TimeSpan tempo) => _agora += tempo;
}
