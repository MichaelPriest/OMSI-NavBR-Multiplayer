# Documentação do OMSI NavBR Multiplayer

> **Estado atual do multiplayer:** jogadores já podem compartilhar presença, telemetria, chat/voz e aparecer no **mapa/minimapa do NavBR**, mas **o ônibus do outro jogador ainda NÃO é criado dentro do mundo 3D do OMSI**. Veja [Estado real do multiplayer](MULTIPLAYER_STATUS.md).

## Uso e testes

- [Manual de uso](MANUAL_DE_USO.md)
- [Estado real do multiplayer](MULTIPLAYER_STATUS.md) — diferença entre jogador no GPS/HUD do NavBR e ônibus físico dentro do OMSI, limitações atuais e etapas necessárias antes do multiplayer visual 3D.
- [Como gerar Roadmap dos mapas](GERAR_ROADMAP_MAPAS.md) — passo a passo no OMSI Editor para criar `whole.roadmap.bmp` e lidar com mapas grandes.
- [Checklist de teste v0.3.0-alpha.10 — plugin e bridge](ALPHA10_PLUGIN_TEST_CHECKLIST.md) — instalação segura, painel `PLUGIN BRIDGE v1 • EXP`, heartbeat e teste entre dois PCs.
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

Se a dúvida for “por que vejo o outro jogador no NavBR, mas não dentro do OMSI?”, consulte [Estado real do multiplayer](MULTIPLAYER_STATUS.md). Esse é o comportamento esperado das builds atuais.

Problemas de traçado devem incluir, quando possível:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-route.log
```

O plugin experimental está isolado da alpha.9 e não é necessário para GPS, HUD ou multiplayer externo. Para testar a alpha.10 experimental, siga o checklist específico antes de qualquer tentativa futura de criar veículos remotos dentro do OMSI.