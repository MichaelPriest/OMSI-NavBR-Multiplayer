# Documentação do OMSI NavBR Multiplayer

## Alpha.20 pública de teste

A versão pública atual é **`v0.3.0-alpha.20`**.

- [ALPHA20_RELEASE_NOTES.md](ALPHA20_RELEASE_NOTES.md) — notas da versão e limitações conhecidas;
- [RELEASES.md](RELEASES.md) — pacotes e estado público atual;
- [WEB_UI_ARCHITECTURE.md](WEB_UI_ARCHITECTURE.md) — arquitetura React/WebView2 e autoridade nativa;
- [HUD_PRESETS.md](HUD_PRESETS.md) — presets, módulos e personalização do HUD;
- [MULTIPLAYER_STATUS.md](MULTIPLAYER_STATUS.md) — estado real do multiplayer;
- [PLUGIN_UPDATE_VERIFIER.md](PLUGIN_UPDATE_VERIFIER.md) — verificação/atualização automática do plugin.

A Alpha.20 reorganiza a interface por grupos funcionais, move idioma e preferências para Configurações → Geral, refaz a seleção do HUD em um workspace dedicado e adiciona Roadmap Studio com minimapa HD/Ultra. C# e OMSI continuam como autoridades dos dados reais.

### English

Alpha.20 is the current public test prerelease. It reorganizes the desktop UI by functional groups, moves language and general preferences into Settings → General, introduces a dedicated HUD workspace, and adds HD/Ultra minimap generation and roadmap comparison. C#/OMSI remain authoritative for real runtime data.

## Uso e testes / Usage and testing

- [Manual de uso](MANUAL_DE_USO.md)
- [Hardware Cockpit Bridge](HARDWARE_COCKPIT.md)
- [Como gerar Roadmap dos mapas](GERAR_ROADMAP_MAPAS.md)
- [HUD e chat por voz](HUD_AND_VOICE.md)
- [Telemetria](TELEMETRY.md)
- [Simulador Multiplayer](MULTIPLAYER_SIMULATOR.md)

## Multiplayer e integração OMSI / Multiplayer and OMSI integration

- [Rede multiplayer](NETWORKING.md)
- [Salas peer-host](PEER_HOST.md)
- [Estado do multiplayer](MULTIPLAYER_STATUS.md)
- [Plugin OMSI experimental](OMSI_PLUGIN_EXPERIMENTAL.md)
- [Referências técnicas OMSI](REFERENCIAS_OMSILAUNCH_OMSIHOOK.md)

## Mapas, rota e diagnóstico / Maps, routing and diagnostics

Problemas de traçado devem incluir, quando possível:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-route.log
```

Routing reports should include the same log whenever possible.

## Histórico / History

A documentação Alpha.14/13/12/11 permanece no repositório como histórico técnico e de testes.
