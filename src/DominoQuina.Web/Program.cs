using DominoQuina.Core.Api;
using DominoQuina.Core.Armazenamento;
using DominoQuina.Core.Estrategia;
using DominoQuina.Core.Servicos;
using DominoQuina.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Credenciais (token e chaves de ativação) persistidas no perfil do usuário do sistema operacional.
builder.Services.AddSingleton<ICredenciaisCliente>(_ => new CredenciaisEmArquivo());
builder.Services.AddHttpClient<IDominoApi, DominoApiClient>(http => http.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddTransient<GerenciadorPerfis>();
builder.Services.AddSingleton<IEstrategiaJogada, EstrategiaPontuadora>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/erro", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
