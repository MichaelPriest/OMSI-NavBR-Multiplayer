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

## Limites atuais da API pública do openOMSI

A API Lua pública continua sem expor canvas/egui customizado, desenho 3D arbitrário,
registro de widgets próprios no navigator nem comando público para acionar `get_up()`.
Por isso o plugin não usa hook/injeção. Para builds aprimoradas do openOMSI, o pacote
agora inclui um contrato opcional de host (`OpenOmsiGetOverlayFrameV2`) e um aplicador
reproduzível que integra o frame 2D e o world guidance no código do próprio openOMSI.

O openOMSI stock continua funcionando normalmente sem essa extensão. O fallback 3D usa
os mesmos helper objects das route arrows nativas; o modo `mesh` usa uma malha
translúcida compartilhada e é validado separadamente no CI.


## Integração com o Navigator nativo

Em uma build do openOMSI com a extensão host aplicada, o NavBR não cria um segundo
minimapa. O frame v2 controla temporariamente o próprio `Navigator` do openOMSI:

- `MiniMapVisible` liga/desliga o painel do navigator;
- `TrafficVisible` controla os veículos IA no mapa;
- `PlayersVisible` controla os jogadores LAN no mapa;
- `RouteGuidanceVisible` habilita orientação, mas as `nav_arrows` nativas só são usadas
  como fallback quando o `WorldGuidance` NavBR não estiver ativo;
- rota, congestionamento, road network, stops, autozoom e heading-up continuam vindo do
  pipeline nativo do openOMSI.

As preferências originais do Navigator são restauradas após o frame, portanto a extensão
não grava nem sobrescreve permanentemente as configurações do usuário.

O overlay próprio fica restrito ao que o Navigator não oferece: cartão compacto de
manobra, TeleMatrix e guidance 3D estilo NavBR.

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

1. manter stock + mesh host integration compilando contra o upstream pinado;
2. evoluir o HUD 2D para minimapa completo/TeleMatrix no caminho nativo;
3. melhorar a mesh de guidance com estilo de manobra e tema noturno;
4. ligar CCO/empresa/crachá/chat/voz ao transporte de rede do plugin;
5. manter o modo RP delegado ao `on_foot.rs` nativo.


## Overlay nativo opcional do openOMSI

O DLL exporta adicionalmente `OpenOmsiGetOverlayFrame`. Esse símbolo não substitui nem
altera a ABI OMSI clássica; uma build do openOMSI pode detectá-lo opcionalmente e consumir
o frame de HUD/mapa/setas já calculado pelo NavBR.

O contrato completo está em `OPENOMSI_OVERLAY_ABI.md`. O payload de setas representa
dados de navegação em coordenadas do mundo; uma build stock do openOMSI ainda não possui
API pública de plugin para desenhar essas setas 3D por conta própria.
