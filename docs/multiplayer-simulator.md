# NavBR Multiplayer Simulator

Ferramenta de desenvolvimento para testar salas sem precisar abrir várias instâncias do OMSI.

Ela é um projeto separado do cliente de produção. Os jogadores simulados entram no **SignalR real** do NavBR e publicam os mesmos contratos de presença, telemetria e Personagem/RP usados pelo multiplayer.

## Teste rápido

Com um host NavBR aberto em `http://127.0.0.1:27730`:

```powershell
.\scripts\run-multiplayer-simulator.ps1 -Players 8 -Mode mixed
```

Isso cria oito participantes. Em `mixed`, parte deles envia ônibus em movimento e parte envia Personagem/RP.

## Usar o mapa real carregado no OMSI

Em uma sala real, o simulador primeiro tenta sincronizar automaticamente o jogador de referência/autoridade: mapa, fingerprint do mapa, posição, linha/rota e identidade física do ônibus quando disponíveis.

O override manual abaixo continua útil para teste isolado:

```powershell
.\scripts\run-multiplayer-simulator.ps1 `
  -Players 8 `
  -Mode mixed `
  -Map "Nome real do mapa" `
  -X 120 -Y 80 -Z 0 `
  -Radius 65
```

Se o teste exigir o identificador exato do build do mapa, use `-MapId`.

## Modos

- `vehicles`: todos os players publicam ônibus percorrendo trajetórias.
- `rp`: todos publicam personagens alternando Parado, A pé e Correndo.
- `mixed`: mistura ônibus e RP na mesma sala.

O simulador altera posições continuamente. Os ônibus variam heading/velocidade e os personagens RP se deslocam de verdade entre frames.

## Verificação automática

```powershell
.\scripts\run-multiplayer-simulator.ps1 -Players 6 -Mode mixed -Duration 10 -Verify
```

O modo `-Verify` adiciona um cliente-observador à sala e falha se os frames não atravessarem o servidor ou se os participantes não apresentarem deslocamento mensurável.

O CI da Alpha.14 executa esse cenário automaticamente antes de compilar o interop/plugin.

## Limite do simulador

A ferramenta valida rede, sala, estados, atualização de posição, mapa da Central, marcadores e RP remoto. Ela **não substitui o teste final dentro do OMSI** para o personagem humano local, porque o detach/restore do motorista e a escrita física dependem da memória real do OMSI 2.3.004.


## Identidade física atual

No teste físico, o simulador prefere o mesmo `Vehicles\...\.bus/.ovh` herdado do jogador real e calcula o mesmo SHA-256 usado pela telemetria do cliente. Ele também tenta localizar a instalação pelo `Omsi.exe` atualmente em execução, o que cobre bibliotecas Steam personalizadas.

`--verify-physical` continua fail-closed: sem mapa real, GridX/GridY, caminho de veículo e fingerprint válidos, o teste não declara sucesso.
