using System.Text;
using DominoQuina.Core.Modelos;
using DominoQuina.Core.Regras;
using DominoQuina.Core.Servicos;

namespace DominoQuina.Cli;

/// <summary>Desenha a mesa no terminal. Mostra somente a mão do próprio jogador.</summary>
internal static class RenderizadorConsole
{
    private const string Rosa = "\u001b[38;5;205m";
    private const string Cinza = "\u001b[90m";
    private const string Verde = "\u001b[32m";
    private const string Vermelho = "\u001b[31m";
    private const string Negrito = "\u001b[1m";
    private const string Reset = "\u001b[0m";

    /// <summary>Monta o texto da mesa para a sessão.</summary>
    public static string Desenhar(SessaoPartida sessao, IReadOnlyList<Jogada>? jogadasNumeradas = null)
    {
        var estado = sessao.Estado;
        var sb = new StringBuilder();
        sb.AppendLine($"{Rosa}{Negrito}══════════ DOMINÓ PONTA DE QUINA ══════════{Reset}");
        if (estado is null)
        {
            sb.AppendLine("Carregando estado...");
            return sb.ToString();
        }

        var eu = estado.Participantes?.FirstOrDefault(p => p.JogadorId == sessao.JogadorId);
        var adversario = estado.Participantes?.FirstOrDefault(p => p.JogadorId != sessao.JogadorId);

        sb.AppendLine($"{Cinza}Partida {estado.PartidaId} · versão {estado.VersaoEstado} · rodada {estado.NumeroRodada} · {Situacao(estado.Situacao)}{(estado.PontuacaoAlvo is { } alvo ? $" · alvo {alvo}" : string.Empty)}{Reset}");
        sb.AppendLine($"Placar: {Negrito}Você {eu?.PontosNoPlacar?.ToString() ?? "-"}{Reset}  x  {Negrito}{adversario?.NomeExibicao ?? "Aguardando adversário"} {adversario?.PontosNoPlacar?.ToString() ?? "-"}{Reset}");
        var pedrasAdv = adversario?.PedrasNaMao;
        sb.AppendLine($"Adversário: {(pedrasAdv is { } n ? string.Concat(Enumerable.Repeat("▮ ", n)) + $"({n})" : "?")}{(estado.PedrasNaCava is { } cava ? $"   Cava: {cava}" : string.Empty)}");
        sb.AppendLine();

        var mesa = MotorRegras.OrientarTabuleiro(estado.Tabuleiro);
        sb.AppendLine(mesa.Count == 0 ? $"{Cinza}(mesa vazia){Reset}" : "Mesa: " + string.Join(string.Empty, mesa.Select(p => p.ToString())));
        if (MotorRegras.CalcularPontas(estado.Tabuleiro) is { } pontas)
        {
            sb.AppendLine($"Pontas: {pontas.Esquerda} ◀ ▶ {pontas.Direita}  (soma {pontas.Soma}{(MotorRegras.PontosPorSoma(pontas.Soma) > 0 ? $" = {MotorRegras.PontosPorSoma(pontas.Soma)} pt" : string.Empty)})");
        }

        sb.AppendLine();
        sb.AppendLine($"Sua mão: {string.Join(" ", (estado.MinhaMao ?? []).Select(p => p.ToString()))}");
        if (estado.PedraAberturaObrigatoria is { } abertura)
        {
            sb.AppendLine($"{Rosa}Abertura obrigatória: {abertura}{Reset}");
        }

        var turno = estado.EhMinhaVez ? $"{Verde}{Negrito}SUA VEZ → {Acao(estado.ProximaAcao)}{Reset}" : $"{Cinza}{Acao(estado.ProximaAcao)}{Reset}";
        sb.AppendLine(turno);
        if (sessao.Cronometro.EmContagem)
        {
            sb.AppendLine($"Tempo do turno: {sessao.Cronometro.Decorrido.TotalSeconds:0}s · desconto previsto: {sessao.Cronometro.DescontoAtual} pt (regra futura)");
        }

        if (jogadasNumeradas is { Count: > 0 })
        {
            sb.AppendLine("Jogadas possíveis:");
            for (var i = 0; i < jogadasNumeradas.Count; i++)
            {
                var pontos = MotorRegras.PontosDaJogada(estado.Tabuleiro, jogadasNumeradas[i]);
                sb.AppendLine($"  {i + 1}) {jogadasNumeradas[i]}{(pontos > 0 ? $"  {Verde}+{pontos} pt{Reset}" : string.Empty)}");
            }
        }

        if (sessao.UltimaMensagem is { } msg)
        {
            sb.AppendLine((sessao.UltimaMensagemEhErro ? Vermelho : Verde) + msg + Reset);
        }

        if (estado.Situacao == SituacaoPartida.Encerrada)
        {
            sb.AppendLine(estado.VencedorId == sessao.JogadorId ? $"{Verde}{Negrito}🏆 Você venceu!{Reset}" : $"{Vermelho}{Negrito}Fim de partida: vitória do adversário.{Reset}");
        }

        return sb.ToString();
    }

    /// <summary>Texto da situação.</summary>
    public static string Situacao(SituacaoPartida s) => s switch
    {
        SituacaoPartida.AguardandoInicio => "aguardando início",
        SituacaoPartida.EmAndamento => "em andamento",
        _ => "encerrada",
    };

    /// <summary>Texto da próxima ação.</summary>
    public static string Acao(ProximaAcaoPartida a) => a switch
    {
        ProximaAcaoPartida.Aguardar => "aguardando o adversário",
        ProximaAcaoPartida.JogarAbertura => "jogar a abertura",
        ProximaAcaoPartida.Jogar => "jogar",
        ProximaAcaoPartida.Comprar => "comprar",
        ProximaAcaoPartida.Passar => "passar",
        _ => "partida encerrada",
    };
}
