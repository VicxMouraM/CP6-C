using DominoQuina.Core.Armazenamento;
using DominoQuina.Core.Regras;
using DominoQuina.Tests.Apoio;

namespace DominoQuina.Tests.Unidade;

public class CronometroECredenciaisTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 0)]
    [InlineData(10.5, 1)]
    [InlineData(30, 1)]
    [InlineData(31, 2)]
    [InlineData(59.9, 2)]
    [InlineData(60, 3)]
    [InlineData(300, 3)]
    public void Desconto_SegueATabelaDaRegraFutura(double segundos, int desconto) =>
        Assert.Equal(desconto, CronometroTurno.CalcularDesconto(TimeSpan.FromSeconds(segundos)));

    [Fact]
    public void Cronometro_ContaSoDuranteOMeuTurno()
    {
        var relogio = new RelogioFalso();
        var cronometro = new CronometroTurno(relogio);

        cronometro.Observar(ehMinhaVez: false, versaoEstado: 1);
        relogio.Avancar(TimeSpan.FromSeconds(50));
        Assert.Equal(TimeSpan.Zero, cronometro.Decorrido);

        cronometro.Observar(ehMinhaVez: true, versaoEstado: 2);
        relogio.Avancar(TimeSpan.FromSeconds(12));
        cronometro.Observar(ehMinhaVez: true, versaoEstado: 3); // comprou e continua: mesmo turno
        relogio.Avancar(TimeSpan.FromSeconds(3));

        Assert.Equal(TimeSpan.FromSeconds(15), cronometro.Decorrido);
        Assert.Equal(1, cronometro.DescontoAtual);

        cronometro.Observar(ehMinhaVez: false, versaoEstado: 4);
        Assert.False(cronometro.EmContagem);
    }

    [Fact]
    public void CredenciaisEmArquivo_PersistemEntreExecucoes()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "dq-teste-" + Guid.NewGuid());
        var arquivo = Path.Combine(pasta, "credenciais.json");
        var jogador = Guid.NewGuid();
        var partida = Guid.NewGuid();
        try
        {
            var primeira = new CredenciaisEmArquivo(arquivo)
            {
                UrlBase = "https://exemplo/",
                Token = "abc",
            };
            primeira.SalvarChaveAtivacao(jogador, new string('f', 64));
            primeira.RegistrarPartida(partida, jogador);

            var segunda = new CredenciaisEmArquivo(arquivo);

            Assert.Equal("https://exemplo", segunda.UrlBase);
            Assert.Equal("abc", segunda.Token);
            Assert.Equal(new string('f', 64), segunda.ObterChaveAtivacao(jogador));
            Assert.Equal(partida, Assert.Single(segunda.PartidasRecentes).PartidaId);
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(arquivo));
            }
        }
        finally
        {
            if (Directory.Exists(pasta))
            {
                Directory.Delete(pasta, recursive: true);
            }
        }
    }

    [Fact]
    public void Credenciais_SemUrl_UsamOEnderecoPadrao() =>
        Assert.Equal(CredenciaisEmMemoria.UrlPadrao, new CredenciaisEmMemoria().UrlBase);
}
