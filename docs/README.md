# Documentação do OMSI NavBR Multiplayer

## Alpha.18 pública de teste

- [ALPHA18_RELEASE_NOTES.md](ALPHA18_RELEASE_NOTES.md) — notas da versão e limitações conhecidas;
- [ALPHA18_COMMUNITY.md](ALPHA18_COMMUNITY.md) — roteiro de validação pública;
- [MULTIPLAYER_STATUS.md](MULTIPLAYER_STATUS.md) — estado real do multiplayer;
- [PLUGIN_UPDATE_VERIFIER.md](PLUGIN_UPDATE_VERIFIER.md) — verificação/atualização automática do plugin.


## Desenvolvimento atual — Alpha.15

A versão de desenvolvimento e publicação atual é **`0.3.0-alpha.15`**.

### Português (pt-BR)

- [Alpha.15 — notas da versão](ALPHA15_RELEASE_NOTES.md)
- [Alpha.15 — escopo mestre](ALPHA15_MASTER_SCOPE.md)
- [Alpha.15 — roteiro de validação](ALPHA15_COMMUNITY.md)
- [Roadmap](ROADMAP.md)
- [Releases](RELEASES.md)

A Alpha.15 consolida a interface React/WebView2, multiplayer, navegação, Plugin Bridge v3, state interop ABI v7, ônibus físico experimental, Personagem/RP e o Portal V2. Escritas nativas no OMSI permanecem experimentais e opt-in.

### English (en)

- [Alpha.15 — release notes / notas da versão](ALPHA15_RELEASE_NOTES.md)
- [Alpha.15 — master scope / escopo mestre](ALPHA15_MASTER_SCOPE.md)
- [Alpha.15 — validation guide / roteiro de validação](ALPHA15_COMMUNITY.md)
- [Roadmap](ROADMAP.md)
- [Releases](RELEASES.md)

Alpha.15 consolidates the React/WebView2 desktop UI, multiplayer, navigation, Plugin Bridge v3, state interop ABI v7, experimental physical buses, Character/RP and Portal V2. Native OMSI writes remain experimental and opt-in.

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
