namespace DominoQuina.Core.Modelos;

/// <summary>Corpo de <c>POST /api/identidade/token</c>.</summary>
/// <param name="Email">E-mail institucional (@fiap.com.br).</param>
public sealed record SolicitarTokenRequest(string Email);

/// <summary>Corpo de <c>POST /api/jogadores</c>.</summary>
/// <param name="Nome">Nome do perfil (1 a 150 caracteres).</param>
public sealed record CriarJogadorRequest(string Nome);

/// <summary>Corpo de <c>POST /api/jogadores/{id}/ativar-cliente</c>.</summary>
/// <param name="ChaveAtivacao">Segredo do cliente: 32 bytes aleatórios em 64 caracteres hexadecimais.</param>
public sealed record AtivarJogadorRequest(string ChaveAtivacao);

/// <summary>Corpo de <c>POST /api/partidas</c>.</summary>
/// <param name="PontuacaoAlvo">Pontos necessários para vencer.</param>
/// <param name="JogadorId">Perfil que cria a partida.</param>
public sealed record CriarPartidaRequest(int PontuacaoAlvo, Guid JogadorId);

/// <summary>Corpo das ações sem pedra (<c>entrar</c>, <c>iniciar</c>, <c>comprar</c>, <c>passar</c>).</summary>
/// <param name="JogadorId">Perfil que executa a ação.</param>
/// <param name="VersaoEsperada">Versão do último estado conhecido.</param>
/// <param name="ChaveIdempotencia">UUID novo por intenção.</param>
public sealed record AcaoRequest(Guid JogadorId, long VersaoEsperada, Guid ChaveIdempotencia);

/// <summary>Corpo de <c>POST /api/partidas/{id}/jogar</c>.</summary>
/// <param name="JogadorId">Perfil que joga.</param>
/// <param name="VersaoEsperada">Versão do último estado conhecido.</param>
/// <param name="ChaveIdempotencia">UUID novo por intenção.</param>
/// <param name="ValorA">Primeiro valor da pedra.</param>
/// <param name="ValorB">Segundo valor da pedra.</param>
/// <param name="Lado">Ponta: 0 esquerda, 1 direita.</param>
public sealed record JogarRequest(
    Guid JogadorId,
    long VersaoEsperada,
    Guid ChaveIdempotencia,
    int ValorA,
    int ValorB,
    LadoJogavel Lado);
