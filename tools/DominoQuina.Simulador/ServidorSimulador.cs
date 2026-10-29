using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace DominoQuina.Simulador;

/// <summary>Expõe o <see cref="MotorSimulador"/> com as mesmas rotas da API oficial.</summary>
public static class ServidorSimulador
{
    private const string Ativacao = "X-Jogador-Ativacao";

    /// <summary>Monta o servidor HTTP do simulador no endereço indicado.</summary>
    public static WebApplication Construir(string url, MotorSimulador motor, bool registrarTokensNoConsole = true)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(url);
        builder.Logging.ClearProviders();
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
        var app = builder.Build();

        app.Use(async (ctx, next) =>
        {
            try
            {
                await next();
            }
            catch (SimuladorException erro)
            {
                ctx.Response.StatusCode = (int)erro.Status;
                await ctx.Response.WriteAsJsonAsync(new { codigo = erro.Codigo, mensagem = erro.Message, estadoAutoritativo = erro.EstadoAutoritativo });
            }
        });

        var api = app.MapGroup("/api");

        api.MapPost("/identidade/token", (SolicitarToken corpo) =>
        {
            if (!MotorSimulador.EmailValido(corpo.Email))
            {
                return Results.BadRequest(false);
            }

            var token = motor.EmitirToken(corpo.Email!);
            if (registrarTokensNoConsole)
            {
                Console.WriteLine($"[simulador] token de {corpo.Email}: {token}");
            }

            return Results.Accepted();
        });

        api.MapGet("/identidade/sessao", (HttpRequest r) =>
        {
            var u = motor.Autenticar(r.Headers.Authorization);
            return Results.Ok(new { usuarioId = u.Id, email = u.Email });
        });

        api.MapPost("/identidade/tokens/revogacao", (HttpRequest r) =>
        {
            motor.Revogar(motor.Autenticar(r.Headers.Authorization));
            return Results.NoContent();
        });

        api.MapGet("/jogadores", (HttpRequest r) => Results.Ok(motor.ListarJogadores(motor.Autenticar(r.Headers.Authorization))));
        api.MapPost("/jogadores", (HttpRequest r, CriarJogador corpo) => Results.Ok(motor.CriarJogador(motor.Autenticar(r.Headers.Authorization), corpo.Nome)));
        api.MapPost("/jogadores/{id:guid}/ativar-cliente", (HttpRequest r, Guid id, AtivarJogador corpo) =>
            Results.Ok(motor.Ativar(motor.Autenticar(r.Headers.Authorization), id, corpo.ChaveAtivacao)));
        api.MapDelete("/jogadores/{id:guid}/desativar-cliente", (HttpRequest r, Guid id) =>
            Results.Ok(motor.Desativar(motor.Autenticar(r.Headers.Authorization), id, r.Headers[Ativacao])));

        api.MapGet("/partidas", (HttpRequest r) =>
        {
            motor.Autenticar(r.Headers.Authorization);
            return Results.Ok(motor.ListarPartidas());
        });
        api.MapPost("/partidas", (HttpRequest r, CriarPartida corpo) =>
            Results.Ok(motor.CriarPartida(motor.Autenticar(r.Headers.Authorization), corpo.JogadorId, r.Headers[Ativacao], corpo.PontuacaoAlvo)));

        api.MapPost("/partidas/{id:guid}/entrar", (HttpRequest r, Guid id, Requisicao corpo) => Results.Ok(motor.Entrar(motor.Autenticar(r.Headers.Authorization), id, corpo, r.Headers[Ativacao])));
        api.MapPost("/partidas/{id:guid}/iniciar", (HttpRequest r, Guid id, Requisicao corpo) => Results.Ok(motor.Iniciar(motor.Autenticar(r.Headers.Authorization), id, corpo, r.Headers[Ativacao])));
        api.MapPost("/partidas/{id:guid}/jogar", (HttpRequest r, Guid id, Requisicao corpo) => Results.Ok(motor.Jogar(motor.Autenticar(r.Headers.Authorization), id, corpo, r.Headers[Ativacao])));
        api.MapPost("/partidas/{id:guid}/comprar", (HttpRequest r, Guid id, Requisicao corpo) => Results.Ok(motor.Comprar(motor.Autenticar(r.Headers.Authorization), id, corpo, r.Headers[Ativacao])));
        api.MapPost("/partidas/{id:guid}/passar", (HttpRequest r, Guid id, Requisicao corpo) => Results.Ok(motor.Passar(motor.Autenticar(r.Headers.Authorization), id, corpo, r.Headers[Ativacao])));

        api.MapGet("/partidas/{id:guid}", (HttpRequest r, Guid id, [FromQuery] Guid jogadorId) =>
            Results.Ok(motor.Estado(motor.Autenticar(r.Headers.Authorization), id, jogadorId, r.Headers[Ativacao])));
        api.MapGet("/partidas/{id:guid}/atualizacoes", (HttpRequest r, Guid id, [FromQuery] Guid jogadorId, [FromQuery] long? versaoConhecida) =>
        {
            var (mudou, estado) = motor.Atualizacoes(motor.Autenticar(r.Headers.Authorization), id, jogadorId, versaoConhecida, r.Headers[Ativacao]);
            return mudou ? Results.Ok(estado) : Results.NoContent();
        });
        api.MapGet("/partidas/{id:guid}/visualizar", (HttpRequest r, Guid id) =>
        {
            motor.Autenticar(r.Headers.Authorization);
            return Results.Ok(motor.Observar(id));
        });
        api.MapGet("/partidas/{id:guid}/resultado", (HttpRequest r, Guid id, [FromQuery] Guid jogadorId) =>
            Results.Ok(motor.Resultado(motor.Autenticar(r.Headers.Authorization), id, jogadorId, r.Headers[Ativacao])));

        // Exclusivo do simulador: obter o token sem e-mail (para ensaios locais).
        app.MapGet("/simulador/token", (string email) =>
            MotorSimulador.EmailValido(email) ? Results.Text(motor.EmitirToken(email)) : Results.BadRequest(false));

        return app;
    }

    private sealed record SolicitarToken(string? Email);

    private sealed record CriarJogador(string? Nome);

    private sealed record AtivarJogador(string? ChaveAtivacao);

    private sealed record CriarPartida(int PontuacaoAlvo, Guid JogadorId);
}
