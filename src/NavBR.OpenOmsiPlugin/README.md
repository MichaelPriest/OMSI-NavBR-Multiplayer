# NavBR for openOMSI

Plugin separado para integrar o NavBR ao openOMSI sem depender de offsets de memória,
injeção gráfica ou hacks específicos do OMSI 2 original.

## Base upstream validada

A implementação desta branch está alinhada ao openOMSI
`openOMSI-Project/openOMSI@c97872833b8179cf756af7d1d560597a2248f8c2`.

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

O snapshot Lua v2 é compatível com o leitor v1. Em movimento ou multiplayer ele é
persistido a até 2 Hz; parado, a 1 Hz. O loop de 0,25 s apenas decide se há trabalho,
evitando gravação por frame.

## Estado implementado nesta branch

Disponível:

- velocidade e telemetria básica do veículo;
- posição XYZ e heading;
- mapa e contexto do timetable;
- line, tour, trip, destino e próxima parada;
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

No HEAD validado, a API Lua não expõe um canvas/egui customizado, desenho 3D de plugin,
registro de widgets próprios no navigator, nem um comando público para acionar
`get_up()`. Portanto, estes itens **não serão implementados por hook/injeção**:

- painel NavBR moderno desenhado dentro da janela do jogo;
- HUDs gráficos customizados;
- setas 3D estilo Forza Horizon;
- ativar o modo on-foot programaticamente pelo plugin.

O openOMSI já possui navigator, city map, `nav_ai`, `nav_arrows` e o on-foot nativo.
A integração NavBR deve reutilizar esses recursos até o upstream disponibilizar uma API
de extensão gráfica/configuração em runtime.

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

1. consumir o snapshot v2 diretamente no mapa/GPS do plugin;
2. montar o grafo de rota/timetable sem reutilizar parsers específicos do desktop;
3. adicionar rejoin de rota e congestionamento sobre os veículos nativos;
4. ligar CCO/empresa/crachá/chat/voz ao transporte de rede do plugin;
5. criar HUD/painel/setas 3D somente quando houver API gráfica oficial no openOMSI;
6. manter o modo RP delegado ao `on_foot.rs` nativo.
