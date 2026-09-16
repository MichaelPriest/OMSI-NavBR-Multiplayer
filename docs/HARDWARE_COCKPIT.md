# Hardware Cockpit Bridge

O **Hardware Cockpit Bridge** transforma a telemetria normalizada do NavBR em dados para painéis físicos baseados em Arduino/ESP32.

A fase inicial é **somente saída**: NavBR/OMSI -> hardware. O microcontrolador não acessa a memória do OMSI e não envia comandos ao simulador nesta etapa.

## Transporte USB / Serial

Configuração padrão recomendada:

- protocolo: `NAVBR_HW_V1`
- formato: JSON Lines (um objeto JSON por linha)
- baud rate padrão: `115200`
- frequência: aproximadamente `5 Hz`
- serial: 8 data bits, sem paridade, 1 stop bit, sem handshake
- encoding: UTF-8 sem BOM

No NavBR, abra **Hardware cockpit**, escolha a porta COM e o baud rate e clique em **Conectar**.

O próprio painel mostra a telemetria ao vivo e um preview formatado do pacote. Na porta serial o pacote é enviado de forma compacta, terminado por `\n`.

## Campos do NAVBR_HW_V1

O quadro pode conter:

- `protocol`
- `timestamp`
- `map`
- `vehicle`
- `line`
- `route`
- `destination`
- `currentStreet`
- `nextStop`
- `currentStopIndex`
- `stopRequested`
- `speedKph`
- `delaySeconds`
- `throttlePercent`
- `brakePercent`
- `doors`
- `lights`
- `turnSignal`
- `hornActive`
- `wipersActive`
- `parkingBrakeActive`
- `reverseGear`

Campos ainda não disponíveis para um mapa/ônibus podem ser enviados como `null` ou permanecer em seu valor neutro.

## Velocidade

`speedKph` usa a mesma velocidade normalizada do NavBR/HUD. Na Alpha.11 a leitura principal usa o valor de velocímetro `Tacho` do OMSI em km/h, com fallback para Groundspeed e vetor físico de velocidade.

O plugin também observa a variável `Velocity` como fonte de diagnóstico, permitindo comparar o valor exposto pelo script do ônibus com a telemetria principal sem alterar o simulador.

## Parada solicitada

O plugin solicita a variável local `haltewunsch` e a converte para `stopRequested`.

Quando `stopRequested=true`, um painel físico pode acender a lâmpada/LED de **PARADA SOLICITADA**. O estado é enviado pelo bridge em baixa latência e incorporado ao pacote serial.

O valor é considerado válido somente enquanto o OMSI continua entregando callbacks recentes dessa variável. Assim, ao trocar para um ônibus que não expõe `haltewunsch`, o estado anterior não fica preso no bridge.

Nem todo ônibus/add-on é obrigado a usar exatamente o mesmo nome de variável. O suporte inicial cobre `haltewunsch`; perfis/aliases específicos de ônibus poderão ser adicionados quando necessário.

## Exemplo Arduino / ESP32

O exemplo mínimo está em:

`examples/NavBR.Hardware.Serial/NavBR_Hardware_Serial.ino`

Ele não exige biblioteca externa: recebe os frames a 115200 baud e controla `LED_BUILTIN` a partir de `stopRequested`.

Fluxo de teste:

1. grave o sketch no Arduino/ESP32;
2. feche o Serial Monitor/Plotter para liberar a porta COM;
3. abra o NavBR;
4. entre no OMSI e carregue o ônibus;
5. abra **Hardware cockpit**;
6. selecione a COM do microcontrolador;
7. mantenha `115200` baud;
8. clique em **Conectar**;
9. faça uma solicitação de parada no ônibus e confira o LED.

Algumas placas reiniciam ao abrir a porta serial. Nesse caso, os primeiros frames podem ser ignorados enquanto a placa reinicia; o streaming continua automaticamente.

## Rua atual

OMSI fornece a geometria dos caminhos/splines, mas não há um campo padrão confiável de nome amigável da rua para todos os mapas. Por isso o NavBR não transforma automaticamente o nome técnico de um `.sli` em nome de rua.

A Alpha.11 suporta perfis `NavBR.streets.json`. O resolver compara a posição/tile atual do ônibus com os segmentos nomeados do perfil e usa o mais próximo dentro da tolerância configurada.

O perfil pode ficar em um destes locais:

- `%LOCALAPPDATA%\OMSI NavBR Multiplayer\StreetProfiles\<pasta-do-mapa>.json` — tem prioridade e não modifica o mapa;
- `<pasta-do-mapa>\NavBR.streets.json` — útil quando o próprio perfil acompanha a distribuição do mapa.

Exemplo:

```json
{
  "version": 1,
  "maxDistanceMeters": 35,
  "streets": [
    {
      "name": "Av. Santo Amaro",
      "points": [
        { "gridX": 0, "gridY": 0, "tileX": 42.5, "tileY": 18.2 },
        { "gridX": 0, "gridY": 0, "tileX": 96.1, "tileY": 85.4 },
        { "gridX": 0, "gridY": 1, "tileX": 112.0, "tileY": 12.7 }
      ]
    }
  ]
}
```

Os pontos usam o mesmo sistema `GridX/GridY/TileX/TileY` da telemetria do NavBR. Quando nenhum segmento do perfil estiver próximo o suficiente, `currentStreet` permanece `null`/`—`.

O objetivo é permitir perfis confiáveis para mapas reais e fictícios sem depender de reverse geocoding externo.

## Próximas etapas

- gerar/importar perfis `NavBR.streets.json` para mapas suportados;
- adicionar perfis de variáveis para ônibus que não usam `haltewunsch`;
- transporte Wi-Fi para ESP32 por UDP/WebSocket;
- exemplos para OLED/LCD/matriz de LED e letreiro dianteiro/lateral/traseiro;
- fase bidirecional opcional para botões físicos, mantendo escrita no OMSI isolada e explicitamente habilitada.
