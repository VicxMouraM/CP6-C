using System.Text.Json;
using System.Text.Json.Serialization;

namespace DominoQuina.Core.Api;

/// <summary>Opções de serialização compartilhadas com a API (camelCase e enums numéricos).</summary>
public static class OpcoesJson
{
    /// <summary>Instância única e imutável das opções.</summary>
    public static JsonSerializerOptions Padrao { get; } = Criar();

    private static JsonSerializerOptions Criar()
    {
        var opcoes = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        opcoes.Converters.Add(new ConversorEnumFlexivel());
        opcoes.MakeReadOnly(populateMissingResolver: true);
        return opcoes;
    }
}

/// <summary>
/// Converte enums lendo número (<c>2</c>) ou texto (<c>"jogar"</c>, <c>"Jogar"</c>) e escrevendo sempre número,
/// como exige o contrato. Isso protege o cliente se a API mudar a representação dos enums.
/// </summary>
public sealed class ConversorEnumFlexivel : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(Conversor<>).MakeGenericType(typeToConvert))!;

    private sealed class Conversor<T> : JsonConverter<T>
        where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numero))
            {
                return (T)Enum.ToObject(typeof(T), numero);
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var texto = reader.GetString();
                if (int.TryParse(texto, out var numeroTexto))
                {
                    return (T)Enum.ToObject(typeof(T), numeroTexto);
                }

                if (Enum.TryParse<T>(texto?.Replace("_", string.Empty, StringComparison.Ordinal), ignoreCase: true, out var valor))
                {
                    return valor;
                }
            }

            throw new JsonException($"Valor inválido para {typeof(T).Name}.");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture));
    }
}
