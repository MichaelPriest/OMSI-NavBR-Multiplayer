# Simulador Multiplayer NavBR

Ferramenta **somente de desenvolvimento/teste** para validar sala, presença, telemetria, RP e movimento sem abrir várias instâncias do OMSI.

Os dados simulados nunca substituem a telemetria da interface de produção.

## Comportamento padrão na Alpha.14

Quando executado contra uma sala real, o simulador:

1. entra na mesma sala;
2. detecta um jogador real/autoridade com mapa carregado;
3. herda MapName e MapCompatibilityId;
4. aguarda telemetria real do host;
5. usa a posição real como centro;
6. cria os bots próximos ao host, raio padrão de **18 m**;
7. herda **linha, rota, destino e próxima parada** da operação ativa;
8. só então começa a publicar movimento.

Se não houver mapa/posição/rota real e nenhum fallback explícito for informado, o simulador aguarda em vez de criar bots em 0,0 ou em outro mapa.

## Uso rápido

Com o NavBR já hospedando uma sala:

    .\run-multiplayer-simulator.ps1 -Room SUA-SALA -Players 6 -Mode mixed

Para sala privada:

    .\run-multiplayer-simulator.ps1 -Room SUA-SALA -RoomPassword SUA-SENHA -Players 6

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
- --line / --route / --destination / --next-stop: fallback operacional para teste isolado;
- --password: senha de sala privada;
- --no-auto-server: desabilita auto-start do servidor local.

## Verificação automática

O modo --verify cria um probe na mesma sala e falha se:

- um bot não produzir múltiplos frames;
- o deslocamento for menor que 0,25 m;
- um bot publicar em mapa diferente do mapa resolvido;
- no modo mixed não houver frames de veículo e RP.

## Limites

- até 32 bots por execução;
- intervalo mínimo de 100 ms;
- o simulador valida rede/UI e não substitui teste físico dentro do OMSI;
- o simulador não inventa assets de ônibus proprietários;
- câmera, terreno e animações RP continuam exigindo teste real.
