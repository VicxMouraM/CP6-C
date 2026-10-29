using System.Net;
using DominoQuina.Core.Modelos;

namespace DominoQuina.Core.Api;

/// <summary>
/// Rejeição da API. Carrega o status HTTP, o envelope <see cref="RespostaErro"/> (quando houver)
/// e uma explicação em português para mostrar ao jogador. Uma ação rejeitada nunca é tratada como sucesso.
/// </summary>
public sealed class ApiException : Exception
{
    /// <summary>Cria a exceção.</summary>
    public ApiException(HttpStatusCode statusCode, RespostaErro? erro, string? corpoBruto, TimeSpan? aguardar = null)
        : base(MontarMensagem(statusCode, erro, corpoBruto))
    {
        StatusCode = statusCode;
        Erro = erro;
        CorpoBruto = corpoBruto;
        AguardarAntesDeRepetir = aguardar;
    }

    /// <summary>Status HTTP devolvido.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Envelope de erro da API, se pôde ser lido.</summary>
    public RespostaErro? Erro { get; }

    /// <summary>Corpo bruto da resposta, útil para diagnóstico.</summary>
    public string? CorpoBruto { get; }

    /// <summary>Tempo indicado pela API (Retry-After) em respostas 429.</summary>
    public TimeSpan? AguardarAntesDeRepetir { get; }

    /// <summary>Código de negócio (ex.: <c>ENCAIXE_INVALIDO</c>).</summary>
    public string? Codigo => Erro?.CodigoEfetivo;

    /// <summary>Estado oficial enviado em um 409, quando presente.</summary>
    public EstadoPartidaParticipante? EstadoAutoritativo => Erro?.EstadoAutoritativo;

    /// <summary>401: token ausente, inválido ou revogado.</summary>
    public bool NaoAutenticado => StatusCode == HttpStatusCode.Unauthorized;

    /// <summary>409: conflito de versão ou perfil ativo em outro cliente.</summary>
    public bool Conflito => StatusCode == HttpStatusCode.Conflict;

    /// <summary>422: regra do jogo rejeitada; a partida não avançou.</summary>
    public bool RegraRejeitada => StatusCode == HttpStatusCode.UnprocessableEntity;

    /// <summary>409 com código JOGADOR_JA_ATIVO.</summary>
    public bool JogadorJaAtivo => Conflito && string.Equals(Codigo, "JOGADOR_JA_ATIVO", StringComparison.OrdinalIgnoreCase);

    /// <summary>Explicação amigável para cada status, conforme a tabela do enunciado.</summary>
    public static string ExplicarStatus(HttpStatusCode status) => status switch
    {
        HttpStatusCode.BadRequest => "Dados inválidos. Revise os campos enviados.",
        HttpStatusCode.Unauthorized => "Token ausente, inválido ou revogado. Solicite nova autenticação.",
        HttpStatusCode.Forbidden => "O usuário ou o perfil não tem permissão para esta ação.",
        HttpStatusCode.NotFound => "Partida não encontrada ou indisponível.",
        HttpStatusCode.Conflict => "Conflito: o estado mudou ou o perfil está ativo em outro cliente.",
        HttpStatusCode.UnprocessableEntity => "Jogada rejeitada pela regra. A partida não avançou.",
        HttpStatusCode.TooManyRequests => "Limite de solicitações atingido. Aguarde a janela indicada.",
        HttpStatusCode.InternalServerError => "Erro interno da API. Tente novamente.",
        _ => $"A API respondeu {(int)status}.",
    };

    /// <summary>Tradução dos códigos de negócio conhecidos.</summary>
    public static string? ExplicarCodigo(string? codigo) => codigo?.ToUpperInvariant() switch
    {
        "ESTADO_INVALIDO" => "A partida não está em um estado que permita essa ação.",
        "FORA_DO_TURNO" => "Não é a sua vez.",
        "PECA_AUSENTE" => "Essa pedra não está na sua mão.",
        "ENCAIXE_INVALIDO" => "A pedra não encaixa nessa ponta.",
        "PASSAGEM_NAO_PERMITIDA" => "Passar não é permitido: há cava ou pedra compatível.",
        "JOGADOR_JA_ATIVO" => "Este perfil já está ativo em outro cliente. Libere-o lá antes.",
        "CONFLITO_DE_ESTADO" => "Outra ação chegou antes. O estado foi atualizado.",
        "PARTIDA_INDISPONIVEL" => "Partida não encontrada ou sem vaga.",
        "LIMITE_EXCEDIDO" => "Muitas solicitações de token. Aguarde alguns minutos.",
        "NAO_AUTENTICADO" => "Token ausente, inválido ou revogado.",
        "SEM_PERMISSAO" => "Sem permissão para esta operação.",
        "ENTRADA_INVALIDA" => "Campos inválidos na requisição.",
        _ => null,
    };

    private static string MontarMensagem(HttpStatusCode status, RespostaErro? erro, string? corpo)
    {
        var codigo = erro?.CodigoEfetivo;
        var explicacao = ExplicarCodigo(codigo) ?? ExplicarStatus(status);
        var detalhe = erro?.MensagemEfetiva;
        var texto = $"{(int)status}{(codigo is null ? string.Empty : " " + codigo)}: {explicacao}";
        if (!string.IsNullOrWhiteSpace(detalhe) && !string.Equals(detalhe, explicacao, StringComparison.Ordinal))
        {
            texto += $" ({detalhe})";
        }
        else if (erro is null && !string.IsNullOrWhiteSpace(corpo) && corpo.Length < 200)
        {
            texto += $" ({corpo.Trim()})";
        }

        return texto;
    }
}
