# Hardware Cockpit Bridge

O Hardware Cockpit transforma a telemetria normalizada do NavBR em dados para Arduino/ESP32, letreiros, LEDs e computadores de bordo.

A fase atual é somente saída: **NavBR/OMSI → hardware**.

## Transporte Serial

- protocolo: \`NAVBR_HW_V1\`;
- JSON Lines;
- padrão: 115200 baud;
- aproximadamente 5 Hz;
- 8N1, sem handshake;
- UTF-8 sem BOM.

Na Alpha.14, React e WPF compartilham **uma única** instância de \`HardwareCockpitBridgeController\`. Não existem duas portas COM concorrentes.

O tick oficial de telemetria do MainWindow (200 ms) publica os frames, portanto o streaming continua mesmo quando a tela Hardware não está aberta.

## Configuração

Na interface React, abra **Hardware Cockpit**:

1. escolha a COM;
2. escolha o baud;
3. habilite auto-reconnect se desejar;
4. clique em **Conectar hardware**.

COM/baud são persistidos. Auto-reconnect tenta apenas a mesma porta explicitamente escolhida e nunca troca silenciosamente para outro dispositivo.

O shell WPF continua disponível como fallback técnico e usa o mesmo controlador compartilhado.

## Campos do NAVBR_HW_V1

Inclui protocol, timestamp, map, vehicle, line, route, destination, currentStreet, nextStop, currentStopIndex, stopRequested, speedKph, delaySeconds, throttlePercent, brakePercent, doors, lights, turnSignal, hornActive, wipersActive, parkingBrakeActive e reverseGear.

Campos indisponíveis permanecem null/neutros; não são inventados.

## Parada solicitada

O plugin lê \`haltewunsch\` quando disponível e converte para \`stopRequested\`. O valor só é aceito enquanto os callbacks forem recentes, evitando estado preso ao trocar de veículo.

## Rua atual

O NavBR pode usar perfis \`NavBR.streets.json\`, em:

- \`%LOCALAPPDATA%\\OMSI NavBR Multiplayer\\StreetProfiles\\<mapa>.json\`;
- \`<pasta-do-mapa>\\NavBR.streets.json\`.

Sem segmento confiável dentro da tolerância, \`currentStreet\` fica null/—.

## Exemplo Arduino/ESP32

Exemplo: \`examples/NavBR.Hardware.Serial/NavBR_Hardware_Serial.ino\`.

Fluxo:
1. grave o sketch;
2. feche Serial Monitor;
3. abra NavBR/OMSI;
4. carregue o ônibus;
5. abra Hardware Cockpit;
6. escolha a COM e 115200;
7. conecte;
8. solicite parada e confira o LED.

## Próximas etapas

- mais perfis de variáveis por ônibus;
- Wi-Fi/ESP32 UDP/WebSocket;
- exemplos OLED/LCD/matriz;
- fase bidirecional opcional, mantendo escrita no OMSI isolada e opt-in.
