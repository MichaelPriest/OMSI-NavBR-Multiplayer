# Documentação do OMSI NavBR Multiplayer

## Desenvolvimento atual — Alpha.12

A próxima versão de desenvolvimento é **`0.3.0-alpha.12-dev`** e concentra o escopo completo de expansão do NavBR.

- [Alpha.12 — escopo mestre](ALPHA12_MASTER_SCOPE.md) — fonte principal para tudo que entra na próxima versão: GPS avançado, multiplayer, salas públicas/privadas, voz, ônibus físico 3D, tráfego compartilhado, Hardware Cockpit, perfil do motorista, empresas virtuais, CCO/Dispatcher, replay, mapa web, permissões, SDK, workshop e companion.
- [Roadmap](ROADMAP.md) — histórico e evolução das fases anteriores. Para decisões novas da Alpha.12, o escopo mestre acima prevalece.
- [Alpha.11 — desenvolvimento](ALPHA11_DEVELOPMENT.md) — histórico técnico da série Alpha.11.
- [Alpha.11 Test 4 — comunidade](ALPHA11_TEST4_COMMUNITY.md) — checklist da última pré-release pública antes da Alpha.12.

## Uso e testes

- [Manual de uso](MANUAL_DE_USO.md)
- [Hardware Cockpit Bridge](HARDWARE_COCKPIT.md)
- [Como gerar Roadmap dos mapas](GERAR_ROADMAP_MAPAS.md)
- [HUD e chat por voz](HUD_AND_VOICE.md)
- [Telemetria](TELEMETRY.md)

## Multiplayer e integração OMSI

- [Rede multiplayer](NETWORKING.md)
- [Salas peer-host](PEER_HOST.md)
- [Estado do multiplayer](MULTIPLAYER_STATUS.md)
- [Plugin OMSI experimental](OMSI_PLUGIN_EXPERIMENTAL.md)
- [Referências técnicas OMSI](REFERENCIAS_OMSILAUNCH_OMSIHOOK.md)

A Alpha.11 já possui base experimental para `spawn -> update -> despawn` de ônibus remotos físicos dentro do OMSI, mas o recurso continua **opt-in e sujeito a validação real**. O plugin não é obrigatório para GPS/HUD/multiplayer externo.

## Mapas, rota e diagnóstico

Problemas de traçado devem incluir, quando possível:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-route.log
```

O NavBR trabalha com `global.cfg`, TTData (`.ttp/.ttr`), tiles `.map`, splines `.sli`, paths/crossings `.sco` e roadmaps quando disponíveis.

## Distribuição e licenças

- [Releases](RELEASES.md)
- [Licenças](LICENSES.md)
- [Arquitetura](ARCHITECTURE.md)

## Regra de documentação da Alpha.12

Nenhuma funcionalidade combinada deve desaparecer do backlog. Recursos ainda não seguros podem permanecer experimentais ou atrás de feature flags, mas continuam registrados em [ALPHA12_MASTER_SCOPE.md](ALPHA12_MASTER_SCOPE.md) até serem implementados, validados ou explicitamente substituídos por uma solução equivalente.
