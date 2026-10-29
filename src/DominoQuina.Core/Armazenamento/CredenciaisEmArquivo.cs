using System.Text.Json;

namespace DominoQuina.Core.Armazenamento;

/// <summary>
/// Persiste as credenciais em <c>~/.domino-quina/credenciais.json</c> (ou na pasta de
/// <c>DOMINO_QUINA_HOME</c>). Em Linux/macOS o arquivo recebe permissão 600 (somente o dono lê).
/// A API guarda apenas o hash da chave de ativação; a chave original só existe aqui.
/// </summary>
public sealed class CredenciaisEmArquivo : CredenciaisEmMemoria
{
    private static readonly JsonSerializerOptions s_opcoes = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>Carrega (ou cria) o arquivo no caminho indicado.</summary>
    /// <param name="caminhoArquivo">Arquivo de credenciais; <c>null</c> usa o caminho padrão.</param>
    public CredenciaisEmArquivo(string? caminhoArquivo = null)
    {
        CaminhoArquivo = caminhoArquivo ?? CaminhoPadrao();
        Carregar();
    }

    /// <summary>Arquivo usado.</summary>
    public string CaminhoArquivo { get; }

    /// <summary>Caminho padrão: <c>$DOMINO_QUINA_HOME/credenciais.json</c> ou <c>~/.domino-quina/credenciais.json</c>.</summary>
    public static string CaminhoPadrao()
    {
        var pasta = Environment.GetEnvironmentVariable("DOMINO_QUINA_HOME");
        if (string.IsNullOrWhiteSpace(pasta))
        {
            pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".domino-quina");
        }

        return Path.Combine(pasta, "credenciais.json");
    }

    /// <inheritdoc />
    protected override void Persistir()
    {
        lock (Trava)
        {
            var json = JsonSerializer.Serialize(Dados, s_opcoes);
            var pasta = Path.GetDirectoryName(CaminhoArquivo)!;
            Directory.CreateDirectory(pasta);
            var temporario = CaminhoArquivo + ".tmp";
            File.WriteAllText(temporario, json);
            RestringirPermissoes(temporario);
            File.Move(temporario, CaminhoArquivo, overwrite: true);
        }
    }

    private void Carregar()
    {
        if (!File.Exists(CaminhoArquivo))
        {
            return;
        }

        try
        {
            var dados = JsonSerializer.Deserialize<DadosCredenciais>(File.ReadAllText(CaminhoArquivo), s_opcoes);
            if (dados is not null)
            {
                Dados = dados;
            }
        }
        catch (JsonException)
        {
            // Arquivo corrompido: guarda uma cópia e recomeça, sem perder o original.
            File.Copy(CaminhoArquivo, CaminhoArquivo + ".corrompido", overwrite: true);
        }
    }

    private static void RestringirPermissoes(string caminho)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(caminho, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
