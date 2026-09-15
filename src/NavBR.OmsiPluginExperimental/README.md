# NavBR OMSI Plugin — experimental

Este projeto é um protótipo isolado para validar a interface de plugins do OMSI 2 antes de qualquer tentativa de representar veículos remotos dentro do simulador.

## Estado atual

A alpha.10 experimental já possui:

- build Windows x86;
- exports/callbacks esperados pelo OMSI;
- arquivo `.opl` mínimo;
- heartbeat pela system variable `Time`;
- log de `PluginStart`, heartbeat e `PluginFinalize`;
- bridge local por Windows Named Pipes;
- protocolo versionado entre NavBR.Client e plugin;
- contexto do mapa local e estados remotos;
- remoção/limpeza de jogadores remotos;
- limite de 64 estados remotos;
- timeout de 5 segundos;
- filtro por mapa / `MapCompatibilityId`;
- interpolação básica de posição, velocidade e heading.

Ainda é **somente infraestrutura/diagnóstico**:

- **não escreve variáveis do veículo**;
- **não aciona triggers**;
- **não cria nem altera veículos no OMSI**.

O log é salvo em:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-plugin.log
```

## Arquivos esperados no pacote

O build via DNNE gera uma DLL nativa de entrada chamada:

```text
NavBR.OmsiPlugin.dll
```

O arquivo de configuração é:

```text
NavBR.OmsiPlugin.opl
```

Como o protótipo usa hospedagem .NET, o pacote de teste deve preservar também os assemblies e arquivos de runtime gerados pelo build. Não copie somente a DLL nativa.

O artefato também inclui:

```text
Install-NavBROmsiPlugin.ps1
Remove-NavBROmsiPlugin.ps1
```

## Requisito temporário

Nesta fase o plugin é **framework-dependent**. O computador de teste precisa ter o **.NET 10 Runtime x86** disponível para o processo 32-bit do OMSI. O EXE standalone do NavBR ser self-contained não instala esse runtime globalmente.

Antes de distribuir o plugin como funcionalidade normal, o empacotamento deverá ser revisto para reduzir ou eliminar esse requisito manual quando tecnicamente possível.

## Instalação experimental

1. Feche o OMSI.
2. Extraia **todo o artefato** `OMSI-NavBR-Plugin-experimental-win-x86` em uma pasta temporária.
3. Confirme que o .NET 10 Runtime x86 está instalado.
4. Abra o PowerShell nessa pasta.
5. Execute:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Install-NavBROmsiPlugin.ps1 -OmsiRoot "G:\Games\OMSI 2 Steam Edition"
```

Troque o caminho pelo diretório real da sua instalação.

O instalador:

- recusa instalar se `Omsi.exe` estiver aberto;
- confirma que o diretório informado contém `Omsi.exe`;
- copia somente arquivos instaláveis do pacote (`.dll`, `.json`, `.opl`);
- grava um manifesto em `OMSI\plugins\NavBR.OmsiPlugin.install-manifest.txt`.

Depois, inicie o OMSI normalmente, carregue um mapa e um ônibus e verifique:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-plugin.log
```

Um primeiro teste bem-sucedido deve registrar `PluginStart` e heartbeats periódicos.

## Remoção experimental

Feche o OMSI e execute, a partir do mesmo pacote extraído:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Remove-NavBROmsiPlugin.ps1 -OmsiRoot "G:\Games\OMSI 2 Steam Edition"
```

O removedor exige o manifesto criado pelo instalador e remove somente os arquivos listados nele. Se o manifesto não existir, o script interrompe sem apagar arquivos por tentativa/adivinhação.

## O que observar no teste do bridge

Com o cliente NavBR aberto e o plugin carregado, o log deverá evoluir de:

```text
PluginStart
bridge conectado ...
heartbeat ... remoteCount=0 compatibleRemoteCount=0
```

Quando houver outro jogador compatível na sala, o heartbeat deverá refletir o estado recebido. Um jogador em outro mapa/versão não deve aumentar `compatibleRemoteCount`.

Estados sem atualização são removidos após aproximadamente 5 segundos para evitar remotos presos.

## Segurança

Este protótipo não deve ser tratado como funcionalidade estável enquanto não houver validação real no OMSI 2.3.004.

O plugin continua opcional e o multiplayer externo do NavBR deve funcionar normalmente sem ele. Em caso de instabilidade, feche o OMSI e use o script de remoção antes de continuar os testes.
