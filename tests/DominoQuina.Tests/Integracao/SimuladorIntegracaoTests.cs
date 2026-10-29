using System.Net;
using System.Net.Sockets;
using System.Net.Http.Json;
using DominoQuina.Core.Api;
using DominoQuina.Core.Armazenamento;
using DominoQuina.Core.Modelos;
using DominoQuina.Core.Regras;
using DominoQuina.Core.Servicos;
using DominoQuina.Simulador;
using Microsoft.AspNetCore.Builder;

namespace DominoQuina.Tests.Integracao;

/// <summary>
/// Testes ponta a ponta: o cliente real (HTTP, cabeçalhos, JSON, idempotência) contra o simulador da API
/// rodando em uma porta local. Valida o fluxo completo exigido no trabalho, inclusive o jogador autônomo.
/// </summary>
public class SimuladorIntegracaoTests
{
    private sealed class Ambiente : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly List<HttpClient> _clientes = [];

        private Ambiente(WebApplication app, string url)
        {
            _app = app;
            Url = url;
        }

        public string Url { get; }

        public static async Task<Ambiente> IniciarAsync(int semente = 7)
        {
            var url = $"http://127.0.0.1:{PortaLivre()}";
            var app = ServidorSimulador.Construir(url, new MotorSimulador(semente), registrarTokensNoConsole: false);
            await app.StartAsync();
            return new Ambiente(app, url);
        }

        /// <summary>Cria um "cliente" independente (credenciais próprias), como outro aplicativo.</summary>
        public (IDominoApi Api, GerenciadorPerfis Perfis, CredenciaisEmMemoria Credenciais) NovoCliente()
        {
            var credenciais = new CredenciaisEmMemoria { UrlBase = Url };
            var http = new HttpClient(new SocketsHttpHandler { UseProxy = false });
            _clientes.Add(http);
            var api = new DominoApiClient(http, credenciais);
            return (api, new GerenciadorPerfis(api, credenciais), credenciais);
        }

        public async Task<string> TokenAsync(string email)
        {
            using var http = new HttpClient(new SocketsHttpHandler { UseProxy = false });
            var resposta = await http.PostAsJsonAsync($"{Url}/api/identidade/token", new { email });
            Assert.Equal(HttpStatusCode.Accepted, resposta.StatusCode);
            return await http.GetStringAsync($"{Url}/simulador/token?email={email}");
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var c in _clientes)
            {
                c.Dispose();
            }

            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        private static int PortaLivre()
        {
            using var socket = new TcpListener(IPAddress.Loopback, 0);
            socket.Start();
            return ((IPEndPoint)socket.LocalEndpoint).Port;
        }
    }

    private static async Task<(SessaoPartida A, SessaoPartida B)> PrepararPartidaAsync(Ambiente amb, int alvo)
    {
        var token = await amb.TokenAsync("grupo@fiap.com.br");

        var (apiA, perfisA, _) = amb.NovoCliente();
        await perfisA.EntrarComTokenAsync(token);
        var ana = await perfisA.CriarAsync("Ana");
        await perfisA.AtivarAsync(ana.JogadorId);

        var (apiB, perfisB, _) = amb.NovoCliente();
        await perfisB.EntrarComTokenAsync(token);
        var bia = await perfisB.CriarAsync("Bia");
        await perfisB.AtivarAsync(bia.JogadorId);

        var a = await SessaoPartida.CriarPartidaAsync(apiA, ana.JogadorId, alvo);
        var disponivel = Assert.Single(await apiB.ListarPartidasAsync(), p => p.PartidaId == a.PartidaId);
        var b = await SessaoPartida.EntrarAsync(apiB, disponivel, bia.JogadorId);
        await a.AtualizarAsync();
        return (a, b);
    }

    [Fact]
    public async Task DoisJogadoresAutonomos_JogamUmaPartidaCompletaAteOVencedor()
    {
        await using var amb = await Ambiente.IniciarAsync();
        var (a, b) = await PrepararPartidaAsync(amb, alvo: 25);
        var opcoes = new OpcoesJogadorAutonomo { IntervaloConsulta = TimeSpan.FromMilliseconds(20), PausaAntesDeAgir = TimeSpan.Zero };
        var rejeicoes = new List<string>();
        var botA = new JogadorAutonomo(a, opcoes: opcoes);
        var botB = new JogadorAutonomo(b, opcoes: opcoes);
        botA.Registro += m => { if (m.StartsWith('✖') && !m.Contains("CONFLITO_DE_ESTADO")) rejeicoes.Add(m); };
        botB.Registro += m => { if (m.StartsWith('✖') && !m.Contains("CONFLITO_DE_ESTADO")) rejeicoes.Add(m); };

        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var finais = await Task.WhenAll(botA.JogarAteOFimAsync(limite.Token), botB.JogarAteOFimAsync(limite.Token));

        Assert.All(finais, f => Assert.Equal(SituacaoPartida.Encerrada, f!.Situacao));
        Assert.Empty(rejeicoes);
        var resultado = await a.ObterResultadoAsync();
        Assert.NotNull(resultado.VencedorId);
        var vencedor = resultado.Participantes!.Single(p => p.JogadorId == resultado.VencedorId);
        Assert.True(vencedor.PontosNoPlacar >= 25);
        Assert.NotEmpty(resultado.Eventos!);
        // A visão de cada participante nunca contém a mão do adversário: só a própria.
        Assert.All(finais, f => Assert.True((f!.MinhaMao?.Count ?? 0) <= 21));
    }

    [Fact]
    public async Task Iniciar_DistribuiSetePedrasEIndicaAAberturaObrigatoria()
    {
        await using var amb = await Ambiente.IniciarAsync();
        var (a, b) = await PrepararPartidaAsync(amb, alvo: 50);

        Assert.True((await a.IniciarAsync()).Sucesso);
        await b.AtualizarAsync();

        Assert.Equal(7, a.Estado!.MinhaMao!.Count);
        Assert.Equal(7, b.Estado!.MinhaMao!.Count);
        Assert.Empty(a.Estado.MinhaMao.Intersect(b.Estado.MinhaMao));
        var quemAbre = a.Estado.ProximaAcao == ProximaAcaoPartida.JogarAbertura ? a : b;
        var outro = quemAbre == a ? b : a;
        Assert.Equal(ProximaAcaoPartida.Aguardar, outro.Estado!.ProximaAcao);
        Assert.NotNull(quemAbre.Estado!.PedraAberturaObrigatoria);
        Assert.Contains(quemAbre.Estado.MinhaMao!, p => p.MesmaPedra(quemAbre.Estado.PedraAberturaObrigatoria));
    }

    [Fact]
    public async Task AcoesInvalidas_SaoRejeitadasSemAvancarAPartida()
    {
        await using var amb = await Ambiente.IniciarAsync();
        var (a, b) = await PrepararPartidaAsync(amb, alvo: 50);
        await a.IniciarAsync();
        await b.AtualizarAsync();
        var quemAbre = a.Estado!.ProximaAcao == ProximaAcaoPartida.JogarAbertura ? a : b;
        var outro = quemAbre == a ? b : a;
        var versao = quemAbre.Estado!.VersaoEstado;

        // Fora do turno
        var foraDoTurno = await outro.JogarAsync(new Jogada(outro.Estado!.MinhaMao![0], LadoJogavel.Esquerda));
        Assert.True(foraDoTurno.RegraRejeitada);
        Assert.Equal("FORA_DO_TURNO", foraDoTurno.Erro!.Codigo);

        // Abertura com pedra diferente da obrigatória
        var errada = quemAbre.Estado.MinhaMao!.First(p => !p.MesmaPedra(quemAbre.Estado.PedraAberturaObrigatoria));
        var aberturaErrada = await quemAbre.JogarAsync(new Jogada(errada, LadoJogavel.Esquerda));
        Assert.True(aberturaErrada.RegraRejeitada);

        // Passar indevidamente
        Assert.True((await quemAbre.PassarAsync()).RegraRejeitada);

        Assert.Equal(versao, quemAbre.Estado.VersaoEstado);
        await quemAbre.AtualizarAsync();
        Assert.Equal(versao, quemAbre.Estado.VersaoEstado);
    }

    [Fact]
    public async Task VersaoDesatualizada_Recebe409EAdotaOEstadoOficial()
    {
        await using var amb = await Ambiente.IniciarAsync();
        var (a, b) = await PrepararPartidaAsync(amb, alvo: 50);
        await a.IniciarAsync(); // b ainda acha que a partida está na sala de espera

        var resultado = await b.IniciarAsync();

        Assert.False(resultado.Sucesso);
        Assert.True(resultado.Erro!.Conflito);
        Assert.Equal(SituacaoPartida.EmAndamento, b.Estado!.Situacao);
        Assert.Equal(a.Estado!.VersaoEstado, b.Estado.VersaoEstado);
    }

    [Fact]
    public async Task Perfil_NaoPodeFicarAtivoEmDoisClientes()
    {
        await using var amb = await Ambiente.IniciarAsync();
        var token = await amb.TokenAsync("grupo@fiap.com.br");
        var (_, perfis1, _) = amb.NovoCliente();
        var (_, perfis2, _) = amb.NovoCliente();
        await perfis1.EntrarComTokenAsync(token);
        await perfis2.EntrarComTokenAsync(token);
        var perfil = await perfis1.CriarAsync("Único");

        await perfis1.AtivarAsync(perfil.JogadorId);
        await perfis1.AtivarAsync(perfil.JogadorId); // retomar com a mesma chave: aceito
        var ex = await Assert.ThrowsAsync<ApiException>(() => perfis2.AtivarAsync(perfil.JogadorId));
        Assert.True(ex.JogadorJaAtivo);

        await perfis1.DesativarAsync(perfil.JogadorId); // liberar para transferência
        await perfis2.AtivarAsync(perfil.JogadorId);
        Assert.True(perfis2.EstaAtivoAqui(perfil.JogadorId));
    }

    [Fact]
    public async Task Atualizacoes_SemMudanca_Retorna204()
    {
        await using var amb = await Ambiente.IniciarAsync();
        var (a, _) = await PrepararPartidaAsync(amb, alvo: 50);

        Assert.False(await a.AtualizarAsync());
    }

    [Fact]
    public async Task TokenRevogado_PassaARetornar401()
    {
        await using var amb = await Ambiente.IniciarAsync();
        var token = await amb.TokenAsync("revoga@fiap.com.br");
        var (api, perfis, _) = amb.NovoCliente();
        await perfis.EntrarComTokenAsync(token);

        await api.RevogarTokenAsync();

        var ex = await Assert.ThrowsAsync<ApiException>(() => api.ListarJogadoresAsync());
        Assert.True(ex.NaoAutenticado);
    }
}
