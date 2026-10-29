using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace DominoQuina.Simulador;

/// <summary>Erro de negócio do simulador, convertido em resposta HTTP.</summary>
public sealed class SimuladorException(HttpStatusCode status, string codigo, string mensagem, object? estadoAutoritativo = null) : Exception(mensagem)
{
    public HttpStatusCode Status { get; } = status;
    public string Codigo { get; } = codigo;
    public object? EstadoAutoritativo { get; } = estadoAutoritativo;
}

public sealed record PecaSim(int ValorA, int ValorB)
{
    public bool Contem(int v) => ValorA == v || ValorB == v;
    public int Oposto(int v) => ValorA == v ? ValorB : ValorA;
    public bool Mesma(PecaSim o) => (ValorA == o.ValorA && ValorB == o.ValorB) || (ValorA == o.ValorB && ValorB == o.ValorA);
    public int Soma => ValorA + ValorB;
    public bool Dupla => ValorA == ValorB;
}

/// <summary>
/// Implementação em memória das regras do Dominó Ponta de Quina para dois jogadores,
/// seguindo o enunciado: abertura pela maior dupla (ou maior pedra), encaixe nas pontas,
/// compra de uma pedra, passagem só sem cava e sem encaixe, pontuação por múltiplos de 5,
/// fim de rodada por mão vazia ou travamento e fim de partida ao atingir o alvo.
/// </summary>
public sealed class MotorSimulador
{
    private readonly object _trava = new();
    private readonly Random _aleatorio;
    private readonly Dictionary<string, Usuario> _usuariosPorToken = [];
    private readonly Dictionary<string, Usuario> _usuariosPorEmail = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, Jogador> _jogadores = [];
    private readonly Dictionary<Guid, Partida> _partidas = [];

    public MotorSimulador(int? semente = null) => _aleatorio = semente is null ? new Random() : new Random(semente.Value);

    // ---------------- Identidade ----------------

    public static bool EmailValido(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.EndsWith("@fiap.com.br", StringComparison.OrdinalIgnoreCase) && email.IndexOf('@') > 0;

    /// <summary>Cria a conta (se não existir) e devolve o token que seria enviado por e-mail.</summary>
    public string EmitirToken(string email)
    {
        lock (_trava)
        {
            if (!_usuariosPorEmail.TryGetValue(email, out var usuario))
            {
                usuario = new Usuario(Guid.NewGuid(), email.ToLowerInvariant(), "sim-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant());
                _usuariosPorEmail[email] = usuario;
                _usuariosPorToken[usuario.Token] = usuario;
            }

            return usuario.Token;
        }
    }

    public Usuario Autenticar(string? cabecalho)
    {
        var token = cabecalho is not null && cabecalho.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? cabecalho[7..].Trim() : null;
        lock (_trava)
        {
            if (token is null || !_usuariosPorToken.TryGetValue(token, out var usuario) || usuario.Revogado)
            {
                throw new SimuladorException(HttpStatusCode.Unauthorized, "NAO_AUTENTICADO", "Token ausente, inválido ou revogado.");
            }

            return usuario;
        }
    }

    public void Revogar(Usuario usuario)
    {
        lock (_trava)
        {
            usuario.Revogado = true;
        }
    }

    // ---------------- Jogadores ----------------

    public IEnumerable<object> ListarJogadores(Usuario u)
    {
        lock (_trava)
        {
            return _jogadores.Values.Where(j => j.UsuarioId == u.Id).Select(VisaoJogador).ToList();
        }
    }

    public object CriarJogador(Usuario u, string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome) || nome.Length > 150)
        {
            throw new SimuladorException(HttpStatusCode.BadRequest, "ENTRADA_INVALIDA", "Nome entre 1 e 150 caracteres.");
        }

        lock (_trava)
        {
            var j = new Jogador(Guid.NewGuid(), u.Id, nome.Trim());
            _jogadores[j.Id] = j;
            return VisaoJogador(j);
        }
    }

    public object Ativar(Usuario u, Guid jogadorId, string? chave)
    {
        if (chave is null || chave.Length != 64 || !chave.All(Uri.IsHexDigit))
        {
            throw new SimuladorException(HttpStatusCode.BadRequest, "ENTRADA_INVALIDA", "A chave deve ter 64 caracteres hexadecimais.");
        }

        lock (_trava)
        {
            var j = JogadorDaConta(u, jogadorId);
            var hash = Hash(chave);
            if (j.HashChave is not null && j.HashChave != hash)
            {
                throw new SimuladorException(HttpStatusCode.Conflict, "JOGADOR_JA_ATIVO", "Perfil reservado por outra chave.");
            }

            j.HashChave = hash;
            return VisaoJogador(j);
        }
    }

    public object Desativar(Usuario u, Guid jogadorId, string? chave)
    {
        lock (_trava)
        {
            var j = ValidarAtivacao(u, jogadorId, chave);
            j.HashChave = null;
            return VisaoJogador(j);
        }
    }

    // ---------------- Partidas ----------------

    public IEnumerable<object> ListarPartidas()
    {
        lock (_trava)
        {
            return _partidas.Values
                .Where(p => p.Situacao == 0 && p.Participantes.Count == 1)
                .Select(p => new { partidaId = p.Id, pontuacaoAlvo = p.Alvo, versaoEstado = p.Versao })
                .ToList();
        }
    }

    public object CriarPartida(Usuario u, Guid jogadorId, string? chave, int alvo)
    {
        if (alvo <= 0)
        {
            throw new SimuladorException(HttpStatusCode.BadRequest, "ENTRADA_INVALIDA", "Pontuação alvo deve ser positiva.");
        }

        lock (_trava)
        {
            var j = ValidarAtivacao(u, jogadorId, chave);
            var partida = new Partida(Guid.NewGuid(), alvo);
            partida.Participantes.Add(new Participacao(Guid.NewGuid(), j.Id, 0));
            partida.Versao = 1;
            _partidas[partida.Id] = partida;
            return Visao(partida, j.Id);
        }
    }

    public object Entrar(Usuario u, Guid partidaId, Requisicao r, string? chave) => Acao(u, partidaId, r, chave, exigirParticipacao: false, (p, j) =>
    {
        if (p.Situacao != 0 || p.Participantes.Count != 1)
        {
            throw new SimuladorException(HttpStatusCode.NotFound, "PARTIDA_INDISPONIVEL", "Partida sem vaga.");
        }

        if (p.Participantes[0].JogadorId == j.Id)
        {
            throw new SimuladorException(HttpStatusCode.UnprocessableEntity, "ESTADO_INVALIDO", "O perfil já participa da partida.");
        }

        p.Participantes.Add(new Participacao(Guid.NewGuid(), j.Id, 1));
    });

    public object Iniciar(Usuario u, Guid partidaId, Requisicao r, string? chave) => Acao(u, partidaId, r, chave, true, (p, _) =>
    {
        if (p.Situacao != 0 || p.Participantes.Count != 2)
        {
            throw new SimuladorException(HttpStatusCode.UnprocessableEntity, "ESTADO_INVALIDO", "A partida precisa de duas participações e estar aguardando início.");
        }

        p.Situacao = 1;
        NovaRodada(p);
    });

    public object Jogar(Usuario u, Guid partidaId, Requisicao r, string? chave) => Acao(u, partidaId, r, chave, true, (p, j) =>
    {
        var eu = ExigirTurno(p, j.Id);
        if (r.ValorA is not (>= 0 and <= 6) || r.ValorB is not (>= 0 and <= 6) || r.Lado is not (0 or 1))
        {
            throw new SimuladorException(HttpStatusCode.BadRequest, "ENTRADA_INVALIDA", "Valores entre 0 e 6 e lado 0 ou 1.");
        }

        var alvo = new PecaSim(r.ValorA.Value, r.ValorB.Value);
        var peca = eu.Mao.FirstOrDefault(x => x.Mesma(alvo))
            ?? throw new SimuladorException(HttpStatusCode.UnprocessableEntity, "PECA_AUSENTE", "A pedra não está na mão.");

        if (p.Mesa.Count == 0)
        {
            if (p.Abertura is not null && !p.Abertura.Mesma(peca))
            {
                throw new SimuladorException(HttpStatusCode.UnprocessableEntity, "ENCAIXE_INVALIDO", "A abertura deve usar a pedra obrigatória.");
            }

            p.Mesa.Add(peca);
            p.Abertura = null;
        }
        else if (r.Lado == 0)
        {
            var ponta = p.Mesa[0].ValorA;
            if (!peca.Contem(ponta))
            {
                throw new SimuladorException(HttpStatusCode.UnprocessableEntity, "ENCAIXE_INVALIDO", "A pedra não encaixa na ponta esquerda.");
            }

            p.Mesa.Insert(0, new PecaSim(peca.Oposto(ponta), ponta));
        }
        else
        {
            var ponta = p.Mesa[^1].ValorB;
            if (!peca.Contem(ponta))
            {
                throw new SimuladorException(HttpStatusCode.UnprocessableEntity, "ENCAIXE_INVALIDO", "A pedra não encaixa na ponta direita.");
            }

            p.Mesa.Add(new PecaSim(ponta, peca.Oposto(ponta)));
        }

        eu.Mao.Remove(peca);
        var soma = p.Mesa[0].ValorA + p.Mesa[^1].ValorB;
        var pontos = soma > 0 && soma % 5 == 0 ? soma / 5 : 0;
        eu.Pontos += pontos;
        p.PassesSeguidos = 0;
        p.Eventos.Add(new Evento(Guid.NewGuid(), j.Id, peca, r.Lado.Value, false, pontos, DateTimeOffset.UtcNow));

        if (eu.Pontos >= p.Alvo)
        {
            Encerrar(p, eu);
        }
        else if (eu.Mao.Count == 0)
        {
            FimDeRodada(p, eu);
        }
        else
        {
            p.Turno = 1 - p.Turno;
        }
    });

    public object Comprar(Usuario u, Guid partidaId, Requisicao r, string? chave) => Acao(u, partidaId, r, chave, true, (p, j) =>
    {
        var eu = ExigirTurno(p, j.Id);
        if (p.Mesa.Count == 0 || TemEncaixe(p, eu))
        {
            throw new SimuladorException(HttpStatusCode.UnprocessableEntity, "ESTADO_INVALIDO", "Há pedra que encaixa: compra não permitida.");
        }

        if (p.Cava.Count > 0)
        {
            var comprada = p.Cava[^1];
            p.Cava.RemoveAt(p.Cava.Count - 1);
            eu.Mao.Add(comprada);
            if (TemEncaixe(p, eu))
            {
                return; // mantém o turno
            }
        }

        Passagem(p, eu, j.Id);
    });

    public object Passar(Usuario u, Guid partidaId, Requisicao r, string? chave) => Acao(u, partidaId, r, chave, true, (p, j) =>
    {
        var eu = ExigirTurno(p, j.Id);
        if (p.Mesa.Count == 0 || p.Cava.Count > 0 || TemEncaixe(p, eu))
        {
            throw new SimuladorException(HttpStatusCode.UnprocessableEntity, "PASSAGEM_NAO_PERMITIDA", "Há cava ou pedra compatível.");
        }

        Passagem(p, eu, j.Id);
    });

    public object Estado(Usuario u, Guid partidaId, Guid jogadorId, string? chave)
    {
        lock (_trava)
        {
            var j = ValidarAtivacao(u, jogadorId, chave);
            var p = PartidaDoJogador(partidaId, j.Id);
            return Visao(p, j.Id);
        }
    }

    public (bool Mudou, object? Estado) Atualizacoes(Usuario u, Guid partidaId, Guid jogadorId, long? versao, string? chave)
    {
        lock (_trava)
        {
            var j = ValidarAtivacao(u, jogadorId, chave);
            var p = PartidaDoJogador(partidaId, j.Id);
            return versao == p.Versao ? (false, null) : (true, Visao(p, j.Id));
        }
    }

    public object Observar(Guid partidaId)
    {
        lock (_trava)
        {
            var p = _partidas.GetValueOrDefault(partidaId) ?? throw new SimuladorException(HttpStatusCode.NotFound, "PARTIDA_INDISPONIVEL", "Partida não encontrada.");
            return new
            {
                partidaId = p.Id, versaoEstado = p.Versao, situacao = p.Situacao, numeroRodada = p.Rodada,
                jogadorDoTurnoId = JogadorDoTurno(p), tabuleiro = p.Mesa, participantes = Resumo(p), vencedorId = p.Vencedor,
            };
        }
    }

    public object Resultado(Usuario u, Guid partidaId, Guid jogadorId, string? chave)
    {
        lock (_trava)
        {
            var j = ValidarAtivacao(u, jogadorId, chave);
            var p = PartidaDoJogador(partidaId, j.Id);
            return new
            {
                partidaId = p.Id, situacao = p.Situacao, vencedorId = p.Vencedor, participantes = Resumo(p),
                eventos = p.Eventos.Select(e => new
                {
                    eventoId = e.Id, jogadorId = e.JogadorId, pecaJogada = e.Peca, lado = e.Lado,
                    passouVez = e.Passou, pontosConcedidos = e.Pontos, recebidoEmUtc = e.Quando,
                }),
            };
        }
    }

    // ---------------- Internos ----------------

    private object Acao(Usuario u, Guid partidaId, Requisicao r, string? chave, bool exigirParticipacao, Action<Partida, Jogador> efeito)
    {
        if (r.JogadorId is not { } jogadorId || r.ChaveIdempotencia is not { } idem)
        {
            throw new SimuladorException(HttpStatusCode.BadRequest, "ENTRADA_INVALIDA", "jogadorId e chaveIdempotencia são obrigatórios.");
        }

        lock (_trava)
        {
            var j = ValidarAtivacao(u, jogadorId, chave);
            var p = _partidas.GetValueOrDefault(partidaId) ?? throw new SimuladorException(HttpStatusCode.NotFound, "PARTIDA_INDISPONIVEL", "Partida não encontrada.");
            if (exigirParticipacao && p.Participantes.All(x => x.JogadorId != j.Id))
            {
                throw new SimuladorException(HttpStatusCode.Forbidden, "SEM_PERMISSAO", "O perfil não participa da partida.");
            }

            if (p.Idempotencia.TryGetValue((j.Id, idem), out var anterior))
            {
                return new { chaveIdempotencia = idem, acaoIdempotente = true, estado = Visao(p, j.Id) ?? anterior };
            }

            if (r.VersaoEsperada != p.Versao)
            {
                var autoritativo = p.Participantes.Any(x => x.JogadorId == j.Id) ? Visao(p, j.Id) : null;
                throw new SimuladorException(HttpStatusCode.Conflict, "CONFLITO_DE_ESTADO", $"Versão esperada {r.VersaoEsperada}, atual {p.Versao}.", autoritativo);
            }

            efeito(p, j);
            p.Versao++;
            var estado = Visao(p, j.Id);
            p.Idempotencia[(j.Id, idem)] = estado;
            return new { chaveIdempotencia = idem, acaoIdempotente = false, estado };
        }
    }

    private void NovaRodada(Partida p)
    {
        p.Rodada++;
        p.Mesa.Clear();
        p.PassesSeguidos = 0;
        var todas = new List<PecaSim>();
        for (var a = 0; a <= 6; a++)
        {
            for (var b = a; b <= 6; b++)
            {
                todas.Add(new PecaSim(a, b));
            }
        }

        var embaralhadas = todas.OrderBy(_ => _aleatorio.Next()).ToList();
        foreach (var part in p.Participantes)
        {
            part.Mao = embaralhadas.Take(7).ToList();
            embaralhadas.RemoveRange(0, 7);
        }

        p.Cava = embaralhadas;

        // Maior dupla; sem dupla, a maior pedra (soma, depois maior lado).
        var candidatas = p.Participantes.SelectMany((part, i) => part.Mao.Select(peca => (peca, i))).ToList();
        var duplas = candidatas.Where(c => c.peca.Dupla).ToList();
        var escolhida = duplas.Count > 0
            ? duplas.MaxBy(c => c.peca.ValorA)
            : candidatas.OrderByDescending(c => c.peca.Soma).ThenByDescending(c => Math.Max(c.peca.ValorA, c.peca.ValorB)).First();
        p.Turno = escolhida.i;
        p.Abertura = escolhida.peca;
    }

    private void FimDeRodada(Partida p, Participacao? vencedorRodada)
    {
        if (vencedorRodada is null)
        {
            // Travamento: vence quem tiver menos pontos na mão.
            var somas = p.Participantes.Select(x => x.Mao.Sum(m => m.Soma)).ToList();
            if (somas[0] != somas[1])
            {
                vencedorRodada = p.Participantes[somas[0] < somas[1] ? 0 : 1];
            }
        }

        if (vencedorRodada is not null)
        {
            var outro = p.Participantes.First(x => x != vencedorRodada);
            vencedorRodada.Pontos += outro.Mao.Sum(m => m.Soma) / 5;
            if (vencedorRodada.Pontos >= p.Alvo)
            {
                Encerrar(p, vencedorRodada);
                return;
            }
        }

        NovaRodada(p);
    }

    private static void Encerrar(Partida p, Participacao vencedor)
    {
        p.Situacao = 2;
        p.Vencedor = vencedor.JogadorId;
    }

    private void Passagem(Partida p, Participacao eu, Guid jogadorId)
    {
        p.PassesSeguidos++;
        p.Eventos.Add(new Evento(Guid.NewGuid(), jogadorId, null, null, true, 0, DateTimeOffset.UtcNow));
        if (p.PassesSeguidos >= 2 && p.Cava.Count == 0)
        {
            FimDeRodada(p, null);
        }
        else
        {
            p.Turno = 1 - p.Participantes.IndexOf(eu);
        }
    }

    private static Participacao ExigirTurno(Partida p, Guid jogadorId)
    {
        if (p.Situacao != 1)
        {
            throw new SimuladorException(HttpStatusCode.UnprocessableEntity, "ESTADO_INVALIDO", "A partida não está em andamento.");
        }

        var eu = p.Participantes.First(x => x.JogadorId == jogadorId);
        if (p.Participantes.IndexOf(eu) != p.Turno)
        {
            throw new SimuladorException(HttpStatusCode.UnprocessableEntity, "FORA_DO_TURNO", "Não é a sua vez.");
        }

        return eu;
    }

    private static bool TemEncaixe(Partida p, Participacao eu) =>
        p.Mesa.Count == 0 || eu.Mao.Any(m => m.Contem(p.Mesa[0].ValorA) || m.Contem(p.Mesa[^1].ValorB));

    private static int ProximaAcao(Partida p, Participacao eu)
    {
        if (p.Situacao == 2)
        {
            return 5;
        }

        if (p.Situacao == 0 || p.Participantes.IndexOf(eu) != p.Turno)
        {
            return 0;
        }

        if (p.Mesa.Count == 0)
        {
            return 1;
        }

        return TemEncaixe(p, eu) ? 2 : p.Cava.Count > 0 ? 3 : 4;
    }

    private static Guid? JogadorDoTurno(Partida p) => p.Situacao == 1 ? p.Participantes[p.Turno].JogadorId : null;

    private object Visao(Partida p, Guid jogadorId)
    {
        var eu = p.Participantes.FirstOrDefault(x => x.JogadorId == jogadorId);
        if (eu is null)
        {
            return null!;
        }

        var proxima = ProximaAcao(p, eu);
        return new
        {
            partidaId = p.Id,
            versaoEstado = p.Versao,
            situacao = p.Situacao,
            numeroRodada = p.Rodada,
            jogadorDoTurnoId = JogadorDoTurno(p),
            minhaMao = eu.Mao,
            tabuleiro = p.Mesa,
            participantes = Resumo(p),
            vencedorId = p.Vencedor,
            proximaAcao = proxima,
            pedraAberturaObrigatoria = proxima == 1 ? p.Abertura : null,
            participacaoId = eu.Id,
            pontuacaoAlvo = p.Alvo,
            quantidadeCava = p.Cava.Count,
        };
    }

    private List<object> Resumo(Partida p) => p.Participantes.Select(x => (object)new
    {
        jogadorId = x.JogadorId,
        participacaoId = x.Id,
        nome = _jogadores.GetValueOrDefault(x.JogadorId)?.Nome,
        posicao = x.Posicao,
        pontuacao = x.Pontos,
        quantidadePedrasNaMao = x.Mao.Count,
        ehVez = p.Situacao == 1 && p.Participantes.IndexOf(x) == p.Turno,
    }).ToList();

    private Partida PartidaDoJogador(Guid partidaId, Guid jogadorId)
    {
        var p = _partidas.GetValueOrDefault(partidaId);
        if (p is null || p.Participantes.All(x => x.JogadorId != jogadorId))
        {
            throw new SimuladorException(HttpStatusCode.NotFound, "PARTIDA_INDISPONIVEL", "Partida não encontrada ou indisponível ao solicitante.");
        }

        return p;
    }

    private Jogador JogadorDaConta(Usuario u, Guid jogadorId)
    {
        if (!_jogadores.TryGetValue(jogadorId, out var j) || j.UsuarioId != u.Id)
        {
            throw new SimuladorException(HttpStatusCode.Forbidden, "SEM_PERMISSAO", "O perfil não pertence à conta.");
        }

        return j;
    }

    private Jogador ValidarAtivacao(Usuario u, Guid jogadorId, string? chave)
    {
        var j = JogadorDaConta(u, jogadorId);
        if (chave is null || j.HashChave is null || j.HashChave != Hash(chave))
        {
            throw new SimuladorException(HttpStatusCode.Forbidden, "SEM_PERMISSAO", "Chave de ativação ausente ou diferente da ativa.");
        }

        return j;
    }

    private static object VisaoJogador(Jogador j) => new { jogadorId = j.Id, nome = j.Nome, ativo = j.HashChave is not null };

    private static string Hash(string chave) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chave)));

    public sealed class Usuario(Guid id, string email, string token)
    {
        public Guid Id { get; } = id;
        public string Email { get; } = email;
        public string Token { get; } = token;
        public bool Revogado { get; set; }
    }

    private sealed class Jogador(Guid id, Guid usuarioId, string nome)
    {
        public Guid Id { get; } = id;
        public Guid UsuarioId { get; } = usuarioId;
        public string Nome { get; } = nome;
        public string? HashChave { get; set; }
    }

    private sealed class Participacao(Guid id, Guid jogadorId, int posicao)
    {
        public Guid Id { get; } = id;
        public Guid JogadorId { get; } = jogadorId;
        public int Posicao { get; } = posicao;
        public List<PecaSim> Mao { get; set; } = [];
        public int Pontos { get; set; }
    }

    private sealed record Evento(Guid Id, Guid JogadorId, PecaSim? Peca, int? Lado, bool Passou, int Pontos, DateTimeOffset Quando);

    private sealed class Partida(Guid id, int alvo)
    {
        public Guid Id { get; } = id;
        public int Alvo { get; } = alvo;
        public long Versao { get; set; }
        public int Situacao { get; set; }
        public int Rodada { get; set; }
        public int Turno { get; set; }
        public int PassesSeguidos { get; set; }
        public PecaSim? Abertura { get; set; }
        public Guid? Vencedor { get; set; }
        public List<Participacao> Participantes { get; } = [];
        public List<PecaSim> Mesa { get; } = [];
        public List<PecaSim> Cava { get; set; } = [];
        public List<Evento> Eventos { get; } = [];
        public Dictionary<(Guid, Guid), object> Idempotencia { get; } = [];
    }
}

/// <summary>Corpo genérico das ações (cobre entrar, iniciar, jogar, comprar e passar).</summary>
public sealed record Requisicao(Guid? JogadorId, long VersaoEsperada, Guid? ChaveIdempotencia, int? ValorA = null, int? ValorB = null, int? Lado = null);
