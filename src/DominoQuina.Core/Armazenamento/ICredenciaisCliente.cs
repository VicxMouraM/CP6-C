namespace DominoQuina.Core.Armazenamento;

/// <summary>
/// Guarda o que o cliente precisa lembrar entre execuções: endereço da API, token da conta,
/// chaves de ativação de cada perfil e partidas recentes. O token é reutilizável até revogação,
/// então não é pedido de novo a cada abertura do aplicativo.
/// </summary>
public interface ICredenciaisCliente
{
    /// <summary>Endereço base da API.</summary>
    string UrlBase { get; set; }

    /// <summary>Token Bearer da conta, ou <c>null</c> se ainda não autenticado.</summary>
    string? Token { get; set; }

    /// <summary>Último e-mail usado para solicitar token.</summary>
    string? Email { get; set; }

    /// <summary>Chave de ativação guardada para o perfil, se houver.</summary>
    string? ObterChaveAtivacao(Guid jogadorId);

    /// <summary>Guarda a chave de ativação do perfil (antes de enviá-la à API).</summary>
    void SalvarChaveAtivacao(Guid jogadorId, string chave);

    /// <summary>Esquece a chave de ativação do perfil (após liberar).</summary>
    void RemoverChaveAtivacao(Guid jogadorId);

    /// <summary>Perfis com chave guardada neste cliente.</summary>
    IReadOnlyCollection<Guid> PerfisComChave { get; }

    /// <summary>Registra uma partida em que o perfil participa, para retomar depois.</summary>
    void RegistrarPartida(Guid partidaId, Guid jogadorId);

    /// <summary>Partidas recentes deste cliente (mais recentes primeiro).</summary>
    IReadOnlyList<PartidaRecente> PartidasRecentes { get; }
}

/// <summary>Referência a uma partida que este cliente jogou ou está jogando.</summary>
/// <param name="PartidaId">Partida.</param>
/// <param name="JogadorId">Perfil usado na partida.</param>
/// <param name="RegistradaEm">Quando foi registrada.</param>
public sealed record PartidaRecente(Guid PartidaId, Guid JogadorId, DateTimeOffset RegistradaEm);
