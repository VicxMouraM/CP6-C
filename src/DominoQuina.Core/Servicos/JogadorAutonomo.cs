using DominoQuina.Core.Api;
using DominoQuina.Core.Estrategia;
using DominoQuina.Core.Modelos;

namespace DominoQuina.Core.Servicos;

/// <summary>Configuração do jogador autônomo.</summary>
public sealed class OpcoesJogadorAutonomo
{
    /// <summary>Intervalo entre consultas a <c>/atualizacoes</c> enquanto aguarda.</summary>
    public TimeSpan IntervaloConsulta { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Pausa antes de agir (deixa a partida visível para quem assiste). Mantida bem abaixo de 10 s.</summary>
    public TimeSpan PausaAntesDeAgir { get; set; } = TimeSpan.FromMilliseconds(400);

    /// <summary>Inicia a partida assim que houver dois participantes.</summary>
    public bool IniciarAutomaticamente { get; set; } = true;

    /// <summary>Limite de rejeições seguidas antes de desistir (proteção contra laço infinito).</summary>
    public int MaximoFalhasSeguidas { get; set; } = 25;
}

/// <summary>
/// Joga sozinho uma partida completa: consulta atualizações, decide com a estratégia e envia a ação.
/// Se uma jogada for rejeitada (422), recarrega o estado e tenta a alternativa (comprar, depois passar).
/// </summary>
public sealed class JogadorAutonomo
{
    private readonly SessaoPartida _sessao;
    private readonly IEstrategiaJogada _estrategia;
    private readonly OpcoesJogadorAutonomo _opcoes;

    /// <summary>Cria o jogador.</summary>
    public JogadorAutonomo(SessaoPartida sessao, IEstrategiaJogada? estrategia = null, OpcoesJogadorAutonomo? opcoes = null)
    {
        _sessao = sessao;
        _estrategia = estrategia ?? new EstrategiaPontuadora();
        _opcoes = opcoes ?? new OpcoesJogadorAutonomo();
    }

    /// <summary>Registro do que o jogador fez (para console ou tela).</summary>
    public event Action<string>? Registro;

    /// <summary>
    /// Executa uma decisão com o estado atual, se houver algo a fazer.
    /// </summary>
    /// <returns>O resultado da ação, ou <c>null</c> se era para aguardar.</returns>
    public async Task<ResultadoAcao?> ExecutarTurnoAsync(CancellationToken ct = default)
    {
        if (_sessao.Estado is not { } estado)
        {
            return null;
        }

        var decisao = DecisorAcao.Decidir(estado, _estrategia, _opcoes.IniciarAutomaticamente);
        if (decisao.Tipo is TipoAcao.Aguardar or TipoAcao.Encerrar)
        {
            return null;
        }

        if (_opcoes.PausaAntesDeAgir > TimeSpan.Zero)
        {
            await Task.Delay(_opcoes.PausaAntesDeAgir, ct);
        }

        var resultado = await ExecutarDecisaoAsync(decisao, ct);
        Registrar(resultado.Sucesso ? $"✔ {resultado.Mensagem}" : $"✖ {resultado.Mensagem}");

        // Jogada recusada pela regra: tenta comprar; compra recusada: tenta passar.
        if (resultado.RegraRejeitada && decisao.Tipo == TipoAcao.Jogar && _sessao.Estado?.EhMinhaVez == true)
        {
            resultado = await _sessao.ComprarAsync(ct);
            Registrar(resultado.Sucesso ? $"✔ {resultado.Mensagem}" : $"✖ {resultado.Mensagem}");
        }

        if (resultado.RegraRejeitada && decisao.Tipo is TipoAcao.Jogar or TipoAcao.Comprar && _sessao.Estado?.EhMinhaVez == true)
        {
            resultado = await _sessao.PassarAsync(ct);
            Registrar(resultado.Sucesso ? $"✔ {resultado.Mensagem}" : $"✖ {resultado.Mensagem}");
        }

        return resultado;
    }

    /// <summary>
    /// Joga até a partida terminar (ou o cancelamento). Retorna o último estado conhecido.
    /// </summary>
    public async Task<EstadoPartidaParticipante?> JogarAteOFimAsync(CancellationToken ct = default)
    {
        var falhasSeguidas = 0;
        if (_sessao.Estado is null)
        {
            await _sessao.CarregarAsync(ct);
        }

        while (!ct.IsCancellationRequested)
        {
            var estado = _sessao.Estado;
            if (estado is not null && DecisorAcao.Decidir(estado, _estrategia).Tipo == TipoAcao.Encerrar)
            {
                Registrar("Partida encerrada.");
                return estado;
            }

            ResultadoAcao? resultado = null;
            try
            {
                resultado = await ExecutarTurnoAsync(ct);
                if (resultado is null or { Sucesso: false })
                {
                    await Task.Delay(_opcoes.IntervaloConsulta, ct);
                    await _sessao.AtualizarAsync(ct);
                }
            }
            catch (ApiException ex) when (!ex.NaoAutenticado)
            {
                Registrar($"✖ {ex.Message}");
                resultado = new ResultadoAcao(false, ex.Message, ex);
                await Task.Delay(_opcoes.IntervaloConsulta, ct);
            }
            catch (HttpRequestException ex)
            {
                Registrar($"… conexão instável ({ex.Message}); tentando de novo.");
                await Task.Delay(_opcoes.IntervaloConsulta * 2, ct);
            }

            falhasSeguidas = resultado is { Sucesso: false } ? falhasSeguidas + 1 : 0;
            if (falhasSeguidas >= _opcoes.MaximoFalhasSeguidas)
            {
                Registrar("Muitas rejeições seguidas; o jogador autônomo parou.");
                return _sessao.Estado;
            }
        }

        return _sessao.Estado;
    }

    private Task<ResultadoAcao> ExecutarDecisaoAsync(AcaoDecidida decisao, CancellationToken ct) => decisao.Tipo switch
    {
        TipoAcao.Iniciar => _sessao.IniciarAsync(ct),
        TipoAcao.Jogar => _sessao.JogarAsync(decisao.Jogada!, ct),
        TipoAcao.Comprar => _sessao.ComprarAsync(ct),
        TipoAcao.Passar => _sessao.PassarAsync(ct),
        _ => Task.FromResult(new ResultadoAcao(true, "Nada a fazer.")),
    };

    private void Registrar(string mensagem) => Registro?.Invoke(mensagem);
}
