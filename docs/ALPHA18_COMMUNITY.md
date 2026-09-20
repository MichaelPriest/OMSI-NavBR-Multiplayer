# Alpha.18 — validação pública / public validation

A Alpha.18 foi liberada para ampliar os testes antes de declarar o multiplayer como validado.

## Testar em dois PCs reais

Testar separadamente **LAN/local**, **Servidor NavBR online** e **Host pela Internet**. Verificar entrada na sala, presença, telemetria, chat, voz, reconexão e saída limpa.

## Ônibus físico

Confirmar que o ônibus local nunca é reutilizado como remoto, que cada remoto recebe ponteiro próprio, se aparece no mundo 3D, posição/rotação/movimento e despawn.

## Plugin

Em **Configurações > Instalações**, confirmar 3/3 arquivos encontrados, 3/3 SHA-256 OK, atualização com OMSI fechado e atualização pendente com OMSI aberto.

## Logs

Enviar `navbr.log`, `navbr-plugin.log`, `navbr-error.log` e `navbr-route.log`. Procurar `physical-spawn before`, `physical-spawn after`, `physical-spawn assigned` e `physical-render-confirm`.

LAN/online só serão considerados validados após testes reproduzíveis com **dois PCs/duas sessões reais do OMSI**.
