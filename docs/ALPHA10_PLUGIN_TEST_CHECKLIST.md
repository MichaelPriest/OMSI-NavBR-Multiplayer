# Alpha.10 — checklist de teste da test.4

> Este checklist cobre a **v0.3.0-alpha.10-test.4** em desenvolvimento. A prerelease geral recomendada continua sendo a **v0.3.0-alpha.9**.

Use a prerelease permanente de integração:

```text
v0.3.0-alpha.10-test.4
```

Para o teste normal, o recomendado passa a ser o **EXE standalone**. O pacote integrado continua disponível como fallback técnico.

As `test.1`, `test.2` e `test.3` permanecem publicadas para rastreabilidade e não são sobrescritas.

## O que muda na test.4

Além da validação do plugin/bridge já presente na test.3, esta rodada inclui:

- novo ícone oficial no aplicativo/EXE;
- barra superior translúcida dentro do jogo;
- GPS em modo heading-up: ônibus sempre para cima, mapa/rota girando;
- linha, destino e próxima parada fora da área do mapa;
- indicação de manobra quando a geometria da rota é detalhada o suficiente;
- chat visual acoplado abaixo do GPS;
- bloqueio de clique no OMSI enquanto o campo de chat está aberto;
- mais combinações configuráveis para chat/PTT, sem F5-F8;
- `navbr.log` criado automaticamente em toda execução;
- pacote do plugin embutido dentro do próprio cliente;
- instalação/atualização/remoção do plugin pelo próprio NavBR;
- detecção automática do OMSI em bibliotecas Steam/unidades diferentes;
- seletor de pasta somente se a detecção automática falhar;
- preflight de .NET 10 Runtime x86 antes de qualquer cópia para o OMSI.

**Ainda não existe ônibus remoto físico dentro do mundo 3D do OMSI.**

## 1. Preparação

- Feche o OMSI antes de instalar/atualizar o plugin.
- Feche outras cópias do NavBR.
- Preserve um backup de `OMSI\plugins` por precaução.
- O plugin atual requer **.NET 10 Runtime x86**.
- Não é necessário copiar o NavBR para dentro da pasta do OMSI.

## 2. Instalação do plugin pelo próprio EXE

Abra o `OMSI.NavBR.Multiplayer.exe`.

No painel:

```text
PLUGIN BRIDGE v1 • EXP
```

use:

```text
Instalar / atualizar plugin
```

O NavBR deve:

1. usar a instalação do OMSI já detectada pelo cliente, quando disponível;
2. procurar o OMSI pelo Steam e por todas as bibliotecas configuradas;
3. aceitar instalações em qualquer unidade (`C:`, `D:`, `G:` etc.);
4. abrir um seletor de pasta apenas se a detecção automática falhar;
5. confirmar que a pasta contém `Omsi.exe`;
6. recusar instalação se o OMSI estiver aberto;
7. validar um .NET 10 Runtime x86 real antes de copiar arquivos;
8. extrair o pacote embutido somente para `OMSI\plugins`;
9. criar o manifesto da instalação;
10. não sobrescrever arquivos não rastreados de terceiros.

O fluxo normal **não exige PowerShell nem a pasta `Plugin` do ZIP**.

A pasta `Plugin` do pacote integrado permanece apenas como fallback técnico/diagnóstico.

## 3. Remoção pelo próprio EXE

Com o OMSI fechado, use:

```text
Remover plugin
```

O NavBR deve remover somente os arquivos registrados no manifesto da instalação NavBR.

## 4. Validação do novo ícone

Antes de abrir o OMSI, confira o `OMSI.NavBR.Multiplayer.exe`:

- Explorer deve mostrar o novo ícone NavBR;
- a janela principal deve usar o mesmo ícone;
- a janela Multiplayer também deve usar o mesmo ícone;
- a barra de tarefas deve usar o mesmo ícone;
- o workflow da `test.4` deve recusar a publicação se o asset de origem não for o novo ícone aprovado;
- se o Windows ainda exibir o antigo por cache, renomeie temporariamente o EXE ou reinicie o Explorer para confirmar o recurso embutido.

## 5. HUD moderno / GPS

Abra o NavBR, o OMSI 2.3.004, carregue um mapa e um ônibus.

Confirme no jogo:

- barra superior translúcida visível somente durante gameplay;
- indicador de conexão animado;
- sala/mapa, jogadores, chat, PTT e atalhos na barra;
- linha, destino e próxima parada em card separado do mapa;
- mapa do GPS centralizado no ônibus;
- marcador local sempre apontando para cima;
- ao virar o ônibus, mapa e rota devem girar no sentido contrário;
- zoom do mapa continua funcionando e pode chegar a 10x no modo de edição;
- setas de manobra aparecem apenas quando houver geometria detalhada suficiente;
- em rota grosseira/tile-fallback, é aceitável a seta ficar escondida.

## 6. Chat em jogo

Atalho padrão: `F9`.

Teste:

1. pressione o atalho de chat;
2. o campo deve abrir abaixo do GPS;
3. clique fora do campo: o clique **não deve atingir o OMSI** enquanto o chat estiver aberto;
4. digite letras/números normalmente;
5. as teclas digitadas **não devem acionar funções do OMSI**;
6. `Enter` envia;
7. `Esc` fecha sem enviar;
8. ao fechar, o foco deve retornar ao OMSI e o overlay voltar a ser click-through.

## 7. Atalhos

Na janela Multiplayer, teste combinações diferentes para chat e PTT.

A lista inclui:

- F1-F4;
- F9-F12;
- Shift + tecla;
- Ctrl + tecla;
- Ctrl + Shift + tecla.

F5-F8 permanecem fora da lista. Se a combinação estiver ocupada em `Inputs/keyboard.cfg`, o NavBR deve bloqueá-la e mostrar o conflito.

## 8. Logs automáticos

Ao abrir o cliente, deve existir automaticamente:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr.log
```

Um ciclo normal deve conter pelo menos `session-start` e, ao fechar normalmente, `session-end`.

Outros logs continuam separados:

```text
navbr-route.log
navbr-plugin.log
navbr-error.log
```

## 9. Plugin bridge

No painel `PLUGIN BRIDGE v1 • EXP`, antes da instalação confirme:

```text
package=EMBEDDED
```

Depois de instalar e abrir OMSI + NavBR, confirme:

```text
package=EMBEDDED
install=INSTALLED
files=3/3
manifest=YES
status=CONNECTED
process-match=YES
heartbeat=LIVE
```

Também confira:

- `callbacks` aumentando;
- `plugin-pid` igual ao PID do `Omsi.exe`;
- `remote` e `compatible` coerentes;
- `map` e fingerprint corretos;
- `plugin-dir` apontando para a instalação real do OMSI, independentemente da unidade.

## 10. Dois computadores

Depois do teste local passar nos dois PCs:

1. use o mesmo mapa/versão;
2. crie a sala no PC A;
3. entre pelo PC B;
4. confirme jogadores no GPS/HUD;
5. confirme chat e PTT;
6. confira `remote > 0` no plugin;
7. com mapa compatível, confira `compatible > 0`;
8. teste mapa incompatível e confirme que deixa de contar como compatível;
9. saia da sala e confirme remoção/timeout do estado remoto.

**Nenhum ônibus remoto físico deve aparecer dentro do OMSI nesta rodada.**

## 11. O que enviar se houver problema

Informe:

- tag usada: `v0.3.0-alpha.10-test.4`;
- versão do OMSI;
- caminho onde seu OMSI está instalado;
- mapa e ônibus;
- print/foto do HUD se o problema for visual;
- valores do painel `PLUGIN BRIDGE v1 • EXP`;
- conteúdo relevante de `navbr.log`;
- conteúdo relevante de `navbr-route.log`;
- conteúdo relevante de `navbr-plugin.log`;
- `navbr-error.log`, se existir;
- se houve crash, travamento ou queda perceptível de FPS.

## Critério para avançar

A representação física futura só deve avançar depois de confirmar:

- HUD/GPS estável sem regressão;
- ícone correto no build publicado;
- chat sem vazamento de clique/teclado para o OMSI;
- logs automáticos funcionando;
- instalador interno detectando instalações do OMSI em diferentes unidades;
- plugin instalado e carregado com bridge estável;
- dois PCs funcionando no mesmo mapa;
- filtro de compatibilidade funcionando;
- nenhum crash/regressão importante no OMSI.
