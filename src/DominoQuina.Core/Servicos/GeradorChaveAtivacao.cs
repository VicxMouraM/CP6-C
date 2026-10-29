using System.Security.Cryptography;

namespace DominoQuina.Core.Servicos;

/// <summary>Gera a chave de ativação do cliente: 32 bytes criptograficamente aleatórios em 64 caracteres hexadecimais.</summary>
public static class GeradorChaveAtivacao
{
    /// <summary>Tamanho da chave em bytes.</summary>
    public const int TamanhoBytes = 32;

    /// <summary>Gera uma nova chave (hexadecimal minúsculo, 64 caracteres).</summary>
    public static string Gerar() => Convert.ToHexString(RandomNumberGenerator.GetBytes(TamanhoBytes)).ToLowerInvariant();

    /// <summary>Valida o formato exigido pela API.</summary>
    public static bool EhValida(string? chave) =>
        chave is { Length: TamanhoBytes * 2 } && chave.All(Uri.IsHexDigit);
}
