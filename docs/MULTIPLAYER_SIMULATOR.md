# Simulador Multiplayer NavBR

Ferramenta **somente de desenvolvimento/teste** para validar sala, presença, telemetria, RP e movimento sem abrir várias instâncias do OMSI.

Os dados simulados nunca substituem a telemetria da interface de produção.

## Comportamento padrão atual

Quando executado contra uma sala real, o simulador:

1. entra na mesma sala;
2. detecta um jogador real/autoridade com mapa carregado;
3. herda `MapName`, `MapCompatibilityId` e o protocolo real da sessão;
4. aguarda telemetria real do host;
5. herda, quando disponíveis, **caminho e fingerprint SHA-256 do ônibus, HOF e identidade OMSI** do jogador de referência;
6. usa a posição real como centro;
7. cria os bots próximos ao host, raio padrão de **18 m**;
8. herda **linha, rota, destino e próxima parada** da operação ativa;
9. só então começa a publicar movimento.

Se não houver mapa/posição real e nenhum fallback explícito for informado, o simulador aguarda em vez de criar bots em 0,0 ou em outro mapa. Linha/rota não são obrigatórias para provar o spawn físico.

## Uso rápido

Com o NavBR já hospedando uma sala:

    .\run-multiplayer-simulator.ps1 -Room SUA-SALA -Players 6 -Mode mixed

Para sala privada:

    .\run-multiplayer-simulator.ps1 -Room SUA-SALA -RoomPassword SUA-SENHA -Players 6

Para validar especificamente os ônibus físicos com apenas **um OMSI real**, o pacote de teste também inclui:

- `run-physical-online.cmd`: usa o servidor oficial NavBR e cria 3 ônibus simulados;
- `run-physical-local.cmd`: usa/abre o servidor local e cria 3 ônibus simulados.

Fluxo recomendado: abra o OMSI e o NavBR, entre na sala `navbr-physical-test`, mantenha o mapa e um ônibus rígido carregados e então execute um desses atalhos. O teste só aprova quando os três bots forem materializados fisicamente no OMSI e, ao final, também forem removidos corretamente.

Não é necessário informar --map quando existe um host real na sala. O mapa da sala tem prioridade.

## Servidor local automático

Se http://127.0.0.1:27730 estiver vazio e o pacote incluir a pasta server, o simulador inicia o NavBR.Server automaticamente.

Se o app já estiver hospedando a porta 27730, o simulador reutiliza o host existente.

## Overrides

- --map / --map-id: fallback para teste isolado; uma sala real tem prioridade;
- --x --y --z: centro explícito;
- --grid-x --grid-y --tile-x --tile-y: seed de navegação explícito;
- --radius: raio de movimento; padrão 18 m;
- --vehicle-path / --vehicle-id: identidade real de veículo para teste físico;
- --line / --route / --destination / --next-stop: fallback operacional opcional para teste isolado;
- --verify-physical: exige um cliente NavBR real com OMSI carregado e só aprova quando os IDs dos bots são confirmados como ônibus materializados;
- --omsi-root: raiz opcional do OMSI para resolver o EN92 padrão e rotas do HOF;
- --map-tile-index: Kachel explícita para teste físico isolado;
- --password: senha de sala privada;
- --no-auto-server: desabilita auto-start do servidor local.

## Verificação automática

O modo --verify cria um probe na mesma sala e falha se:

- um bot não produzir múltiplos frames;
- o deslocamento for menor que 0,25 m;
- um bot publicar em mapa diferente do mapa resolvido;
- no modo mixed não houver frames de veículo e RP.

O CI também executa esse caminho sem OMSI usando um NavBR.Server local empacotado, quatro bots e modo `mixed`. Isso valida automaticamente servidor, SignalR, sala, presença e telemetria antes do teste físico.

No modo `--verify-physical`, a validação agora cobre o ciclo completo do ônibus remoto: o host só publica o bot como físico depois que `MakeVehicle` e a materialização visual do OMSI foram confirmados; quando os bots saem da sala, o simulador também exige que os respectivos IDs desapareçam do conjunto físico do host em até 8 segundos. Assim, um ônibus órfão/fantasma após desconexão também reprova o teste.

## Limites

- até 32 bots por execução;
- intervalo mínimo de 100 ms;
- `--verify` valida rede/UI sem OMSI; `--verify-physical` faz a prova real de materialização dentro do OMSI;
- o simulador não inventa assets de ônibus proprietários;
- câmera, terreno e animações RP continuam exigindo teste real.


## Validação de telemetria física avançada

Os ônibus simulados alternam de forma determinística acelerador, freio, combustível, iluminação externa/interna, luz de freio e setas/pisca-alerta. O probe automático considera falha quando movimento chega, mas esses estados não atravessam o pipeline multiplayer.


## Alpha.15 — validação física por grid / Alpha.15 — grid-based physical validation

### Português (pt-BR)

O modo `--verify-physical` exige `GridX/GridY` reais herdados de um jogador NavBR com OMSI carregado. O índice `MapTileIndex` do host não é reutilizado como identidade remota; cada OMSI resolve sua própria Kachel local a partir do grid.

### English (en)

`--verify-physical` requires real `GridX/GridY` inherited from a NavBR player with OMSI loaded. The host's `MapTileIndex` is not reused as a remote identity; each OMSI process resolves its own local Kachel from the grid.


## Alpha.16 — identidade física real do simulador / real physical simulator identity

### Português (pt-BR)

Para validação física, o simulador não anuncia mais uma identidade genérica. Quando há um jogador real na sala, ele reutiliza apenas fatos observados da sessão: mapa/fingerprint, protocolo, caminho e fingerprint do veículo e HOF quando disponíveis.

A resolução local do veículo agora prefere o mesmo `.bus`/`.ovh` herdado da sala antes do ônibus padrão de teste. A raiz do OMSI também pode ser descoberta diretamente pelo `Omsi.exe` em execução, incluindo Steam Libraries personalizadas fora de `Program Files`.

Isso mantém o fingerprint usado pelo simulador igual ao calculado pelo cliente real (`sha256:<arquivo .bus/.ovh>`) e permite que o coordenador físico chegue à etapa de `MakeVehicle` sem inventar assets.

Para `--verify-physical`, a identidade herdada só é reutilizada quando a definição é realmente **single-part/rígida**. Se o ônibus atual declarar outro veículo por `[couple_front]` ou `[couple_back]` (por exemplo, articulados), o simulador procura um ônibus rígido padrão realmente instalado. Isso evita um teste falso que seria recusado depois pelo coordenador como `consist-unsupported`.

### English (en)

For physical validation, the simulator no longer advertises a generic identity. When a real player is present in the room, it reuses only observed session facts: map/fingerprint, protocol, vehicle path/fingerprint, and HOF when available.

Local vehicle resolution now prefers the same inherited `.bus`/`.ovh` before the stock test bus. The OMSI root can also be discovered from the currently running `Omsi.exe`, including custom Steam Libraries outside `Program Files`.

This keeps the simulator fingerprint aligned with the real client (`sha256:<.bus/.ovh file>`) so the physical coordinator can reach the guarded `MakeVehicle` stage without inventing assets.

For `--verify-physical`, an inherited identity is reused only when the definition is truly **single-part/rigid**. If the current bus declares another vehicle through `[couple_front]` or `[couple_back]` (for example an articulated bus), the simulator looks for a real installed stock rigid bus instead. This prevents a false test setup that the physical coordinator would later reject as `consist-unsupported`.
