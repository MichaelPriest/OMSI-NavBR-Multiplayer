# Roadmap

## Fase 0 — Bootstrap

- [x] Repositório e estrutura inicial
- [x] Cliente WPF
- [x] Servidor SignalR
- [x] Contrato de telemetria compartilhado
- [x] Detecção de `Omsi.exe` sem Steam
- [x] Base multilíngue com `.resx` / `ResourceManager`
- [x] Português (Brasil), English, Español, Deutsch e Français
- [x] Detecção automática do idioma do Windows
- [x] Troca de idioma em tempo real e preferência persistida
- [x] Build CI verde
- [x] Ícone oficial do aplicativo embutido no executável
- [x] Versionamento SemVer inicial
- [x] Workflow de GitHub Release para cliente e servidor

## Fase 1 — Telemetria local

- [x] Detectar OMSI 2.3.004 por versão/hash
- [x] Abrir processo com acesso somente de leitura
- [x] Implementar leitura de posição X/Y/Z
- [x] Implementar leitura de heading
- [x] Implementar leitura de velocidade
- [x] Implementar identificação do mapa carregado
- [x] Resolver o veículo ativo do jogador
- [x] Criar profile de compatibilidade 2.3.004
- [x] Dashboard multilíngue de telemetria a cada 200 ms
- [x] Ler GridX/GridY e posição local TileX/TileY
- [ ] Validar posição em runtime no OMSI 2.3.004
- [ ] Validar velocidade contra o velocímetro do OMSI
- [ ] Validar sinal/zero do heading para o GPS
- [ ] Criar allowlist de hashes conhecidos após os primeiros testes
- [ ] Fallback por signature scanning para builds futuras

### Critério de aceite

Com o OMSI 2.3.004 aberto e um ônibus ativo, o NavBR deve mostrar mapa, X/Y/Z, direção e velocidade mudando em tempo real sem Steam API e sem permissão de escrita no processo.

## Fase 2 — GPS local

- [x] Detectar pasta `maps`
- [x] Catalogar mapas com `global.cfg`
- [x] Detectar `whole.roadmap.bmp` / `roadmap.bmp`
- [x] Mostrar quantidade de mapas e roadmaps disponíveis no cliente
- [x] Localizar textos do GPS nos cinco idiomas atuais
- [x] Ler grade de tiles do `global.cfg`
- [x] Transformar GridX/GridY + TileX/TileY em pixels do roadmap para mapas padrão de 300 m/tile
- [x] Renderizar ônibus sobre o roadmap
- [x] Zoom por botões e roda do mouse
- [x] Pan por arraste
- [x] Follow vehicle
- [x] Indicador do ônibus rotacionado pelo heading
- [x] Janela always-on-top
- [x] Ajustar roadmap ao viewport
- [ ] Validar visualmente o marcador e heading em runtime
- [ ] Suportar corretamente mapas com `[worldcoordinates]`
- [ ] Modo heading-up com rotação do mapa inteiro
- [ ] Tratamento visual aprimorado para mapas sem roadmap

## Fase 3 — TTData

- [ ] Parser `Busstops.cfg`
- [ ] Parser `.ttr`
- [ ] Parser `.ttp`
- [ ] Parser `.ttl`
- [ ] Linha/rota selecionada
- [ ] Próxima parada
- [ ] Distância restante
- [ ] ETA
- [ ] Formatação de distância/tempo conforme cultura selecionada

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
- [ ] Códigos de erro de rede independentes de idioma

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

## Localização contínua

Novas telas e funcionalidades devem nascer com chaves de recurso. Traduções não devem ser codificadas dentro de telemetria, TTData, mapas ou protocolo multiplayer. Isso permite adicionar novos idiomas progressivamente sem alterar a lógica do simulador.
