namespace DominoQuina.Core.Armazenamento;

/// <summary>Implementação em memória (testes e execuções descartáveis).</summary>
public class CredenciaisEmMemoria : ICredenciaisCliente
{
    /// <summary>Endereço usado quando nada foi configurado.</summary>
    public const string UrlPadrao = "https://domino-quina-api-b1-pf2261.azurewebsites.net";

    /// <summary>Trava para acesso concorrente (UI e jogador autônomo).</summary>
    protected object Trava { get; } = new();

    /// <summary>Dados mantidos.</summary>
    protected DadosCredenciais Dados { get; set; } = new();

    /// <inheritdoc />
    public string UrlBase
    {
        get { lock (Trava) { return string.IsNullOrWhiteSpace(Dados.UrlBase) ? UrlPadrao : Dados.UrlBase; } }
        set { lock (Trava) { Dados.UrlBase = value?.Trim().TrimEnd('/'); } Persistir(); }
    }

    /// <inheritdoc />
    public string? Token
    {
        get { lock (Trava) { return Dados.Token; } }
        set { lock (Trava) { Dados.Token = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); } Persistir(); }
    }

    /// <inheritdoc />
    public string? Email
    {
        get { lock (Trava) { return Dados.Email; } }
        set { lock (Trava) { Dados.Email = value?.Trim(); } Persistir(); }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Guid> PerfisComChave
    {
        get { lock (Trava) { return Dados.ChavesAtivacao.Keys.ToList(); } }
    }

    /// <inheritdoc />
    public IReadOnlyList<PartidaRecente> PartidasRecentes
    {
        get { lock (Trava) { return Dados.PartidasRecentes.OrderByDescending(p => p.RegistradaEm).ToList(); } }
    }

    /// <inheritdoc />
    public string? ObterChaveAtivacao(Guid jogadorId)
    {
        lock (Trava)
        {
            return Dados.ChavesAtivacao.TryGetValue(jogadorId, out var chave) ? chave : null;
        }
    }

    /// <inheritdoc />
    public void SalvarChaveAtivacao(Guid jogadorId, string chave)
    {
        lock (Trava) { Dados.ChavesAtivacao[jogadorId] = chave; }
        Persistir();
    }

    /// <inheritdoc />
    public void RemoverChaveAtivacao(Guid jogadorId)
    {
        lock (Trava) { Dados.ChavesAtivacao.Remove(jogadorId); }
        Persistir();
    }

    /// <inheritdoc />
    public void RegistrarPartida(Guid partidaId, Guid jogadorId)
    {
        lock (Trava)
        {
            Dados.PartidasRecentes.RemoveAll(p => p.PartidaId == partidaId && p.JogadorId == jogadorId);
            Dados.PartidasRecentes.Add(new PartidaRecente(partidaId, jogadorId, DateTimeOffset.UtcNow));
            if (Dados.PartidasRecentes.Count > 30)
            {
                Dados.PartidasRecentes = Dados.PartidasRecentes.OrderByDescending(p => p.RegistradaEm).Take(30).ToList();
            }
        }

        Persistir();
    }

    /// <summary>Gravação durável; nada a fazer em memória.</summary>
    protected virtual void Persistir()
    {
    }
}

/// <summary>Formato serializado das credenciais.</summary>
public sealed class DadosCredenciais
{
    /// <summary>Endereço base da API.</summary>
    public string? UrlBase { get; set; }

    /// <summary>Token Bearer.</summary>
    public string? Token { get; set; }

    /// <summary>E-mail da conta.</summary>
    public string? Email { get; set; }

    /// <summary>Chave de ativação por perfil.</summary>
    public Dictionary<Guid, string> ChavesAtivacao { get; set; } = [];

    /// <summary>Partidas recentes.</summary>
    public List<PartidaRecente> PartidasRecentes { get; set; } = [];
}
