using DominoQuina.Core.Api;
using DominoQuina.Core.Armazenamento;
using DominoQuina.Core.Modelos;

namespace DominoQuina.Core.Servicos;

/// <summary>
/// Fluxo de identidade e perfis: token, criação de perfis e ativação/liberação do perfil neste cliente.
/// </summary>
public sealed class GerenciadorPerfis
{
    private readonly IDominoApi _api;
    private readonly ICredenciaisCliente _credenciais;

    /// <summary>Cria o gerenciador.</summary>
    public GerenciadorPerfis(IDominoApi api, ICredenciaisCliente credenciais)
    {
        _api = api;
        _credenciais = credenciais;
    }

    /// <summary>Indica se há token guardado.</summary>
    public bool PossuiToken => !string.IsNullOrWhiteSpace(_credenciais.Token);

    /// <summary>Pede o token por e-mail (chega na caixa do e-mail FIAP).</summary>
    public async Task SolicitarTokenAsync(string email, CancellationToken ct = default)
    {
        await _api.SolicitarTokenAsync(email.Trim(), ct);
        _credenciais.Email = email.Trim();
    }

    /// <summary>Guarda o token recebido por e-mail e valida-o consultando a sessão.</summary>
    /// <exception cref="ApiException">401 se o token for inválido (o token não é guardado).</exception>
    public async Task<SessaoResponse> EntrarComTokenAsync(string token, CancellationToken ct = default)
    {
        var anterior = _credenciais.Token;
        _credenciais.Token = token.Trim();
        try
        {
            return await _api.ObterSessaoAsync(ct);
        }
        catch (ApiException ex) when (ex.NaoAutenticado)
        {
            _credenciais.Token = anterior;
            throw;
        }
    }

    /// <summary>Revoga o token na API e o remove deste cliente.</summary>
    public async Task SairAsync(bool revogarNaApi, CancellationToken ct = default)
    {
        if (revogarNaApi && PossuiToken)
        {
            try
            {
                await _api.RevogarTokenAsync(ct);
            }
            catch (ApiException ex) when (ex.NaoAutenticado)
            {
                // Já estava inválido: basta esquecer localmente.
            }
        }

        _credenciais.Token = null;
    }

    /// <summary>Lista os perfis da conta, indicando quais têm chave neste cliente.</summary>
    public async Task<IReadOnlyList<PerfilLocal>> ListarAsync(CancellationToken ct = default)
    {
        var perfis = await _api.ListarJogadoresAsync(ct);
        return perfis.Select(p => new PerfilLocal(p, _credenciais.ObterChaveAtivacao(p.JogadorId) is not null)).ToList();
    }

    /// <summary>Cria um perfil novo.</summary>
    public Task<PerfilJogador> CriarAsync(string nome, CancellationToken ct = default)
    {
        var limpo = nome.Trim();
        if (limpo.Length is < 1 or > 150)
        {
            throw new ArgumentException("O nome deve ter entre 1 e 150 caracteres.", nameof(nome));
        }

        return _api.CriarJogadorAsync(limpo, ct);
    }

    /// <summary>
    /// Ativa o perfil neste cliente. Reutiliza a chave já guardada (retomar após reabrir o app)
    /// ou gera uma nova, <b>guardando-a antes</b> de enviar: se a resposta se perder, repetir com a
    /// mesma chave é reconhecido pela API.
    /// </summary>
    /// <exception cref="ApiException">409 JOGADOR_JA_ATIVO se outro cliente reservou o perfil.</exception>
    public async Task<PerfilJogador> AtivarAsync(Guid jogadorId, CancellationToken ct = default)
    {
        var chave = _credenciais.ObterChaveAtivacao(jogadorId);
        var chaveNova = chave is null;
        if (chave is null)
        {
            chave = GeradorChaveAtivacao.Gerar();
            _credenciais.SalvarChaveAtivacao(jogadorId, chave);
        }

        try
        {
            return await _api.AtivarClienteAsync(jogadorId, chave, ct);
        }
        catch (ApiException ex) when (ex.JogadorJaAtivo || (ex.Conflito && chaveNova))
        {
            if (chaveNova)
            {
                _credenciais.RemoverChaveAtivacao(jogadorId);
            }

            throw;
        }
    }

    /// <summary>Libera o perfil para ser usado em outro cliente e esquece a chave local.</summary>
    public async Task<PerfilJogador> DesativarAsync(Guid jogadorId, CancellationToken ct = default)
    {
        var perfil = await _api.DesativarClienteAsync(jogadorId, ct);
        _credenciais.RemoverChaveAtivacao(jogadorId);
        return perfil;
    }

    /// <summary>Indica se o perfil tem chave neste cliente.</summary>
    public bool EstaAtivoAqui(Guid jogadorId) => _credenciais.ObterChaveAtivacao(jogadorId) is not null;
}

/// <summary>Perfil da conta com a informação de ativação local.</summary>
/// <param name="Perfil">Dados vindos da API.</param>
/// <param name="AtivoNesteCliente">Se há chave de ativação guardada aqui.</param>
public sealed record PerfilLocal(PerfilJogador Perfil, bool AtivoNesteCliente);
