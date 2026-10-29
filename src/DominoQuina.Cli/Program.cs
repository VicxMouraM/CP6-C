using System.Text;
using DominoQuina.Cli;
using DominoQuina.Core.Api;
using DominoQuina.Core.Armazenamento;
using DominoQuina.Core.Estrategia;
using DominoQuina.Core.Modelos;
using DominoQuina.Core.Servicos;

Console.OutputEncoding = Encoding.UTF8;

var credenciais = new CredenciaisEmArquivo();
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
var api = new DominoApiClient(http, credenciais);
var perfis = new GerenciadorPerfis(api, credenciais);
using var cancelamento = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancelamento.Cancel(); };
var ct = cancelamento.Token;

var argumentos = new Argumentos(args);
try
{
    return argumentos.Comando switch
    {
        "url" => DefinirUrl(),
        "token" => await SolicitarTokenAsync(),
        "login" => await LoginAsync(),
        "sessao" => await SessaoAsync(),
        "sair" => await SairAsync(),
        "perfis" => await ListarPerfisAsync(),
        "criar-perfil" => await CriarPerfilAsync(),
        "ativar" => await AtivarAsync(),
        "desativar" => await DesativarAsync(),
        "partidas" => await ListarPartidasAsync(),
        "autonomo" => await AutonomoAsync(),
        "jogar" => await JogarInterativoAsync(),
        "resultado" => await ResultadoAsync(),
        _ => Ajuda(),
    };
}
catch (ApiException ex)
{
    Console.Error.WriteLine($"Erro da API → {ex.Message}");
    if (ex.NaoAutenticado)
    {
        Console.Error.WriteLine("Solicite um token (domino token <email>) e use domino login <token>.");
    }

    return 2;
}
catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FormatException or HttpRequestException)
{
    Console.Error.WriteLine($"Erro → {ex.Message}");
    return 1;
}
catch (OperationCanceledException)
{
    Console.WriteLine("Interrompido.");
    return 130;
}

int Ajuda()
{
    Console.WriteLine($"""
        Dominó Ponta de Quina — cliente de linha de comando
        API: {credenciais.UrlBase}
        Credenciais: {credenciais.CaminhoArquivo}

        Conta
          domino url <endereço>              Define o endereço base da API
          domino token <email@fiap.com.br>   Pede o token por e-mail
          domino login <token>               Guarda e valida o token recebido
          domino sessao                      Mostra a sessão do token
          domino sair [--revogar]            Esquece (e opcionalmente revoga) o token

        Perfis
          domino perfis                      Lista perfis e quais estão ativos aqui
          domino criar-perfil <nome>         Cria um perfil
          domino ativar <jogadorId>          Ativa o perfil neste cliente (gera e guarda a chave)
          domino desativar <jogadorId>       Libera o perfil para outro cliente

        Partidas
          domino partidas                    Lista mesas com vaga
          domino autonomo --jogador <id> --criar <alvo>          Cria a mesa e joga sozinho
          domino autonomo --jogador <id> --entrar [partidaId]    Entra (na primeira vaga ou na indicada) e joga sozinho
          domino autonomo --jogador <id> --partida <partidaId>   Retoma uma partida e joga sozinho
          domino jogar --jogador <id> (--criar <alvo> | --entrar [id] | --partida <id>)   Joga você mesmo
          domino resultado --jogador <id> --partida <id>         Resultado e histórico
        """);
    return 0;
}

int DefinirUrl()
{
    credenciais.UrlBase = argumentos.Posicional(0, "endereço");
    Console.WriteLine($"API configurada: {credenciais.UrlBase}");
    return 0;
}

async Task<int> SolicitarTokenAsync()
{
    var email = argumentos.Posicional(0, "e-mail");
    await perfis.SolicitarTokenAsync(email, ct);
    Console.WriteLine($"Pedido aceito (202). O token será enviado para {email}. Depois use: domino login <token>");
    return 0;
}

async Task<int> LoginAsync()
{
    var sessao = await perfis.EntrarComTokenAsync(argumentos.Posicional(0, "token"), ct);
    Console.WriteLine($"Token válido e guardado. Usuário: {sessao.UsuarioId}");
    return 0;
}

async Task<int> SessaoAsync()
{
    var sessao = await api.ObterSessaoAsync(ct);
    Console.WriteLine($"Usuário: {sessao.UsuarioId} {sessao.Email}");
    return 0;
}

async Task<int> SairAsync()
{
    await perfis.SairAsync(argumentos.Tem("--revogar"), ct);
    Console.WriteLine("Token removido deste cliente.");
    return 0;
}

async Task<int> ListarPerfisAsync()
{
    var lista = await perfis.ListarAsync(ct);
    if (lista.Count == 0)
    {
        Console.WriteLine("Nenhum perfil. Crie com: domino criar-perfil <nome>");
    }

    foreach (var p in lista)
    {
        var situacao = p.AtivoNesteCliente ? "ATIVO AQUI" : p.Perfil.AtivoEmAlgumCliente == true ? "ativo em outro cliente" : "livre";
        Console.WriteLine($"{p.Perfil.JogadorId}  {p.Perfil.Nome,-25} {situacao}");
    }

    return 0;
}

async Task<int> CriarPerfilAsync()
{
    var perfil = await perfis.CriarAsync(string.Join(' ', argumentos.Posicionais), ct);
    Console.WriteLine($"Perfil criado: {perfil.JogadorId} {perfil.Nome}");
    Console.WriteLine($"Ative neste cliente com: domino ativar {perfil.JogadorId}");
    return 0;
}

async Task<int> AtivarAsync()
{
    var id = Guid.Parse(argumentos.Posicional(0, "jogadorId"));
    try
    {
        await perfis.AtivarAsync(id, ct);
        Console.WriteLine($"Perfil {id} ativo neste cliente. A chave fica guardada em {credenciais.CaminhoArquivo}.");
        return 0;
    }
    catch (ApiException ex) when (ex.JogadorJaAtivo)
    {
        Console.Error.WriteLine("Este perfil está ativo em outro cliente (409 JOGADOR_JA_ATIVO). Libere-o lá com 'desativar' ou use outro perfil.");
        return 3;
    }
}

async Task<int> DesativarAsync()
{
    var id = Guid.Parse(argumentos.Posicional(0, "jogadorId"));
    await perfis.DesativarAsync(id, ct);
    Console.WriteLine($"Perfil {id} liberado. Outro cliente já pode ativá-lo.");
    return 0;
}

async Task<int> ListarPartidasAsync()
{
    var partidas = await api.ListarPartidasAsync(ct);
    if (partidas.Count == 0)
    {
        Console.WriteLine("Nenhuma mesa aguardando adversário.");
    }

    foreach (var p in partidas)
    {
        Console.WriteLine($"{p.PartidaId}  1/2  até {p.PontuacaoAlvo} pontos  (versão {p.VersaoEstado})");
    }

    return 0;
}

async Task<SessaoPartida> AbrirSessaoAsync()
{
    var jogadorId = Guid.Parse(argumentos.Valor("--jogador") ?? throw new ArgumentException("Informe --jogador <id>."));
    if (!perfis.EstaAtivoAqui(jogadorId))
    {
        Console.WriteLine("Ativando o perfil neste cliente...");
        await perfis.AtivarAsync(jogadorId, ct);
    }

    SessaoPartida sessao;
    if (argumentos.Valor("--criar") is { } alvoTexto)
    {
        sessao = await SessaoPartida.CriarPartidaAsync(api, jogadorId, int.Parse(alvoTexto), ct);
        Console.WriteLine($"Mesa criada: {sessao.PartidaId}. Aguardando adversário...");
    }
    else if (argumentos.Tem("--entrar"))
    {
        var disponiveis = await api.ListarPartidasAsync(ct);
        var desejada = argumentos.Valor("--entrar") is { } texto && Guid.TryParse(texto, out var id)
            ? disponiveis.FirstOrDefault(p => p.PartidaId == id)
            : disponiveis.Count > 0 ? disponiveis[0] : null;
        if (desejada is null)
        {
            throw new InvalidOperationException("Nenhuma mesa disponível com esse critério.");
        }

        sessao = await SessaoPartida.EntrarAsync(api, desejada, jogadorId, ct);
        Console.WriteLine($"Entrou na mesa {sessao.PartidaId} (até {desejada.PontuacaoAlvo}).");
    }
    else if (argumentos.Valor("--partida") is { } partidaTexto)
    {
        sessao = new SessaoPartida(api, Guid.Parse(partidaTexto), jogadorId);
        await sessao.CarregarAsync(ct);
    }
    else
    {
        throw new ArgumentException("Use --criar <alvo>, --entrar [partidaId] ou --partida <partidaId>.");
    }

    credenciais.RegistrarPartida(sessao.PartidaId, jogadorId);
    return sessao;
}

async Task<int> AutonomoAsync()
{
    using var sessao = await AbrirSessaoAsync();
    var jogador = new JogadorAutonomo(sessao, new EstrategiaPontuadora(), new OpcoesJogadorAutonomo
    {
        IniciarAutomaticamente = !argumentos.Tem("--sem-iniciar"),
    });
    var ultimoDesenho = string.Empty;
    jogador.Registro += mensagem => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {mensagem}");
    sessao.Alterado += () =>
    {
        var desenho = RenderizadorConsole.Desenhar(sessao);
        if (desenho != ultimoDesenho)
        {
            ultimoDesenho = desenho;
            Console.WriteLine(desenho);
        }
    };

    Console.WriteLine(RenderizadorConsole.Desenhar(sessao));
    var final = await jogador.JogarAteOFimAsync(ct);
    if (final?.Situacao == SituacaoPartida.Encerrada)
    {
        await MostrarResultadoAsync(sessao);
    }

    return 0;
}

async Task<int> JogarInterativoAsync()
{
    using var sessao = await AbrirSessaoAsync();
    while (!ct.IsCancellationRequested)
    {
        var estado = sessao.Estado!;
        if (estado.Situacao == SituacaoPartida.Encerrada)
        {
            Console.Clear();
            Console.WriteLine(RenderizadorConsole.Desenhar(sessao));
            await MostrarResultadoAsync(sessao);
            return 0;
        }

        if (estado.Situacao == SituacaoPartida.AguardandoInicio && (estado.Participantes?.Count ?? 0) >= 2)
        {
            Console.Clear();
            Console.WriteLine(RenderizadorConsole.Desenhar(sessao));
            Console.Write("Dois jogadores na mesa. Enter para iniciar a partida (ou aguarde o adversário iniciar): ");
            if (Console.ReadLine() is not null)
            {
                await sessao.IniciarAsync(ct);
            }

            continue;
        }

        if (!estado.EhMinhaVez)
        {
            Console.Clear();
            Console.WriteLine(RenderizadorConsole.Desenhar(sessao));
            await Task.Delay(1000, ct);
            await sessao.AtualizarAsync(ct);
            continue;
        }

        var jogadas = sessao.JogadasPossiveis();
        Console.Clear();
        Console.WriteLine(RenderizadorConsole.Desenhar(sessao, jogadas));
        Console.Write("Escolha o número da jogada, [c]omprar, [p]assar, [d]ica ou Enter para atualizar: ");
        var entrada = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (entrada is null)
        {
            return 0;
        }

        if (entrada == "c")
        {
            await sessao.ComprarAsync(ct);
        }
        else if (entrada == "p")
        {
            await sessao.PassarAsync(ct);
        }
        else if (entrada == "d")
        {
            var dica = new EstrategiaPontuadora().Escolher(estado);
            Console.WriteLine(dica is null ? "Sem encaixe: compre (ou passe, se não houver cava)." : $"Sugestão: {dica}");
            await Task.Delay(1500, ct);
        }
        else if (int.TryParse(entrada, out var numero) && numero >= 1 && numero <= jogadas.Count)
        {
            await sessao.JogarAsync(jogadas[numero - 1], ct);
        }
        else
        {
            await sessao.AtualizarAsync(ct);
        }
    }

    return 0;
}

async Task<int> ResultadoAsync()
{
    using var sessao = await AbrirSessaoAsync();
    await MostrarResultadoAsync(sessao);
    return 0;
}

async Task MostrarResultadoAsync(SessaoPartida sessao)
{
    var resultado = await sessao.ObterResultadoAsync(ct);
    Console.WriteLine($"Resultado: {RenderizadorConsole.Situacao(resultado.Situacao)} · vencedor: {(resultado.VencedorId == sessao.JogadorId ? "VOCÊ" : resultado.VencedorId?.ToString() ?? "-")}");
    foreach (var p in resultado.Participantes ?? [])
    {
        Console.WriteLine($"  {p.NomeExibicao}: {p.PontosNoPlacar?.ToString() ?? "-"} pontos");
    }

    Console.WriteLine($"Eventos registrados: {resultado.Eventos?.Count ?? 0}");
    foreach (var e in (resultado.Eventos ?? []).TakeLast(10))
    {
        var quem = e.JogadorId == sessao.JogadorId ? "Você" : "Adversário";
        var oque = e.PassouVez ? "passou" : e.PecaJogada is { } peca ? $"jogou {peca}" : "agiu";
        Console.WriteLine($"  {e.RecebidoEmUtc.ToLocalTime():HH:mm:ss} {quem} {oque}{(e.PontosConcedidos > 0 ? $" (+{e.PontosConcedidos})" : string.Empty)}");
    }
}
