# Arquitetura inicial

## Princípios

1. **Sem dependência da Steam**: o cliente encontra `Omsi.exe` em execução e deriva a instalação pelo caminho do processo.
2. **Integração desacoplada**: telemetria, mapa, navegação e multiplayer são módulos separados.
3. **Compatibilidade por perfil**: cada build suportada do OMSI terá um perfil de leitura/assinaturas próprio.
4. **Sem DRM bypass**: o projeto não modifica ativação/licenciamento do OMSI.
5. **Servidor independente do simulador**: o backend recebe apenas snapshots do cliente NavBR.

## Fluxo

```text
Omsi.exe
  │
  ├─ Process detector
  ├─ Version detector
  └─ Telemetry provider
         │
         ▼
   NavBR.Client
     ├─ Map reader
     ├─ TTData reader
     ├─ Navigation engine
     ├─ GPS / overlay
     └─ Multiplayer client
              │
              ▼
        NavBR.Server
          └─ SignalR rooms
              │
        ┌─────┴─────┐
        ▼           ▼
     Player A    Player B
```

## Cliente

O cliente será WPF em `net10.0-windows`, inicialmente compilado em x86 para acompanhar o OMSI 2, que é um processo de 32 bits.

### Camadas previstas

- `OmsiProcessDetector`: encontra PID, executável, versão e pasta da instalação.
- `ICompatibilityProfile`: identifica uma build conhecida.
- `ITelemetryProvider`: abstrai leitura de memória/plugin.
- `MapReader`: `global.cfg`, tiles, roadmap e conversão tile/local -> mapa.
- `TtDataReader`: `Busstops.cfg`, `.ttr`, `.ttp`, `.ttl`.
- `NavigationEngine`: próxima parada, progresso da viagem, distância e ETA.
- `MultiplayerClient`: sala, presença e telemetria remota.
- `Overlay`: janela sempre no topo / modo GPS.

## Telemetria

O primeiro backend de telemetria será um leitor externo do processo. A implementação deve usar detecção por versão e, quando necessário, signature/pattern scanning em vez de depender apenas de offsets fixos.

A API interna do cliente não deve conhecer offsets diretamente. Exemplo:

```csharp
public interface ITelemetryProvider
{
    bool IsAvailable { get; }
    VehicleTelemetry? Read();
}
```

Assim podemos adicionar posteriormente um provider baseado em plugin oficial sem reescrever GPS e multiplayer.

## Multiplayer

A fase inicial usa SignalR/WebSocket.

Cada cliente envia snapshots como:

- player id;
- map id/hash;
- vehicle id;
- line/route;
- X/Y/Z;
- heading;
- speed;
- timestamp.

O servidor distribui somente aos demais jogadores da mesma sala.

### Fase posterior

A representação de veículos remotos **dentro do OMSI** será tratada separadamente. Até provarmos uma integração estável, multiplayer significa jogadores sincronizados no NavBR/overlay, não injeção de ônibus no mundo do jogo.

## Compatibilidade de mapas

A sala deve usar um fingerprint do mapa para impedir que dois jogadores com versões incompatíveis do mesmo mapa sejam colocados na mesma sessão sem aviso.

Fingerprint futuro sugerido:

```text
SHA-256(
  global.cfg +
  sorted tile names +
  TTData manifest
)
```
