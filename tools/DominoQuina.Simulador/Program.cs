using DominoQuina.Simulador;

// Simulador local: dotnet run --project tools/DominoQuina.Simulador [-- http://localhost:5199]
var url = args.FirstOrDefault(a => a.StartsWith("http", StringComparison.OrdinalIgnoreCase)) ?? "http://localhost:5199";
var app = ServidorSimulador.Construir(url, new MotorSimulador());
Console.WriteLine($"Simulador da API em {url}  (token: GET {url}/simulador/token?email=voce@fiap.com.br)");
await app.RunAsync();
