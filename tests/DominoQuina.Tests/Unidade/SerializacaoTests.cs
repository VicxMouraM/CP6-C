using System.Text.Json;
using DominoQuina.Core.Api;
using DominoQuina.Core.Modelos;
using DominoQuina.Core.Servicos;

namespace DominoQuina.Tests.Unidade;

public class SerializacaoTests
{
    [Fact]
    public void Estado_ComEnumsNumericos_EhLidoCorretamente()
    {
        const string json = """
            {"partidaId":"aaaaaaaa-0000-0000-0000-000000000001","versaoEstado":7,"situacao":1,"numeroRodada":2,
             "minhaMao":[{"valorA":6,"valorB":1}],"tabuleiro":[{"valorA":3,"valorB":3}],
             "participantes":[{"jogadorId":"bbbbbbbb-0000-0000-0000-000000000002","nome":"VicMoura","pontuacao":12,"quantidadePedrasNaMao":4,"ehVez":true}],
             "proximaAcao":1,"pedraAberturaObrigatoria":{"valorA":6,"valorB":6},"pontuacaoAlvo":50}
            """;

        var estado = JsonSerializer.Deserialize<EstadoPartidaParticipante>(json, OpcoesJson.Padrao)!;

        Assert.Equal(7, estado.VersaoEstado);
        Assert.Equal(SituacaoPartida.EmAndamento, estado.Situacao);
        Assert.Equal(ProximaAcaoPartida.JogarAbertura, estado.ProximaAcao);
        Assert.Equal(new Peca(6, 6), estado.PedraAberturaObrigatoria);
        Assert.Equal(new Peca(6, 1), Assert.Single(estado.MinhaMao!));
        Assert.Equal(50, estado.PontuacaoAlvo);
        Assert.True(estado.EhMinhaVez);

        var participante = Assert.Single(estado.Participantes!);
        Assert.Equal(12, participante.PontosNoPlacar);
        Assert.Equal(4, participante.PedrasNaMao);
        Assert.True(participante.EhVez);
        Assert.Equal("VicMoura", participante.NomeExibicao);
    }

    [Fact]
    public void Estado_ComEnumsEmTexto_TambemEhLido()
    {
        const string json = """{"situacao":"emAndamento","proximaAcao":"comprar"}""";

        var estado = JsonSerializer.Deserialize<EstadoPartidaParticipante>(json, OpcoesJson.Padrao)!;

        Assert.Equal(SituacaoPartida.EmAndamento, estado.Situacao);
        Assert.Equal(ProximaAcaoPartida.Comprar, estado.ProximaAcao);
    }

    [Fact]
    public void Participante_ComNomesAlternativos_EncontraPlacarEPedras()
    {
        const string json = """{"jogadorId":"bbbbbbbb-0000-0000-0000-000000000002","pontos":9,"pecasNaMao":3}""";

        var p = JsonSerializer.Deserialize<ResumoParticipantePartida>(json, OpcoesJson.Padrao)!;

        Assert.Equal(9, p.PontosNoPlacar);
        Assert.Equal(3, p.PedrasNaMao);
    }

    [Fact]
    public void RespostaErro_LeEstadoAutoritativo()
    {
        const string json = """{"codigo":"CONFLITO_DE_ESTADO","mensagem":"versão","estadoAutoritativo":{"versaoEstado":9,"proximaAcao":0}}""";

        var erro = JsonSerializer.Deserialize<RespostaErro>(json, OpcoesJson.Padrao)!;

        Assert.Equal("CONFLITO_DE_ESTADO", erro.CodigoEfetivo);
        Assert.Equal(9, erro.EstadoAutoritativo!.VersaoEstado);
    }

    [Fact]
    public void JogarRequest_EhSerializadoEmCamelCaseComLadoNumerico()
    {
        var chave = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var json = JsonSerializer.Serialize(new JogarRequest(Guid.Empty, 2, chave, 6, 1, LadoJogavel.Esquerda), OpcoesJson.Padrao);

        Assert.Contains("\"versaoEsperada\":2", json);
        Assert.Contains("\"chaveIdempotencia\":\"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\"", json);
        Assert.Contains("\"valorA\":6", json);
        Assert.Contains("\"lado\":0", json);
    }

    [Fact]
    public void ChaveAtivacao_Tem64HexadecimaisEEhUnica()
    {
        var a = GeradorChaveAtivacao.Gerar();
        var b = GeradorChaveAtivacao.Gerar();

        Assert.Equal(64, a.Length);
        Assert.True(GeradorChaveAtivacao.EhValida(a));
        Assert.NotEqual(a, b);
        Assert.False(GeradorChaveAtivacao.EhValida("xyz"));
    }
}
