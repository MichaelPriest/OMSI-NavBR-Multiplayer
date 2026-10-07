# NavBR for openOMSI

Plugin separado para integrar o NavBR ao openOMSI sem depender de offsets de memória,
injeção gráfica ou hacks específicos do OMSI 2 original.

## Base upstream validada

A implementação desta branch está alinhada ao openOMSI
`openOMSI-Project/openOMSI@100f7f6a2322f3b35207694850a7a4970fe0bb17`.

São usados somente contratos públicos existentes nesse HEAD:

- plugin OMSI tradicional (`.opl` + DLL) para callbacks de variáveis;
- Lua plugin oficial para `omsi.info()`, `omsi.position()`, `omsi.others()`,
  `omsi.other_var()`, eventos, timers e dados persistidos;
- multiplayer/LAN e o modo on-foot continuam pertencendo ao próprio openOMSI.

## Arquitetura

O pacote tem duas partes complementares e independentes do NavBR Desktop:

1. **DLL Native AOT x64**
   - ABI OMSI documentada: `PluginStart`, `PluginFinalize`, `AccessVariable`,
     `AccessSystemVariable`, `AccessStringVariable`, `AccessTrigger`;
   - telemetria de alta frequência disponível pelas variáveis OMSI;
   - governor de carga;
   - bridge local opcional. A ausência do NavBR Desktop não impede o plugin de carregar.

2. **Companion Lua oficial do openOMSI**
   - posição e heading do veículo;
   - mapa, line/tour/trip, destino e próxima parada;
   - chegada/partida prevista da próxima parada;
   - view, pausa, multiplayer e estado on-foot;
   - contagem de tráfego;
   - veículos próximos via `omsi.others()`;
   - velocidade dos veículos próximos via `omsi.other_var(id, "Velocity")`;
   - classificação nativa `ai` / `player`.

O snapshot Lua v4 mantém compatibilidade com os leitores v1/v2/v3 e inclui `trips`, `next_stop_number`, `map_path`, `trip_name`, `stops`, `destination`, fabricante e modelo. Em movimento ou multiplayer ele é
persistido a até 2 Hz; parado, a 1 Hz. O loop de 0,25 s apenas decide se há trabalho,
evitando gravação por frame.

## Estado implementado nesta branch

Disponível:

- velocidade e telemetria básica do veículo;
- posição XYZ e heading;
- mapa e contexto do timetable;
- line, tour, trip, quantidade de trips, índice da próxima parada, destino e próxima parada;
- content root registrado pelo instalador e descoberta autônoma de `maps/<map>/TTData`;
- resolução real de `.ttl` → tour → trip → `.ttp` → sequência de paradas via `Busstops.cfg`;
- geometria de rota vinculada ao duty atual, com descarte automático ao trocar line/tour/trip;
- previsão de chegada/partida da próxima parada;
- IA próxima no mapa;
- jogadores openOMSI próximos no mapa;
- velocidade dos veículos próximos para cálculo de congestionamento;
- contadores separados de IA e players próximos;
- detecção de multiplayer;
- detecção de view e modo on-foot;
- pedido de parada;
- temperatura da cabine;
- passageiros;
- estado de horário;
- hora/data/pausa da simulação;
- IBIS linha/rota/destino/atraso;
- diagnóstico;
- governor/otimizador;
- perfis Auto, Stability, Multiplayer, Quality e Diagnostics.

## RP/personagem

O NavBR **não reimplementa** a física do personagem. O openOMSI atual já possui
`crates/omsi-app/src/on_foot.rs`, com:

- Ctrl+Shift+G para levantar/sair quando `get_up` está habilitado;
- W/A/S/D para caminhar;
- Shift para correr;
- Space para pular;
- C para agachar/levantar;
- G para entrar/sair e sentar em assentos disponíveis;
- F1 para primeira pessoa;
- F4 para câmera livre.

O plugin detecta `info.on_foot` e `info.view` e usa esse estado no NavBR. Isso evita
dois controladores competindo pelo mesmo personagem.

## Compatibilidade com atualizações do openOMSI

O NavBR agora é distribuído como **plugin-only**. Ele não substitui, não renomeia e não
faz patch do `openomsi.exe`.

O contrato obrigatório é somente o loader público do openOMSI:

- descoberta recursiva de `.opl` em `Plugins`;
- carregamento da DLL indicada em `[dll]`;
- exports OMSI clássicos `PluginStart` e `PluginFinalize`;
- callbacks opcionais de variável, sistema, string e trigger;
- companion `main.lua` pelas APIs públicas do openOMSI.

Exports NavBR adicionais, como `OpenOmsiGetOverlayFrameV2` e
`OpenOmsiSetHudFlagsV1`, permanecem opcionais e não são necessários para o plugin
carregar ou funcionar. Se uma versão futura do openOMSI oferecer integração nativa
equivalente, ela poderá consumi-los sem mudar a instalação do plugin.

Recursos que exigem desenho arbitrário dentro do renderer 3D do host ficam limitados
ao que a API pública do openOMSI expuser. O NavBR não irá manter um executável próprio
do openOMSI para contornar essa limitação.

## Separação obrigatória

Este projeto não usa:

- `NavBR.OmsiInterop.dll`;
- offsets ou leitura/escrita de memória do OMSI 2;
- spawn físico por ponteiros do OMSI 2;
- o controlador RP do plugin OMSI 2;
- WebView/React do aplicativo desktop.

Código compartilhado deve se limitar a protocolos e modelos neutros em
`NavBR.Shared`.

## Instalação

Este plugin é um pacote **separado do NavBR App**. O aplicativo NavBR não instala,
atualiza, remove nem inicia o openOMSI.

Para instalação manual, feche o openOMSI, extraia o artefato e execute:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Install-NavBROpenOmsiPlugin.ps1 -OpenOmsiRoot "C:\caminho\para\content-root-do-openOMSI"
```

Arquivos instalados:

```text
<content-root>\Plugins\NavBR.OpenOmsi\NavBR.OpenOmsiPlugin.opl
<content-root>\Plugins\NavBR.OpenOmsi\NavBR.OpenOmsiPlugin.dll
<content-root>\Plugins\NavBR.OpenOmsi\main.lua
```

## Remoção

```powershell
.\Remove-NavBROpenOmsiPlugin.ps1 -OpenOmsiRoot "C:\caminho\para\content-root-do-openOMSI"
```

O removedor só apaga arquivos registrados no manifesto NavBR.

## Próximas etapas

1. manter o plugin carregando no **openOMSI stock**, sem patches do executável;
2. acompanhar novas APIs públicas de HUD/mapa/3D e adotá-las quando disponíveis;
3. evoluir minimapa, TeleMatrix, chat, voz/PTT e controles usando apenas interfaces estáveis;
4. manter CCO/empresa/crachá e multiplayer no transporte próprio do NavBR;
5. manter o modo RP delegado ao `on_foot.rs` nativo.

## Regra de instalação

Atualizações do openOMSI podem substituir o executável oficial à vontade. O NavBR
permanece separado em:

```text
<content-root>\Plugins\NavBR.OpenOmsi\NavBR.OpenOmsiPlugin.dll
<content-root>\Plugins\NavBR.OpenOmsi\NavBR.OpenOmsiPlugin.opl
<content-root>\Plugins\NavBR.OpenOmsi\main.lua
```

Enquanto o openOMSI mantiver o contrato público de plugins, nenhuma atualização do
`openomsi.exe` precisa ser substituída pelo NavBR.
