# Instalador de teste do OMSI NavBR Multiplayer

O workflow principal gera um instalador Windows por Inno Setup além dos artefatos portáteis.

## Conteúdo

O instalador contém:

- cliente NavBR completo x86 e self-contained;
- WebView2/React empacotado pelo projeto;
- bundle Native AOT do plugin OMSI já incorporado ao cliente;
- cópia separada do plugin em `Plugin/` para diagnóstico e instalação manual;
- simulador multiplayer em `Simulator/`, incluindo `run-online.cmd` e `run-local.cmd`;
- desinstalador padrão do Windows.

## Instalação

Por padrão é usado:

`%LOCALAPPDATA%\Programs\OMSI NavBR Multiplayer`

Isso permite testar o cliente sem exigir gravação em Program Files. A instalação do plugin dentro da pasta do OMSI continua sendo uma ação explícita pelo próprio NavBR ou pelos scripts de teste, porque a localização do OMSI depende da máquina do usuário.

## Validação automática

O CI instala silenciosamente o pacote em uma pasta temporária, confirma a presença do cliente, simulador, plugin e desinstalador e depois executa o uninstaller silenciosamente. O artefato só é publicado se esse smoke-test passar.
