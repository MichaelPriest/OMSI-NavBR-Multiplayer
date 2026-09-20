# Estado real do multiplayer — Alpha.18

> **Status público:** implementação ativa, **ainda não validada ponta a ponta** em LAN/local nem online entre dois PCs/duas sessões reais do OMSI.

## Já implementado

Salas, SignalR, presença, telemetria, chat/voz, Servidor NavBR, LAN, Host pela Internet, mapa/HUD remoto, compatibilidade de mapa/veículo/HOF, Plugin Bridge v3, interop x86, pipeline experimental de ônibus físico, simulador e verificador/autoatualizador do plugin.

## Validado automaticamente

O CI valida cliente, servidor, interop, Native AOT, exports, instalação/remoção do plugin, bundle embutido, bridge, simulador e instalador/desinstalador. **Isso não comprova uma sessão multiplayer real entre dois computadores.**

## Ainda precisa de validação real

- **LAN/local:** dois PCs na mesma rede;
- **Servidor NavBR online:** dois PCs em redes reais;
- **Host pela Internet:** Firewall/UPnP/NAT/CGNAT reais;
- **ônibus físico:** visibilidade, posição e movimento consistentes em dois OMSI reais.

## Ônibus físico remoto

A Alpha.18 contém spawn físico experimental, mas ele **não deve ser descrito como concluído**. O runtime registra `hostVehiclePointer`, compara `RoadVehicles` antes/depois de `MakeVehicle`, exclui o ônibus local, aceita apenas ponteiros novos/válidos e lê visibilidade lógica/render-thread, ComplObj/model, Kachel e matriz de render.

## Como reportar

Informe versão, modo (LAN/Servidor NavBR/Host), quantidade de PCs, mapa/ônibus/HOF, se o remoto apareceu no app, se apareceu fisicamente no OMSI, se houve movimento e envie `navbr.log` e `navbr-plugin.log`.

Procure por `physical-spawn before`, `physical-spawn after`, `physical-spawn assigned` e `physical-render-confirm`.

Enquanto os testes reais não forem concluídos, usar **experimental**, **em validação** ou **alpha pública de teste**.
