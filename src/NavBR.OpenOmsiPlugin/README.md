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

Feche o openOMSI, extraia o artefato e execute:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Install-NavBROpenOmsiPlugin.ps1 -OpenOmsiRoot "C:\caminho\para\openOMSI"
```

O instalador cria:

```text
<openOMSI>\plugins\NavBR.OpenOmsi\NavBR.OpenOmsiPlugin.opl
<openOMSI>\plugins\NavBR.OpenOmsi\NavBR.OpenOmsiPlugin.dll
```

O `.opl` referencia a DLL a partir da raiz `plugins`, conforme o carregador do openOMSI.

## Remoção

```powershell
.\Remove-NavBROpenOmsiPlugin.ps1 -OpenOmsiRoot "C:\caminho\para\openOMSI"
```

O removedor só apaga arquivos registrados no manifesto NavBR.

## Próximas etapas

1. detectar automaticamente instalações do openOMSI no React;
2. botão Instalar/Atualizar/Remover na tela de Plugins;
3. telemetria posicional nativa do openOMSI por API segura;
4. integração NavBR multiplayer/CCO/empresa dentro da janela do openOMSI;
5. avaliar companion Lua somente para recursos de HUD que façam sentido sem duplicar o bridge nativo.
