# Alpha.10 — checklist de teste da test.5

> Este checklist cobre a **v0.3.0-alpha.10-test.5** em desenvolvimento. A prerelease geral recomendada continua sendo a **v0.3.0-alpha.9**.

Use a prerelease permanente de integração:

```text
v0.3.0-alpha.10-test.5
```

A `test.4` permanece preservada como snapshot anterior. A `test.5` corrige o erro de XAML do HUD encontrado na `test.4` e incorpora o novo fluxo de plugin dentro do próprio EXE.

Para o teste normal, o recomendado passa a ser o **EXE standalone**. O pacote integrado continua disponível como fallback técnico.

## O que validar na test.5

- novo ícone oficial no EXE, janelas e barra de tarefas;
- correção do `XamlParseException` do HUD;
- barra superior translúcida moderna;
- GPS heading-up: ônibus sempre para cima e mapa/rota girando;
- linha, destino e próxima parada fora da área do mapa;
- setas de manobra quando houver geometria suficiente;
- chat acoplado abaixo do GPS;
- clique e teclado não devem vazar para o OMSI enquanto o chat estiver aberto;
- atalhos configuráveis, mantendo F5-F8 reservados;
- `navbr.log` automático;
- `package=EMBEDDED` no painel do plugin;
- instalação/atualização/remoção do plugin pelo próprio EXE;
- detecção automática do OMSI em bibliotecas Steam e unidades diferentes;
- seletor de pasta somente se a detecção automática falhar;
- preflight do .NET 10 Runtime x86 antes de copiar o plugin.

**Ainda não existe ônibus remoto físico no mundo 3D do OMSI.**

## Instalação do plugin

1. Feche o OMSI.
2. Abra `OMSI.NavBR.Multiplayer.exe`.
3. No painel `PLUGIN BRIDGE v1 • EXP`, confirme `package=EMBEDDED`.
4. Clique em **Instalar / atualizar plugin**.
5. O NavBR deve localizar o OMSI automaticamente, mesmo em outra unidade.
6. Se não localizar, selecione manualmente a pasta que contém `Omsi.exe`.
7. Não é necessário abrir PowerShell nem copiar o NavBR para dentro da pasta do jogo.

Resultado esperado após abrir OMSI + NavBR:

```text
package=EMBEDDED
install=INSTALLED
files=3/3
manifest=YES
status=CONNECTED
process-match=YES
heartbeat=LIVE
```

`callbacks` deve aumentar continuamente.

## Ícone

Confira:

- Explorer;
- janela principal;
- janela Multiplayer;
- barra de tarefas.

O workflow da `test.5` valida que o asset de origem é exatamente o novo ícone aprovado antes de gerar o EXE.

## HUD / GPS

- HUD deve abrir sem `XamlParseException`;
- barra translúcida deve aparecer em gameplay;
- ônibus local permanece apontando para cima;
- mapa/rota giram no sentido contrário ao giro do ônibus;
- linha, destino e próxima parada ficam fora do mapa;
- zoom continua até 10x no modo de edição;
- seta de manobra só aparece quando a geometria é confiável.

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
9. saída da sala remove o estado remoto.

**Nenhum ônibus remoto físico deve aparecer dentro do OMSI nesta rodada.**

## Se houver problema

Envie:

- tag `v0.3.0-alpha.10-test.5`;
- versão do OMSI;
- caminho da instalação do OMSI;
- mapa/ônibus;
- print do HUD;
- painel `PLUGIN BRIDGE v1 • EXP`;
- `navbr.log`;
- `navbr-route.log`;
- `navbr-plugin.log`;
- `navbr-error.log`, se existir.
