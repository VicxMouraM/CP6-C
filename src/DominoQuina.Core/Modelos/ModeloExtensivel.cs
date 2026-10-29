using System.Text.Json;
using System.Text.Json.Serialization;

namespace DominoQuina.Core.Modelos;

/// <summary>
/// Base para modelos de resposta que guardam campos extras enviados pela API.
/// Assim o cliente continua funcionando se a API acrescentar campos, e consegue
/// ler valores cujo nome exato pode variar (por exemplo, a quantidade de pedras do adversário).
/// </summary>
public abstract class ModeloExtensivel
{
    /// <summary>Campos do JSON que não correspondem a nenhuma propriedade declarada.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? CamposExtras { get; set; }

    /// <summary>
    /// Procura um valor inteiro nos campos extras. Tenta primeiro os nomes exatos e,
    /// se nenhum existir, qualquer campo cujo nome contenha todos os fragmentos de <paramref name="fragmentos"/>.
    /// </summary>
    protected int? BuscarInteiro(string[] nomesExatos, params string[][] fragmentos)
    {
        var elemento = Buscar(nomesExatos, fragmentos, JsonValueKind.Number);
        return elemento is { } e && e.TryGetInt32(out var valor) ? valor : null;
    }

    /// <summary>Procura um texto nos campos extras (mesma lógica de <see cref="BuscarInteiro"/>).</summary>
    protected string? BuscarTexto(string[] nomesExatos, params string[][] fragmentos) =>
        Buscar(nomesExatos, fragmentos, JsonValueKind.String)?.GetString();

    /// <summary>Procura um booleano nos campos extras (mesma lógica de <see cref="BuscarInteiro"/>).</summary>
    protected bool? BuscarBooleano(string[] nomesExatos, params string[][] fragmentos)
    {
        var elemento = Buscar(nomesExatos, fragmentos, JsonValueKind.True)
                       ?? Buscar(nomesExatos, fragmentos, JsonValueKind.False);
        return elemento?.GetBoolean();
    }

    private JsonElement? Buscar(string[] nomesExatos, string[][] fragmentos, JsonValueKind tipo)
    {
        if (CamposExtras is null || CamposExtras.Count == 0)
        {
            return null;
        }

        foreach (var nome in nomesExatos)
        {
            foreach (var (chave, valor) in CamposExtras)
            {
                if (string.Equals(chave, nome, StringComparison.OrdinalIgnoreCase) && valor.ValueKind == tipo)
                {
                    return valor;
                }
            }
        }

        foreach (var grupo in fragmentos)
        {
            foreach (var (chave, valor) in CamposExtras)
            {
                if (valor.ValueKind == tipo &&
                    grupo.All(f => chave.Contains(f, StringComparison.OrdinalIgnoreCase)))
                {
                    return valor;
                }
            }
        }

        return null;
    }
}
