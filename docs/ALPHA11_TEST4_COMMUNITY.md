# Alpha.11 Test 4 — checklist da comunidade

Esta pré-release concentra a modernização visual da janela principal, a correção da velocidade e a primeira versão funcional do **Hardware Cockpit Bridge**.

## Antes de testar

1. feche o OMSI e o NavBR antigos;
2. abra a Test 4;
3. em **Ferramentas avançadas > Diagnóstico técnico**, use **Instalar / atualizar plugin**;
4. reinicie o OMSI depois da atualização do plugin;
5. carregue um mapa, ônibus e uma viagem/rota normalmente.

A atualização do plugin é importante nesta versão porque o `.opl` passou a solicitar também `Velocity` e `haltewunsch`.

## 1. Janela principal / navegação lateral

Verifique em resolução e escala do Windows que você costuma usar:

- a janela abre sem conteúdo cortado;
- a barra lateral possui rolagem vertical quando necessário;
- idioma e **Minimizar para bandeja** continuam acessíveis;
- **Visão geral**, **Navegação**, **Multiplayer**, **Hardware cockpit** e **Ferramentas avançadas** abrem corretamente;
- os cards não aparecem deslocados para colunas/linhas do layout antigo.

## 2. Velocidade

Este é um teste prioritário.

Compare durante aceleração, velocidade constante e frenagem:

- velocímetro do ônibus no OMSI;
- velocidade mostrada pelo NavBR/HUD;
- `speedKph` no preview de **Hardware cockpit**.

A fonte principal agora é o `Tacho` do OMSI em km/h. Groundspeed e o vetor físico permanecem como fallback.

Informe o modelo do ônibus se houver diferença relevante entre o painel do ônibus e o NavBR.

## 3. Hardware Cockpit — USB/Serial

A tela **Hardware cockpit** agora permite:

- listar portas COM;
- selecionar baud rate;
- conectar/desconectar Arduino/ESP32;
- enviar `NAVBR_HW_V1` em JSON Lines;
- atualização aproximada de 5 Hz;
- visualizar o pacote completo no próprio NavBR.

Configuração recomendada: **115200 baud**.

Confira se linha, destino, próxima parada, velocidade e demais campos acompanham o OMSI.

## 4. LED de parada solicitada

A Test 4 lê a variável local `haltewunsch` pelo plugin.

Com um ônibus compatível:

1. gere uma solicitação de parada;
2. confira **Parada solicitada** na tela Hardware cockpit;
3. o pacote deve alternar `stopRequested` para `true`;
4. depois que a solicitação for limpa pelo ônibus, deve retornar para `false`.

O exemplo `NavBR_Hardware_Serial.ino` usa `LED_BUILTIN` para demonstrar esse comportamento.

Alguns ônibus/add-ons usam outra variável para o mesmo recurso. Se não funcionar em um ônibus específico, informe o nome/modelo para criarmos um perfil de alias sem quebrar os demais.

## 5. Rua atual

`currentStreet` agora possui um matcher próprio por mapa, usando `GridX/GridY/TileX/TileY` e um perfil `NavBR.streets.json`.

Sem perfil instalado, o NavBR deve mostrar `—`/`null`; isso é esperado. Ele não usa o nome técnico da spline como se fosse nome real de rua.

Consulte `HARDWARE_COCKPIT.md` para o formato do perfil e os locais suportados.

## 6. Plugin Bridge v2

No diagnóstico técnico confira:

- plugin instalado;
- bridge conectado quando o OMSI está em jogo;
- heartbeat não fica `STALE` durante uso normal;
- o NavBR continua funcionando se o Hardware Cockpit não for usado.

## 7. Regressões importantes

Também confirme que continuam funcionando:

- HUD/minimapa sobre o OMSI;
- esconder HUD em menus/janelas auxiliares;
- rota e próxima parada;
- multiplayer, chat e voz;
- minimização para bandeja;
- instalação/atualização do plugin.

## Arquivos para Arduino/ESP32

- documentação: `HARDWARE_COCKPIT.md`
- exemplo: `NavBR_Hardware_Serial.ino`

O transporte Wi-Fi/ESP32 e controles físicos de entrada para o OMSI ficam para fases posteriores. A saída Serial desta Test 4 permanece somente leitura do simulador.
