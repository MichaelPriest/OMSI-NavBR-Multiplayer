# Simulador Multiplayer NavBR

Ferramenta **somente de desenvolvimento/teste** para validar a Central Multiplayer, SignalR, presença, telemetria, RP e movimento sem abrir várias instâncias do OMSI.

Os dados gerados pelo simulador nunca são usados pelo cliente de produção. O executável fica em `src/NavBR.MultiplayerSimulator`.

## Teste rápido da Central

Com uma sala NavBR rodando localmente em TCP 27730:

```powershell
dotnet run --project src/NavBR.MultiplayerSimulator -- `
  --server http://127.0.0.1:27730 `
  --room navbr-sim `
  --players 8 `
  --mode mixed `
  --radius 80
```

Modos:

- `vehicles`: todos os bots enviam telemetria de ônibus;
- `rp`: todos enviam estado Personagem/RP;
- `mixed`: mistura ônibus e RP.

Os bots se movem continuamente. No modo RP, os estados alternam entre **Parado**, **A pé** e **Correndo**.

## Verificação automática do movimento

```powershell
dotnet run --project src/NavBR.MultiplayerSimulator -- `
  --server http://127.0.0.1:27730 `
  --room navbr-sim `
  --players 6 `
  --mode mixed `
  --duration 10 `
  --verify
```

O modo `--verify` cria um probe SignalR na mesma sala e falha se os jogadores não produzirem múltiplos frames com deslocamento mensurável.

O CI da Alpha.14 executa esse teste automaticamente.

## Teste no mesmo mapa real do OMSI

Para testar compatibilidade com a viagem carregada:

```powershell
dotnet run --project src/NavBR.MultiplayerSimulator -- `
  --server http://127.0.0.1:27730 `
  --room SUA-SALA `
  --players 6 `
  --mode mixed `
  --map "NOME REAL DO MAPA" `
  --x 120 --y 80 --z 0 `
  --radius 60
```

Use o nome real reportado pela telemetria do NavBR.

## HUD / minimapa real

O HUD usa a posição de navegação do OMSI. Para fazer os bots aparecerem no minimapa, informe também a posição base real da viagem:

```powershell
  --grid-x 0 --grid-y 0 --tile-x 150 --tile-y 150
```

O simulador aplica o deslocamento dos bots sobre `TileX/TileY`. Use valores reais da sessão; não invente esses valores para testes de compatibilidade.

## Ônibus físico

É possível informar identidade real de um ônibus instalado:

```powershell
  --vehicle-path "Vehicles\\Modelo\\onibus.bus" `
  --vehicle-id "COMPATIBILITY-ID-REAL"
```

Isso serve para testes controlados do pipeline físico. O simulador não inventa um veículo de produção nem substitui a necessidade de validar o spawn no OMSI.

## Limites

- máximo padrão: 32 bots por execução;
- intervalo mínimo: 100 ms;
- o simulador valida rede/UI, não substitui o teste do interop dentro do processo do OMSI;
- câmera, terreno, animações e interação física do Personagem/RP ainda exigem teste no simulador real.
