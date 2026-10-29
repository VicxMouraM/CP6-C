namespace DominoQuina.Core.Regras;

/// <summary>
/// Mede o tempo de resposta do turno e calcula o desconto previsto na "regra futura"
/// (até 10s: 0; até 30s: 1; abaixo de 60s: 2; 60s ou mais: 3, desconto máximo).
/// A regra ainda não é aplicada pela API: o cliente apenas exibe o tempo e a penalidade prevista.
/// </summary>
public sealed class CronometroTurno
{
    private readonly TimeProvider _relogio;
    private long? _versaoDoTurno;
    private DateTimeOffset? _inicio;

    /// <summary>Cria o cronômetro.</summary>
    /// <param name="relogio">Fonte de tempo (substituível nos testes).</param>
    public CronometroTurno(TimeProvider? relogio = null) => _relogio = relogio ?? TimeProvider.System;

    /// <summary>Indica se há turno em contagem.</summary>
    public bool EmContagem => _inicio is not null;

    /// <summary>Tempo decorrido desde que o turno foi recebido.</summary>
    public TimeSpan Decorrido => _inicio is { } inicio ? _relogio.GetUtcNow() - inicio : TimeSpan.Zero;

    /// <summary>Desconto previsto para o tempo atual.</summary>
    public int DescontoAtual => CalcularDesconto(Decorrido);

    /// <summary>
    /// Informa o estado recebido. Começa a contar quando o turno passa a ser do jogador
    /// (nova versão com ação pendente) e para quando deixa de ser.
    /// </summary>
    public void Observar(bool ehMinhaVez, long versaoEstado)
    {
        if (!ehMinhaVez)
        {
            _inicio = null;
            _versaoDoTurno = null;
            return;
        }

        // Comprar e continuar jogando faz parte do mesmo turno: só reinicia se o turno havia acabado.
        if (_inicio is null)
        {
            _inicio = _relogio.GetUtcNow();
            _versaoDoTurno = versaoEstado;
        }
    }

    /// <summary>Versão do estado em que o turno começou.</summary>
    public long? VersaoDoTurno => _versaoDoTurno;

    /// <summary>Tabela de desconto por demora.</summary>
    public static int CalcularDesconto(TimeSpan tempo) => tempo.TotalSeconds switch
    {
        <= 10 => 0,
        <= 30 => 1,
        < 60 => 2,
        _ => 3,
    };
}
