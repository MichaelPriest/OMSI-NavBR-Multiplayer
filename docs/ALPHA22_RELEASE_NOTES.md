# OMSI NavBR Multiplayer v0.3.0-alpha.22

## Português (Brasil)

A Alpha.22 promove para a linha pública as correções do multiplayer físico validadas após a Alpha.21.

### Marco de validação

Foi concluído com sucesso um teste online usando **bots/AI do simulador** conectados ao fluxo real do NavBR/OMSI:

- os ônibus físicos simulados foram materializados no OMSI;
- com a simulação ativa, passaram a seguir corretamente o host pela trajetória;
- o fluxo simulador → rede → cliente → plugin → RoadVehicle foi confirmado funcional;
- o comportamento que antes só aparecia corretamente com o OMSI pausado foi corrigido no cenário de AI/simulador testado.

Esse resultado **não equivale ainda à validação com players reais**. A próxima etapa é testar dois PCs/duas sessões independentes do OMSI, em ambos os sentidos.

### Multiplayer físico / RoadVehicle

- state interop **ABI v20**;
- ownership externo do RoadVehicle reforçado;
- sincronização de `Velocity` e `Last_Velocity`;
- sincronização de `Used_RelVec`;
- `RelMatrix` / `RelMatrixVar` tratados separadamente de `Pos_Mat`;
- `AbsPosition_Inv` mantida junto da matriz absoluta;
- corpo ODE remoto mantido sob autoridade do NavBR;
- proteção contra escrita em ponteiros/matrizes ainda não materializados;
- diagnóstico de posição visual × corpo físico preservado para investigação.

### HUD

- overlay deixa de usar topmost global;
- HUD passa a ser associado ao HWND de gameplay do OMSI;
- outras aplicações não devem mais ficar cobertas pelo HUD;
- menus/diálogos separados do OMSI não são tratados automaticamente como gameplay;
- hotkeys permanecem vinculadas ao contexto real do OMSI.

### Performance

- trabalho de ownership/ODE por callback foi limitado;
- transform completo só é reaplicado quando necessário;
- keepalive evita gravações ODE redundantes;
- diagnósticos físicos/path foram reduzidos e espaçados;
- verificação de plugin ganhou cache;
- perfis/preferências/histórico/HOF/roadmap reduziram I/O síncrono repetitivo;
- timers visuais de diagnóstico passaram a prioridade de background quando aplicável.

### Documentação

- novo [manual completo de atalhos](KEYBOARD_SHORTCUTS.md);
- README, changelog, manual de uso, status multiplayer e documentação do plugin atualizados;
- portal público passa a destacar claramente:
  - **AI/simulador online validado**;
  - **players reais ainda pendentes de validação**;
- referências públicas usadas na investigação de interop OMSI foram registradas em `THIRD_PARTY_NOTICES.md`.

### Ainda precisa de teste real

- player A aparecendo fisicamente para player B;
- player B aparecendo fisicamente para player A;
- movimento/heading durante condução real;
- troca de Kachel;
- curvas e cruzamentos;
- reconexão/despawn;
- luzes/setas/estado visual;
- latência e estabilidade em LAN/Servidor NavBR/Host pela Internet.

Ônibus articulados continuam fora do conjunto físico validado.

## English

Alpha.22 promotes the physical-multiplayer fixes validated after Alpha.21.

The online simulator AI/bot scenario has been validated successfully in a real OMSI session: simulated physical buses materialized and followed the host while the simulation was running. The next required milestone is the same end-to-end validation with **real players on two PCs/two independent OMSI sessions**.

Alpha.22 includes state interop ABI v20, additional RoadVehicle/ODE/matrix synchronization, gameplay-scoped HUD behavior, callback/I/O performance improvements, and updated public documentation.
