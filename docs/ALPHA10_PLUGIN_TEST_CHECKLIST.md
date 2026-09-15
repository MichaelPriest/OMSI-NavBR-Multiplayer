# Alpha.10 — checklist de teste da test.6

> Este checklist cobre a **v0.3.0-alpha.10-test.6**. A prerelease geral recomendada continua sendo a **v0.3.0-alpha.9**.

Use a prerelease permanente de integração:

```text
v0.3.0-alpha.10-test.6
```

As `test.1` a `test.5` permanecem preservadas como snapshots históricos. A `test.6` é a primeira rodada que combina o HUD/GPS moderno com **plugin Native AOT x86 autocontido**, sem exigir instalação separada do .NET Runtime x86.

Para o teste normal, o recomendado é o **EXE standalone**. O pacote integrado continua disponível como fallback técnico.

## O que validar na test.6

- novo ícone compacto do NavBR no EXE, janelas e barra de tarefas;
- HUD sem `XamlParseException`;
- barra superior translúcida moderna;
- GPS heading-up: ônibus sempre para cima e mapa/rota girando;
- linha, destino e próxima parada fora da área do mapa;
- setas de manobra quando houver geometria suficiente;
- chat acoplado ao GPS/HUD;
- clique e teclado não devem vazar para o OMSI enquanto o chat estiver aberto;
- atalhos configuráveis, mantendo F5-F8 reservados;
- `navbr.log` automático;
- `package=EMBEDDED` no painel do plugin;
- `deployment=NATIVE-AOT-X86` e `runtime=BUILT-IN`;
- instalação/atualização/remoção do plugin pelo próprio EXE;
- **nenhum pedido para instalar .NET Runtime x86**;
- detecção automática do OMSI pelo caminho já conhecido/processo, registro Aerosoft e bibliotecas Steam em qualquer unidade;
- seletor de pasta somente se todas as detecções automáticas falharem.

**Ainda não existe ônibus remoto físico no mundo 3D do OMSI.**

## Instalação do plugin

1. Feche o OMSI.
2. Abra `OMSI.NavBR.Multiplayer.exe`.
3. No painel `PLUGIN BRIDGE v1 • EXP`, confirme `package=EMBEDDED`.
4. Clique em **Instalar / atualizar plugin**.
5. O NavBR deve localizar o OMSI automaticamente, mesmo em outra unidade/biblioteca.
6. A descoberta considera também `HKLM\SOFTWARE\WOW6432Node\aerosoft\OMSI 2` / `Product_Path`, referência usada pelo OMSI Launcher.
7. Se não localizar, selecione manualmente a pasta que contém `Omsi.exe`.
8. Não é necessário abrir PowerShell nem copiar o NavBR para dentro da pasta do jogo.
9. Não é necessário instalar .NET Runtime x86: o plugin Native AOT é autocontido.

Resultado esperado após instalar o plugin, abrir OMSI e manter o NavBR aberto:

```text
package=EMBEDDED
deployment=NATIVE-AOT-X86
install=INSTALLED
files=2/2
manifest=YES
runtime=BUILT-IN
status=CONNECTED
process-match=YES
heartbeat=LIVE
```

`callbacks` deve aumentar continuamente depois que o OMSI estiver carregado e chamando as variáveis do plugin.

## Ícone

Confira:

- Explorer;
- janela principal;
- janela Multiplayer;
- barra de tarefas.

A `test.6` gera um ICO específico para Windows com 10 tamanhos (16 a 256 px), usando somente o símbolo compacto **pino laranja + ônibus**, em vez de reduzir a arte promocional inteira.

Se o Windows ainda mostrar um ícone antigo somente em um atalho já existente, crie um novo atalho diretamente a partir do EXE da `test.6` antes de considerar falha de empacotamento. O próprio EXE deve conter o novo recurso de ícone.

## HUD / GPS

- HUD deve abrir sem `XamlParseException`;
- barra translúcida deve aparecer em gameplay;
- ônibus local permanece apontando para cima;
- mapa/rota giram no sentido contrário ao giro do ônibus;
- linha, destino e próxima parada ficam fora do mapa;
- zoom continua até 10x no modo de edição;
- seta de manobra só aparece quando a geometria é confiável;
- abrir menus/opções/timetable do OMSI não deve deixar o overlay preso sobre telas onde deveria se ocultar.

## Chat

Atalho padrão: `F9`.

Enquanto estiver digitando:

- clique não deve atingir o OMSI;
- letras/números não devem acionar funções do jogo;
- `Enter` envia;
- `Esc` fecha sem enviar;
- ao fechar, o overlay volta a ser click-through.

## Atalhos

Teste F1-F4 e F9-F12, com Shift/Ctrl/Ctrl+Shift. F5-F8 permanecem fora da lista. Conflitos com `Inputs/keyboard.cfg` devem ser bloqueados.

## Logs

Deve ser criado automaticamente:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr.log
```

Também continuam:

```text
navbr-route.log
navbr-plugin.log
navbr-error.log
```

No `navbr-plugin.log`, uma carga bem-sucedida da nova arquitetura deve registrar `PluginStart` e `deployment=native-aot`.

## Dois computadores

Depois do teste local passar:

1. mesmo mapa/versão nos dois PCs;
2. PC A cria a sala;
3. PC B entra;
4. confirme jogadores no GPS/HUD;
5. confirme chat/PTT;
6. `remote > 0`;
7. mapa compatível: `compatible > 0`;
8. mapa incompatível: `compatible=0`;
9. saída da sala remove o estado remoto em até alguns segundos/evento de desconexão;
10. reconexão/rejoin não deve duplicar jogador remoto.

**Nenhum ônibus remoto físico deve aparecer dentro do OMSI nesta rodada.**

## Referências técnicas desta rodada

As investigações usam como referência, sem incorporar diretamente código incompatível:

- `NyCodeGHG/omsi-launcher`: descoberta de instalação, Steam App ID `252530`, bibliotecas e registro Aerosoft;
- `space928/Omsi-Extensions` / `OmsiHook`: arquitetura de integração com OMSI 2.3.004, `PlayerVehicle`, `RoadVehicles`, estado de veículos e ponte plugin/RPC.

A camada de veículos remotos físicos será investigada **depois** da aceitação desta base. Não será ativada por escrita arbitrária de memória nesta test.6.

## Se houver problema

Envie:

- tag `v0.3.0-alpha.10-test.6`;
- versão do OMSI;
- caminho da instalação do OMSI;
- mapa/ônibus;
- print do HUD;
- print do ícone no Explorer/barra de tarefas se estiver incorreto;
- painel `PLUGIN BRIDGE v1 • EXP`;
- `navbr.log`;
- `navbr-route.log`;
- `navbr-plugin.log`;
- `navbr-error.log`, se existir.
