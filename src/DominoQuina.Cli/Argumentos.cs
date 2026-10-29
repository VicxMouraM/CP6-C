namespace DominoQuina.Cli;

/// <summary>Leitura simples dos argumentos: <c>comando posicionais... --opcao valor</c>.</summary>
internal sealed class Argumentos
{
    private readonly string[] _args;

    /// <summary>Cria o leitor.</summary>
    public Argumentos(string[] args) => _args = args;

    /// <summary>Primeiro argumento (comando), em minúsculas.</summary>
    public string Comando => _args.Length > 0 ? _args[0].ToLowerInvariant() : "ajuda";

    /// <summary>Argumentos após o comando que não são opções.</summary>
    public IReadOnlyList<string> Posicionais
    {
        get
        {
            var lista = new List<string>();
            for (var i = 1; i < _args.Length; i++)
            {
                if (_args[i].StartsWith("--", StringComparison.Ordinal))
                {
                    // pula o valor da opção, se houver
                    if (i + 1 < _args.Length && !_args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        i++;
                    }

                    continue;
                }

                lista.Add(_args[i]);
            }

            return lista;
        }
    }

    /// <summary>Posicional obrigatório.</summary>
    public string Posicional(int indice, string nome) =>
        Posicionais.Count > indice ? Posicionais[indice] : throw new ArgumentException($"Informe {nome}.");

    /// <summary>Indica se a opção foi passada.</summary>
    public bool Tem(string opcao) => _args.Contains(opcao, StringComparer.OrdinalIgnoreCase);

    /// <summary>Valor da opção (o argumento seguinte), se houver.</summary>
    public string? Valor(string opcao)
    {
        var i = Array.FindIndex(_args, a => string.Equals(a, opcao, StringComparison.OrdinalIgnoreCase));
        return i >= 0 && i + 1 < _args.Length && !_args[i + 1].StartsWith("--", StringComparison.Ordinal) ? _args[i + 1] : null;
    }
}
