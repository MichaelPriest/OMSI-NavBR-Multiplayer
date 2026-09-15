# Alpha.10 — checklist de teste da test.4

> Este checklist cobre a **v0.3.0-alpha.10-test.4** em desenvolvimento. A prerelease geral recomendada continua sendo a **v0.3.0-alpha.9**.

Use a prerelease permanente de integração:

```text
v0.3.0-alpha.10-test.4
```

Pacote recomendado:

```text
OMSI-NavBR-alpha10-test.4-integration-win-x86.zip
```

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
- preflight de .NET 10 Runtime x86 mantido no instalador do plugin.

**Ainda não existe ônibus remoto físico dentro do mundo 3D do OMSI.**

## 1. Preparação

- Feche o OMSI antes de instalar/atualizar o plugin.
- Feche outras cópias do NavBR.
- Preserve um backup de `OMSI\plugins` por precaução.
- Use a pasta `Plugin` completa do bundle.
- O plugin atual requer **.NET 10 Runtime x86**.

## 2. Instalação do plugin

Na pasta `Plugin` do bundle:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Install-NavBROmsiPlugin.ps1 -OmsiRoot "G:\Games\OMSI 2 Steam Edition"
```

Ajuste o caminho para sua instalação real.

O instalador deve:

- encontrar `Omsi.exe`;
- recusar instalação se o OMSI estiver aberto;
- validar um .NET 10 Runtime x86 real antes de copiar arquivos;
- criar o manifesto da instalação;
- não sobrescrever arquivos não rastreados.

## 3. Validação do novo ícone

Antes de abrir o OMSI, confira o `OMSI.NavBR.Multiplayer.exe`:

- Explorer deve mostrar o novo ícone NavBR;
- a janela principal deve usar o mesmo ícone;
- a janela Multiplayer também deve usar o mesmo ícone;
- se o Windows ainda exibir o antigo por cache, renomeie temporariamente o EXE ou reinicie o Explorer para confirmar o recurso embutido.

O CI da test.4 também valida o ícone embutido no EXE.

## 4. HUD moderno / GPS

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

## 5. Chat em jogo

Atalho padrão: `F9`.

Teste:

1. pressione o atalho de chat;
2. o campo deve abrir abaixo do GPS;
3. clique fora do campo: o clique **não deve atingir o OMSI** enquanto o chat estiver aberto;
4. digite letras/números normalmente;
5. `Enter` envia;
6. `Esc` fecha sem enviar;
7. ao fechar, o foco deve retornar ao OMSI e o overlay voltar a ser click-through.

## 6. Atalhos

Na janela Multiplayer, teste combinações diferentes para chat e PTT.

A lista inclui:

- F1-F4;
- F9-F12;
- Shift + tecla;
- Ctrl + tecla;
- Ctrl + Shift + tecla.

F5-F8 permanecem fora da lista. Se a combinação estiver ocupada em `Inputs/keyboard.cfg`, o NavBR deve bloqueá-la e mostrar o conflito.

## 7. Logs automáticos

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

## 8. Plugin bridge

No painel `PLUGIN BRIDGE v1 • EXP`, após instalar e abrir OMSI + NavBR, confirme:

```text
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
- `plugin-dir` apontando para a instalação real.

## 9. Dois computadores

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

## 10. O que enviar se houver problema

Informe:

- tag usada: `v0.3.0-alpha.10-test.4`;
- versão do OMSI;
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
- chat sem vazamento de clique para o OMSI;
- logs automáticos funcionando;
- plugin instalado e carregado com bridge estável;
- dois PCs funcionando no mesmo mapa;
- filtro de compatibilidade funcionando;
- nenhum crash/regressão importante no OMSI.
