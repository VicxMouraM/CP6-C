# Roteiro do vídeo de apresentação (≈ 8 a 10 minutos)

O critério "Apresentação na aula" (10%) passou a ser **vídeo de apresentação**. Prazo: **02/11/2026, 23:59**.
Grave a tela com áudio (OBS Studio, Xbox Game Bar `Win + Alt + R`, Teams ou Clipchamp), publique no YouTube como
**não listado** (ou no Google Drive com acesso "qualquer pessoa com o link") e cole o link no topo do `README.md`.

Cada integrante pode narrar uma parte. Ajuste os nomes.

| Tempo | Quem narra | O que aparece na tela |
|---|---|---|
| 0:00–0:45 | Integrante 1 | Apresentação do grupo e do objetivo: cliente próprio que joga pela API. A API decide distribuição, abertura, validade e pontos; o cliente mostra o estado confirmado e envia ações. |
| 0:45–2:15 | Integrante 2 | **Conexão e registro.** Tela Conta: solicitar token (202), colar e entrar. Tela Perfis: criar perfil e ativar (chave de 64 hex gerada e guardada antes do envio). Mostrar o **409 JOGADOR_JA_ATIVO** tentando ativar o mesmo perfil em outro cliente e o botão *Liberar*. |
| 2:15–4:45 | Integrante 3 | **Partida com estado na tela.** Criar mesa, o adversário entra (outro navegador ou o CLI), *Iniciar*. Mostrar: abertura obrigatória, mão própria, pedras do adversário viradas, placar, turno, relógio, escolha da ponta ◀/▶, comprar, pontuação nas pontas (soma múltipla de 5). Tentar uma ação proibida e mostrar a rejeição **422** sem a partida avançar. |
| 4:45–6:30 | Integrante 4 | **Jogador autônomo.** No terminal: `dotnet run -- autonomo --jogador <id> --entrar <partidaId>`. Mostrar o bot jogando sozinho até o fim e/ou ligar o interruptor *Autônomo* na mesa. Explicar a estratégia (pontos imediatos, risco para o adversário, descarte de pedras pesadas, flexibilidade) e o tempo de resposta < 1 s. Se possível, jogar contra o cliente de outro grupo. |
| 6:30–7:45 | Integrante 5 | **Arquitetura e diagrama de classes** (`docs/diagrama-classes.png`): Core × Web × CLI; `SessaoPartida` (só aplica versões novas, UUID por intenção, 409 usa o estado autoritativo, 422 não avança); `IDominoApi` e `IEstrategiaJogada`. |
| 7:45–9:00 | Integrante 1 | **Testes**: rodar `dotnet test` e mostrar o resumo (todos aprovados), destacando a partida completa ponta a ponta. Encerrar com a tela de resultado e o link do repositório. |

## Antes de gravar

- [ ] Token válido no site (a tela Conta mostra "Conectado").
- [ ] Dois perfis: um ativo no navegador e outro no CLI (use `$env:DOMINO_QUINA_HOME = "C:\temp\robo"` no terminal do robô).
- [ ] Se o seu perfil já tiver uma mesa esperando adversário, ela aparece como **Sua mesa** na sala: clique nela para retomar.
- [ ] `dotnet test` passando.
- [ ] **Não mostrar o token, a chave de ativação nem o arquivo `credenciais.json`** no vídeo. No Swagger, o bloco *Curl* exibe os dois: evite essa área ou desfoque na edição.
- [ ] Se a API oficial estiver fora do ar na hora da gravação, use o simulador (`dotnet run --project tools/DominoQuina.Simulador`) e diga isso no vídeo.

## Comandos úteis

```powershell
# Terminal 1 — site
dotnet run --project src/DominoQuina.Web

# Terminal 2 — adversário autônomo
cd src\DominoQuina.Cli
$env:DOMINO_QUINA_HOME = "C:\temp\robo"
dotnet run -- login <token>
dotnet run -- criar-perfil Robo
dotnet run -- perfis
dotnet run -- autonomo --jogador <id-do-robo> --entrar <partidaId>

# Testes
dotnet test
```
