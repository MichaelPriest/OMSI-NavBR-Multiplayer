# Manual de atalhos de teclado - OMSI NavBR Multiplayer

> Estado documentado: PR #40 `fix/roadvehicle-frame-ownership`.
> Este manual cobre os atalhos implementados no cliente NavBR atual. Atalhos do próprio OMSI 2 não são redefinidos aqui.

## 1. Resumo rápido

| Atalho | Função | Contexto |
|---|---|---|
| `F9` | Abrir chat | Padrão; configurável |
| `F10` | Push-to-talk (PTT) | Padrão; configurável |
| `Enter` | Enviar mensagem do chat | Chat aberto |
| `Esc` | Fechar chat sem enviar | Chat aberto |
| `K` | Abrir/fechar configuração NavBR TP/TS | OMSI em gameplay |
| `Ctrl+Alt+H` | Mostrar/ocultar HUD completo | OMSI em gameplay |
| `Ctrl+Alt+F6` | Mostrar/ocultar widget NavBR TP/TS | OMSI em gameplay |
| `Ctrl+Alt+F7` | Alternar tema do NavBR TP/TS | OMSI em gameplay |
| `Ctrl+Alt+F8` | Alternar tamanho do NavBR TP/TS | OMSI em gameplay |
| `W` | Caminhar para frente | Personagem/RP ativo |
| `S` | Caminhar para trás | Personagem/RP ativo |
| `A` | Girar para a esquerda | Personagem/RP ativo |
| `D` | Girar para a direita | Personagem/RP ativo |
| `Shift` | Correr | Personagem/RP ativo + movimento |
| `E` | Voltar/entrar no ônibus | Personagem/RP ativo e próximo do ônibus |
| `Esc` | Retorno de emergência ao ônibus | Personagem/RP ativo |

## 2. Chat

### Atalho padrão

`F9`

O atalho abre o campo de chat do HUD quando:

- o OMSI é a janela de gameplay em primeiro plano;
- o HUD está ativo;
- a combinação não conflita com o `Inputs\keyboard.cfg` do OMSI;
- Chat e PTT não usam a mesma combinação.

### Dentro do chat

- `Enter` - envia a mensagem e fecha o campo;
- `Esc` - fecha o campo sem enviar;
- enquanto o chat está aberto, o PTT é interrompido;
- ao fechar o chat, o foco retorna ao OMSI.

## 3. Push-to-talk / voz

### Atalho padrão

`F10`

Funcionamento:

- pressionar e manter - transmite voz;
- soltar - encerra imediatamente a transmissão;
- não funciona se a combinação estiver em conflito com o OMSI;
- não funciona enquanto o campo de chat está aberto.

## 4. Combinações configuráveis para Chat e PTT

As teclas base disponíveis são:

- `F1`
- `F2`
- `F3`
- `F4`
- `F9`
- `F10`
- `F11`
- `F12`

Para cada uma delas, o NavBR aceita quatro formas:

| Sem modificador | Shift | Ctrl | Ctrl+Shift |
|---|---|---|---|
| F1 | Shift+F1 | Ctrl+F1 | Ctrl+Shift+F1 |
| F2 | Shift+F2 | Ctrl+F2 | Ctrl+Shift+F2 |
| F3 | Shift+F3 | Ctrl+F3 | Ctrl+Shift+F3 |
| F4 | Shift+F4 | Ctrl+F4 | Ctrl+Shift+F4 |
| F9 | Shift+F9 | Ctrl+F9 | Ctrl+Shift+F9 |
| F10 | Shift+F10 | Ctrl+F10 | Ctrl+Shift+F10 |
| F11 | Shift+F11 | Ctrl+F11 | Ctrl+Shift+F11 |
| F12 | Shift+F12 | Ctrl+F12 | Ctrl+Shift+F12 |

Isso fornece **32 combinações possíveis** para Chat e **32 para PTT**.

### Por que F5-F8 não aparecem nessa lista?

`F5`, `F6`, `F7` e `F8` foram intencionalmente excluídas do seletor de Chat/PTT porque são teclas frequentemente usadas pelo OMSI. O NavBR só usa `F6-F8` em combinações fixas com `Ctrl+Alt` para o TP/TS.

### Alt não é opção para Chat/PTT

O catálogo configurável usa:

- sem modificador;
- Shift;
- Ctrl;
- Ctrl+Shift.

Alt não é oferecido para Chat/PTT.

## 5. HUD

### `Ctrl+Alt+H` - ligar/desligar HUD

Alterna a visibilidade do HUD completo do NavBR durante o gameplay.

O comando só é tratado quando:

- o OMSI é a janela de gameplay em primeiro plano;
- o chat não está ativo.

O HUD atual é vinculado à janela de gameplay do OMSI e não deve permanecer sobre outras aplicações.

## 6. NavBR TP/TS

### `K` - configuração operacional

Abre o painel de configuração do NavBR TP/TS, incluindo linha/sentido e rotas reais resolvidas a partir do HOF do ônibus.

Com o painel já aberto:

- `K` fecha e salva;
- se o cursor estiver no campo de linha, a tecla não força o fechamento para não atrapalhar a digitação.

### `Ctrl+Alt+F6` - mostrar/ocultar TP/TS

Alterna a visibilidade do widget operacional.

### `Ctrl+Alt+F7` - trocar tema

Percorre os temas disponíveis do NavBR TP/TS.

### `Ctrl+Alt+F8` - trocar tamanho

Percorre os tamanhos disponíveis do NavBR TP/TS.

## 7. Personagem / RP

Essas teclas só são capturadas quando:

- o modo Personagem/RP está ativado;
- um personagem foi selecionado;
- o controle a pé está realmente ativo;
- o OMSI está em primeiro plano.

### Movimento

| Tecla | Ação |
|---|---|
| `W` | Andar para frente |
| `S` | Andar para trás |
| `A` | Virar para a esquerda |
| `D` | Virar para a direita |
| `Shift` | Correr enquanto se move |

Velocidades internas atuais:

- caminhada: aproximadamente 1,45 m/s;
- corrida: aproximadamente 3,25 m/s;
- marcha à ré: aproximadamente 1,05 m/s.

### `E` - entrar/voltar ao ônibus

Tenta retornar ao ônibus quando o personagem está dentro da distância de interação.

Distância atual: **até 8 metros**.

### `Esc` - retorno de emergência

Encerra imediatamente o controle a pé e solicita retorno ao ônibus.

Durante o modo RP, essas teclas são consumidas pelo controlador do NavBR para evitar que a mesma entrada seja aplicada simultaneamente ao OMSI.

## 8. Edição do HUD - controles de mouse relacionados

Embora não sejam atalhos de teclado, fazem parte da operação do HUD:

- botão **Mover HUD** - entra no modo de edição;
- arrastar - move o HUD;
- roda do mouse - altera o zoom do HUD;
- duplo clique no handle - restaura a posição/layout;
- botão **Bloquear HUD** - encerra o modo de edição.

O zoom manual do HUD pode chegar a 10x.

## 9. Detecção automática de conflitos

Antes de habilitar Chat/PTT, o NavBR analisa:

`OMSI 2\Inputs\keyboard.cfg`

Se encontrar a mesma tecla + modificadores já associados a um evento do OMSI:

- o atalho conflitante é desativado;
- o HUD mostra um aviso;
- o tooltip lista os eventos conflitantes quando disponíveis.

Se o `keyboard.cfg` não puder ser localizado/verificado, Chat/PTT ficam desativados por segurança.

## 10. Regras importantes

1. Chat e PTT devem usar combinações diferentes.
2. Os atalhos globais do HUD são processados somente no gameplay do OMSI.
3. Os atalhos de RP funcionam somente com o modo RP ativo.
4. Atalhos conflitantes com o OMSI não são forçados.
5. `F5-F8` não podem ser escolhidos para Chat/PTT.
6. `Ctrl+Alt+H` e `Ctrl+Alt+F6/F7/F8` são comandos fixos do NavBR.
7. `K` é o atalho fixo de configuração do NavBR TP/TS.

## 11. Referência compacta para impressão

```text
CHAT
F9                       Abrir chat (padrão/configurável)
Enter                    Enviar
Esc                      Fechar sem enviar

VOZ
F10                      PTT (padrão/configurável)
segurar F10              Falar
soltar F10               Parar transmissão

HUD
Ctrl + Alt + H            Mostrar/ocultar HUD

NAVBR TP/TS
K                         Abrir/fechar configuração TP/TS
Ctrl + Alt + F6           Mostrar/ocultar TP/TS
Ctrl + Alt + F7           Trocar tema TP/TS
Ctrl + Alt + F8           Trocar tamanho TP/TS

PERSONAGEM / RP
W                         Frente
S                         Trás
A                         Virar à esquerda
D                         Virar à direita
Shift                     Correr
E                         Entrar/voltar ao ônibus (até 8 m)
Esc                       Retorno de emergência

CHAT/PTT CONFIGURÁVEIS
F1-F4, F9-F12
Shift + tecla
Ctrl + tecla
Ctrl + Shift + tecla
```

## 12. Observação sobre atalhos do OMSI

Este documento cobre os atalhos **do NavBR Multiplayer**. Os comandos nativos do OMSI 2 continuam definidos pela instalação/perfil do próprio simulador e podem variar conforme `keyboard.cfg`, addons e configurações do usuário.
