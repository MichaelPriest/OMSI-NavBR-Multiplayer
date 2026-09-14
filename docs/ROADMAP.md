# Roadmap

## Fase 0 — Bootstrap

- [x] Repositório e estrutura inicial
- [x] Cliente WPF
- [x] Servidor SignalR
- [x] Contrato de telemetria compartilhado
- [x] Detecção de `Omsi.exe` sem Steam
- [ ] Build CI verde

## Fase 1 — Telemetria local

- [ ] Detectar OMSI 2.3.004 por versão/hash
- [ ] Abrir processo com acesso somente de leitura
- [ ] Ler posição X/Y/Z
- [ ] Ler heading
- [ ] Ler velocidade
- [ ] Identificar mapa carregado
- [ ] Identificar veículo do jogador
- [ ] Criar profile de compatibilidade 2.3.004
- [ ] Fallback por signature scanning

### Critério de aceite

Com o OMSI aberto, o NavBR deve mostrar valores de posição/direção mudando em tempo real sem Steam API.

## Fase 2 — GPS local

- [ ] Detectar pasta `maps`
- [ ] Ler `global.cfg`
- [ ] Ler roadmap do OMSI
- [ ] Renderizar ônibus sobre o mapa
- [ ] Zoom/pan
- [ ] Follow vehicle
- [ ] Rotação automática
- [ ] Janela always-on-top

## Fase 3 — TTData

- [ ] Parser `Busstops.cfg`
- [ ] Parser `.ttr`
- [ ] Parser `.ttp`
- [ ] Parser `.ttl`
- [ ] Linha/rota selecionada
- [ ] Próxima parada
- [ ] Distância restante
- [ ] ETA

## Fase 4 — Multiplayer no NavBR

- [ ] Conectar cliente ao hub
- [ ] Criar/entrar em sala
- [ ] Player nickname
- [ ] Enviar telemetria em tempo real
- [ ] Interpolação de jogadores remotos
- [ ] Mostrar jogadores no mapa
- [ ] Hash/fingerprint do mapa
- [ ] Reconnect automático
- [ ] Rate limiting

## Fase 5 — Infraestrutura online

- [ ] Autenticação opcional
- [ ] Salas públicas/privadas
- [ ] Senha/convite
- [ ] Presence service
- [ ] Persistência mínima
- [ ] Deployment de produção

## Fase 6 — Veículos remotos dentro do OMSI (experimental)

- [ ] Investigar API oficial de plugins
- [ ] Protótipo de entidade remota
- [ ] Sincronizar posição/orientação
- [ ] Interpolação e extrapolação
- [ ] Portas/luzes/setas/buzina
- [ ] Limites de estabilidade/performance

Esta fase só será promovida a funcionalidade oficial se funcionar sem corromper estado do simulador.
