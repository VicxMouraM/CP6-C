# Roteiro da apresentação (≈ 10 minutos)

Sugestão de divisão para um grupo de até 5 integrantes. Ajuste os nomes.

| Tempo | Quem | O que mostrar |
|---|---|---|
| 0:00–1:00 | Integrante 1 | Objetivo: cliente próprio que joga pela API. A API decide tudo; o cliente mostra o estado confirmado e envia ações. |
| 1:00–2:30 | Integrante 2 | **Conta e perfis ao vivo**: pedir token, colar, criar perfil, ativar (chave de 64 hex guardada antes do envio). Mostrar o 409 `JOGADOR_JA_ATIVO` tentando ativar o mesmo perfil em outro cliente e depois *Liberar*. |
| 2:30–5:00 | Integrante 3 | **Partida manual**: criar mesa, segundo jogador entra (outro navegador ou o CLI), iniciar, abertura obrigatória, jogar escolhendo a ponta, comprar, pontuação nas pontas. Tentar uma jogada inválida e mostrar a rejeição 422 sem a partida avançar. |
| 5:00–7:00 | Integrante 4 | **Jogador autônomo**: ligar o interruptor *Autônomo* ou rodar `domino autonomo --entrar` contra outro grupo. Explicar a estratégia (pontos, risco, descarte, flexibilidade) e o tempo de resposta < 1 s. |
| 7:00–8:30 | Integrante 5 | **Arquitetura e diagrama de classes**: Core × Web × CLI; `SessaoPartida` (versão só avança, UUID por intenção, 409/422); `IDominoApi` e `IEstrategiaJogada`. |
| 8:30–10:00 | Integrante 1 | **Testes**: `dotnet test` (76 casos), destaque para a partida completa ponta a ponta contra o simulador. Perguntas. |

## Checklist antes da aula

- [ ] Token válido salvo (`domino sessao` mostra o usuário).
- [ ] Dois perfis criados e ativos (um no navegador, um no CLI ou em outro `DOMINO_QUINA_HOME`).
- [ ] `dotnet test` passando na máquina da apresentação.
- [ ] Plano B sem internet: simulador local (`dotnet run --project tools/DominoQuina.Simulador`).
- [ ] Liberar perfis ao final se for usá-los em outra máquina (fechar o app não libera).

## Comando para jogar contra outro grupo

```bash
cd src/DominoQuina.Cli
dotnet run -- partidas                                   # ver a mesa do outro grupo
dotnet run -- autonomo --jogador <nosso-perfil> --entrar <partidaId>
# ou criar e esperar que entrem:
dotnet run -- autonomo --jogador <nosso-perfil> --criar 50
```
