# NavBR for openOMSI

Plugin separado para integrar o NavBR ao openOMSI sem depender dos offsets de memória do OMSI 2 original.

## Arquitetura

O openOMSI suporta o formato tradicional de plugins OMSI (`.opl` + DLL). Este pacote usa somente esse ABI documentado:

- `PluginStart`
- `PluginFinalize`
- `AccessVariable`
- `AccessSystemVariable`
- `AccessStringVariable`
- `AccessTrigger`

A DLL é Native AOT x64 para Windows e comunica com o aplicativo NavBR pelo mesmo bridge local versionado já usado pelo projeto.

## Estado inicial

Disponível:

- telemetria de velocidade;
- mapa, posição XYZ e heading via companion Lua oficial do openOMSI;
- linha/tour, próxima parada, destino, visão, estado on-foot e atraso via `omsi.info()`;
- pedido de parada;
- temperatura da cabine;
- passageiros;
- estado de horário;
- hora/data/pausa da simulação;
- IBIS linha/rota/destino/atraso;
- conexão ao NavBR desktop;
- diagnóstico do plugin;
- governor/otimizador de carga do NavBR;
- perfis Auto, Stability, Multiplayer, Quality e Diagnostics.

Não usa:

- ponteiros internos do OMSI 2;
- `NavBR.OmsiInterop.dll`;
- spawn físico por offsets do OMSI 2;
- controle de personagem baseado na memória do OMSI 2.

Esses recursos ficam desativados até existir uma API segura equivalente no openOMSI.

## Instalação

Este plugin é um pacote **separado do NavBR App**. O aplicativo NavBR não instala,
atualiza, remove nem inicia o openOMSI. A instalação deve ser feita no ambiente/launcher
do openOMSI usando o pacote x64 publicado pelo workflow dedicado.

Para instalação manual do pacote, feche o openOMSI, extraia o artefato e execute:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Install-NavBROpenOmsiPlugin.ps1 -OpenOmsiRoot "C:\caminho\para\content-root-do-openOMSI"
```

O parâmetro aponta para o **content root do openOMSI**, não para a instalação original do OMSI 2.
O plugin é instalado em:

```text
<content-root>\Plugins\NavBR.OpenOmsi\NavBR.OpenOmsiPlugin.opl
<content-root>\Plugins\NavBR.OpenOmsi\NavBR.OpenOmsiPlugin.dll
<content-root>\Plugins\NavBR.OpenOmsi\main.lua
```

O `.opl` referencia a DLL a partir da raiz `Plugins`, conforme o carregador do openOMSI.

> Importante: o plugin pertence ao ambiente do openOMSI. O NavBR App apenas expõe o gateway
> local e recebe a conexão do plugin externo; ele não gerencia a instalação do plugin.

## Remoção

```powershell
.\Remove-NavBROpenOmsiPlugin.ps1 -OpenOmsiRoot "C:\caminho\para\openOMSI"
```

O removedor só apaga arquivos registrados no manifesto NavBR.

## Próximas etapas

1. validar o snapshot Lua em uma sessão real do openOMSI;
2. transformar o snapshot seguro em telemetria NavBR local para mapa/CCO/navegação;
3. integrar multiplayer/CCO/empresa dentro da janela do openOMSI;
4. definir com o upstream uma API de alta frequência para spawn/movimento físico remoto sem offsets;
5. expandir o HUD in-game sem duplicar o bridge nativo.
