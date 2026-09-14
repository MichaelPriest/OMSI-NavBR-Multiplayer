# Arquitetura inicial

## Princípios

1. **Sem dependência da Steam**: o cliente encontra `Omsi.exe` em execução e deriva a instalação pelo caminho do processo.
2. **Integração desacoplada**: telemetria, mapa, navegação e multiplayer são módulos separados.
3. **Compatibilidade por perfil**: cada build suportada do OMSI terá um perfil de leitura/assinaturas próprio.
4. **Sem DRM bypass**: o projeto não modifica ativação/licenciamento do OMSI.
5. **Servidor independente do simulador**: o backend recebe apenas snapshots do cliente NavBR.
6. **Localização desde a base**: nenhum módulo de domínio deve depender de texto de interface; idiomas são tratados no cliente por chaves de recurso.

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
     ├─ Localization
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
- `LocalizationService`: detecta idioma, carrega recursos e persiste a preferência do usuário.
- `MapReader`: `global.cfg`, tiles, roadmap e conversão tile/local -> mapa.
- `TtDataReader`: `Busstops.cfg`, `.ttr`, `.ttp`, `.ttl`.
- `NavigationEngine`: próxima parada, progresso da viagem, distância e ETA.
- `MultiplayerClient`: sala, presença e telemetria remota.
- `Overlay`: janela sempre no topo / modo GPS.

## Internacionalização / localização

A interface usa `ResourceManager` e arquivos `.resx` no namespace `NavBR.Client.Resources`.

Idiomas iniciais:

```text
pt-BR  Português (Brasil)
en-US  English
es-ES  Español
de-DE  Deutsch
fr-FR  Français
```

Regras:

- detectar o idioma do Windows na primeira execução;
- usar inglês como fallback;
- permitir troca de idioma em tempo real;
- salvar a preferência em `%LOCALAPPDATA%\OMSI NavBR Multiplayer\language.txt`;
- não colocar textos de UI dentro de providers de telemetria, parsers, DTOs ou protocolo multiplayer;
- mensagens do servidor devem usar códigos/erros estruturados; a tradução final pertence ao cliente;
- novas telas só devem introduzir texto através de chaves de recurso.

Isso permite que GPS, multiplayer, configurações e mensagens de compatibilidade recebam traduções sem alterar a lógica principal.

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

O protocolo de rede não transporta frases traduzidas como estado principal. Sempre que possível, usa códigos e valores estruturados para que cada cliente possa renderizar a mensagem no idioma selecionado localmente.

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
