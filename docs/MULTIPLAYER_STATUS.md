# Estado real do multiplayer — Alpha.21

> **Status público:** o caminho online com **bots/AI do simulador seguindo o host foi validado com sucesso em OMSI real**. A sincronização ponta a ponta entre **players reais em dois PCs/duas sessões OMSI ainda precisa ser testada**.

## Já implementado

Salas, SignalR, presença, telemetria, chat/voz, Servidor NavBR, LAN, Host pela Internet, mapa/HUD remoto, compatibilidade de mapa/veículo/HOF, Plugin Bridge v3, state interop ABI v20, pipeline experimental de ônibus físico, simulador, NavBR TP/TS, Mobile Companion e verificador/autoatualizador do plugin.

## Validado automaticamente

O CI valida cliente, servidor, interop x86, Native AOT, exports, instalação/remoção do plugin, bundle embutido, Plugin Bridge, simulador, installer/uninstaller, PWA e APK.

Esses gates confirmam build/protocolo, mas não substituem teste humano em duas instalações OMSI independentes.

## Validado em teste prático

### Simulador / AI online

**VALIDADO nesta etapa:**

- bots/AI conectados ao fluxo online;
- materialização dos ônibus simulados no OMSI real;
- posição/trajetória útil com a simulação ativa;
- comboio seguindo o host em vez de permanecer correto somente com o OMSI pausado;
- caminho de vehicle-path usado pelo simulador para manter os remotos na via.

Esse resultado confirma que o pipeline físico do simulador consegue chegar ao OMSI e acompanhar a autoridade da sessão.

## Ainda precisa de validação real

### Players reais

A próxima validação obrigatória é feita com **dois jogadores reais**:

- dois PCs;
- duas sessões independentes do OMSI;
- mapa compatível em ambos;
- ônibus compatível disponível localmente;
- recurso de ônibus remoto físico habilitado;
- preferencialmente teste tanto pelo Servidor NavBR quanto por LAN.

Precisamos confirmar:

- jogador A aparece fisicamente para jogador B;
- jogador B aparece fisicamente para jogador A;
- posição, heading e movimento permanecem corretos durante condução real;
- troca de Kachel não produz teleporte/despawn;
- parada/retomada do OMSI não altera a autoridade da pose;
- estabilidade em cruzamentos e curvas;
- comportamento de luzes/setas/estado visual;
- reconexão e despawn limpo.

## Ônibus físico remoto

O recurso continua **experimental**, apesar do teste de AI bem-sucedido. O NavBR mantém ownership externo do RoadVehicle remoto e sincroniza os campos/matrizes necessários para impedir que o frame ativo do OMSI recalcule a pose para outro local.

Estruturas internas `PathInfo` não documentadas continuam sem escrita direta.

Ônibus articulados ainda não são considerados validados por esse caminho.

## Como reportar o teste com players reais

Informe:

- versão/commit do NavBR;
- modo: Servidor NavBR, LAN ou Host pela Internet;
- quantidade de PCs;
- mapa, ônibus e HOF;
- se o jogador remoto apareceu no app;
- se apareceu fisicamente no OMSI;
- se acompanhou movimento/curvas;
- se houve desaparecimento ao despausar;
- `navbr.log`;
- `navbr-plugin.log`;
- `navbr-error.log`, se existir.

Até concluir o teste com jogadores reais, usar as descrições **experimental**, **AI/simulador validado** e **players reais pendentes de validação**.
