# Documentação do OMSI NavBR Multiplayer

## Uso e testes

- [Manual de uso](MANUAL_DE_USO.md)
- [Como gerar Roadmap dos mapas](GERAR_ROADMAP_MAPAS.md) — passo a passo no OMSI Editor para criar `whole.roadmap.bmp` e lidar com mapas grandes.
- [Roadmap de desenvolvimento](ROADMAP.md)
- [Checklist de teste v0.3.0-alpha.2](ALPHA2_TEST_CHECKLIST.md)

## Arquitetura e integração

- [Arquitetura](ARCHITECTURE.md)
- [Telemetria](TELEMETRY.md)
- [Salas peer-host](PEER_HOST.md)
- [HUD e chat por voz](HUD_AND_VOICE.md)
- [Rede multiplayer](NETWORKING.md)
- [Plugin OMSI experimental](OMSI_PLUGIN_EXPERIMENTAL.md) — investigação opcional para representar veículos remotos dentro do simulador.

## Distribuição e licenças

- [Releases](RELEASES.md)
- [Licenças](LICENSES.md)

## Para quem só quer jogar

Comece pelo [Manual de uso](MANUAL_DE_USO.md). Se o mapa for detectado mas aparecer sem imagem de fundo no GPS, siga [Como gerar Roadmap dos mapas](GERAR_ROADMAP_MAPAS.md).

Problemas de traçado devem incluir, quando possível:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-route.log
```

O plugin experimental está isolado da alpha.9 e não é necessário para GPS, HUD ou multiplayer externo.
