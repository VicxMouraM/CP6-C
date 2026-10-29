# Diagrama de classes

Visão das classes principais do núcleo (`DominoQuina.Core`) e de quem as usa (console, web e testes).
O GitHub desenha o diagrama abaixo automaticamente. A mesma figura está em [`diagrama-classes.png`](diagrama-classes.png).

```mermaid
classDiagram
    direction LR

    %% ---------- API ----------
    class IDominoApi {
        <<interface>>
        +SolicitarTokenAsync(email)
        +ObterSessaoAsync() SessaoResponse
        +ListarJogadoresAsync() PerfilJogador[]
        +CriarJogadorAsync(nome) PerfilJogador
        +AtivarClienteAsync(jogadorId, chave) PerfilJogador
        +DesativarClienteAsync(jogadorId) PerfilJogador
        +ListarPartidasAsync() PartidaDisponivel[]
        +CriarPartidaAsync(jogadorId, alvo) EstadoPartidaParticipante
        +EntrarAsync(partidaId, AcaoRequest) ConfirmacaoAcaoPartida
        +IniciarAsync(partidaId, AcaoRequest) ConfirmacaoAcaoPartida
        +ObterEstadoAsync(partidaId, jogadorId) EstadoPartidaParticipante
        +ObterAtualizacaoAsync(partidaId, jogadorId, versao) EstadoPartidaParticipante?
        +JogarAsync(partidaId, JogarRequest) ConfirmacaoAcaoPartida
        +ComprarAsync(partidaId, AcaoRequest) ConfirmacaoAcaoPartida
        +PassarAsync(partidaId, AcaoRequest) ConfirmacaoAcaoPartida
        +ObterResultadoAsync(partidaId, jogadorId) ResultadoHistoricoPartida
    }
    class DominoApiClient {
        -HttpClient _http
        -ICredenciaisCliente _credenciais
        +CabecalhoAtivacao$ = "X-Jogador-Ativacao"
        -EnviarAsync() repete só requisições idempotentes
        -GarantirSucessoAsync() lança ApiException
    }
    class ApiException {
        +HttpStatusCode StatusCode
        +RespostaErro Erro
        +string Codigo
        +EstadoAutoritativo
        +bool NaoAutenticado
        +bool Conflito
        +bool RegraRejeitada
        +bool JogadorJaAtivo
    }
    IDominoApi <|.. DominoApiClient
    DominoApiClient ..> ApiException : lança

    %% ---------- Armazenamento ----------
    class ICredenciaisCliente {
        <<interface>>
        +string UrlBase
        +string Token
        +ObterChaveAtivacao(jogadorId)
        +SalvarChaveAtivacao(jogadorId, chave)
        +RemoverChaveAtivacao(jogadorId)
        +RegistrarPartida(partidaId, jogadorId)
    }
    class CredenciaisEmMemoria
    class CredenciaisEmArquivo {
        +string CaminhoArquivo
        #Persistir() arquivo 600
    }
    ICredenciaisCliente <|.. CredenciaisEmMemoria
    CredenciaisEmMemoria <|-- CredenciaisEmArquivo
    DominoApiClient --> ICredenciaisCliente : token e chave

    %% ---------- Serviços ----------
    class GerenciadorPerfis {
        +SolicitarTokenAsync(email)
        +EntrarComTokenAsync(token) SessaoResponse
        +ListarAsync() PerfilLocal[]
        +CriarAsync(nome)
        +AtivarAsync(jogadorId)
        +DesativarAsync(jogadorId)
    }
    class GeradorChaveAtivacao {
        <<static>>
        +Gerar()$ string 64 hex
    }
    GerenciadorPerfis --> IDominoApi
    GerenciadorPerfis --> ICredenciaisCliente
    GerenciadorPerfis ..> GeradorChaveAtivacao

    class SessaoPartida {
        +Guid PartidaId
        +Guid JogadorId
        +EstadoPartidaParticipante Estado
        +CronometroTurno Cronometro
        +AplicarEstado(estado) bool só versões novas
        +CarregarAsync()
        +AtualizarAsync() bool
        +IniciarAsync() ResultadoAcao
        +JogarAsync(Jogada) ResultadoAcao
        +ComprarAsync() ResultadoAcao
        +PassarAsync() ResultadoAcao
        +JogadasPossiveis() Jogada[]
    }
    class ResultadoAcao {
        +bool Sucesso
        +string Mensagem
        +ApiException Erro
    }
    SessaoPartida --> IDominoApi
    SessaoPartida *-- CronometroTurno
    SessaoPartida ..> ResultadoAcao

    class JogadorAutonomo {
        +ExecutarTurnoAsync() ResultadoAcao
        +JogarAteOFimAsync() EstadoPartidaParticipante
    }
    class DecisorAcao {
        <<static>>
        +Decidir(estado, estrategia)$ AcaoDecidida
    }
    class AcaoDecidida {
        +TipoAcao Tipo
        +Jogada Jogada
    }
    JogadorAutonomo --> SessaoPartida
    JogadorAutonomo --> IEstrategiaJogada
    JogadorAutonomo ..> DecisorAcao
    DecisorAcao ..> AcaoDecidida

    %% ---------- Estratégia e regras ----------
    class IEstrategiaJogada {
        <<interface>>
        +Escolher(estado) Jogada
    }
    class EstrategiaPontuadora {
        ~Avaliar(jogada, mao, mesa) double
        ~RiscoParaAdversario(pontas, mao, mesa) int
    }
    IEstrategiaJogada <|.. EstrategiaPontuadora
    EstrategiaPontuadora ..> MotorRegras

    class MotorRegras {
        <<static>>
        +OrientarTabuleiro(mesa)$ Peca[]
        +CalcularPontas(mesa)$ Pontas
        +JogadasPossiveis(mao, mesa, abertura)$ Jogada[]
        +AplicarJogada(pontas, jogada)$ Pontas
        +PontosPorSoma(soma)$ int
    }
    class CronometroTurno {
        +TimeSpan Decorrido
        +int DescontoAtual
        +Observar(ehMinhaVez, versao)
        +CalcularDesconto(tempo)$ int
    }
    class Pontas {
        +int Esquerda
        +int Direita
        +int Soma
    }
    class Jogada {
        +Peca Peca
        +LadoJogavel Lado
    }
    MotorRegras ..> Pontas
    MotorRegras ..> Jogada

    %% ---------- Modelos ----------
    class Peca {
        +int ValorA
        +int ValorB
        +bool EhDupla
        +MesmaPedra(outra) bool
    }
    class EstadoPartidaParticipante {
        +long VersaoEstado
        +SituacaoPartida Situacao
        +int NumeroRodada
        +List~Peca~ MinhaMao
        +List~Peca~ Tabuleiro
        +ProximaAcaoPartida ProximaAcao
        +Peca PedraAberturaObrigatoria
        +Guid VencedorId
        +bool EhMinhaVez
    }
    class ResumoParticipantePartida {
        +Guid JogadorId
        +string Nome
        +int PontosNoPlacar
        +int PedrasNaMao
    }
    class ModeloExtensivel {
        <<abstract>>
        +Dictionary CamposExtras
        #BuscarInteiro(nomes, fragmentos)
    }
    ModeloExtensivel <|-- EstadoPartidaParticipante
    ModeloExtensivel <|-- ResumoParticipantePartida
    EstadoPartidaParticipante "1" o-- "*" Peca
    EstadoPartidaParticipante "1" o-- "2" ResumoParticipantePartida
    Jogada --> Peca
    SessaoPartida --> EstadoPartidaParticipante

    class SituacaoPartida {
        <<enumeration>>
        AguardandoInicio
        EmAndamento
        Encerrada
    }
    class ProximaAcaoPartida {
        <<enumeration>>
        Aguardar
        JogarAbertura
        Jogar
        Comprar
        Passar
        Encerrada
    }
    class LadoJogavel {
        <<enumeration>>
        Esquerda = 0
        Direita = 1
    }
```

## Camadas

| Projeto | Papel |
|---|---|
| `DominoQuina.Core` | Modelos do contrato, cliente HTTP, regras locais (exibição e dicas), estratégia, sessão de partida e jogador autônomo. Não depende de interface. |
| `DominoQuina.Web` | Interface Blazor Server com aparência de jogo: conta, perfis, sala de partidas e mesa. Usa `SessaoPartida` e `JogadorAutonomo`. |
| `DominoQuina.Cli` | Linha de comando: autenticação, perfis, jogador autônomo e partida no terminal (útil para jogar contra outros grupos). |
| `DominoQuina.Simulador` | Simulador local da API (mesmas rotas e regras) usado pelos testes de integração e para ensaiar sem internet. |
| `DominoQuina.Tests` | Testes xUnit de unidade e de integração ponta a ponta. |

## Princípios do desenho

- **A API é a autoridade.** `MotorRegras` só serve para desenhar a mesa, destacar pedras jogáveis e sugerir jogadas; quem valida e pontua é o servidor.
- **Estado só avança.** `SessaoPartida.AplicarEstado` ignora versões mais antigas, então respostas atrasadas ou consultas paralelas não “voltam no tempo”.
- **Uma intenção, uma chave.** Cada ação gera um UUID novo; repetições por falha de rede reaproveitam o mesmo corpo (mesma chave), sem duplicar jogadas.
- **Rejeição não é sucesso.** 409 aplica o `estadoAutoritativo`; 422 mostra o motivo e mantém o estado; 401 pede nova autenticação.
- **Inversão de dependência.** Serviços dependem de `IDominoApi`, `ICredenciaisCliente` e `IEstrategiaJogada`, o que permite testes com dublês e a troca de estratégia.
