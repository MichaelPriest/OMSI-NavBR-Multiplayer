# Plugin OMSI experimental — veículos remotos físicos

> Documento de engenharia da **Alpha.13**. A escrita no OMSI continua experimental, opt-in e limitada ao OMSI **2.3.004** enquanto a implementação passa por testes reais.

## Objetivo atual

A Alpha.13 Test 1 deixa de tratar o ônibus remoto apenas como investigação futura e passa a validar o fluxo completo:

```text
Jogador remoto
   ↓ SignalR / sala NavBR
NavBR.Client
   ↓ RemotePhysicalVehicleCoordinator
Named Pipe local v2
   ↓
NavBR.OmsiPlugin.dll
   ↓
NavBR.OmsiInterop.dll
   ↓
RoadVehicle remoto dentro do OMSI
```

O servidor da sala nunca escreve diretamente no OMSI. O cliente valida sessão, compatibilidade e estado recebido antes de encaminhar comandos ao plugin local.

## Estado da Alpha.13 Test 1

Já existe base funcional para:

- spawn experimental de veículo remoto;
- update de pose;
- despawn/limpeza;
- posição local nativa `LocalX/Y/Z`;
- quaternion de rotação nativo;
- velocidade;
- luzes externas/interiores/freio no backend atual;
- setas/pisca-alerta no backend atual;
- limite de quantidade de veículos remotos;
- validação de mapa/protocolo/veículo antes do spawn;
- remoção em saída/desconexão/reconexão;
- escrita física protegida por opt-in.

A Test 1 precisa validar isso em **2+ PCs reais** antes de ampliarmos o conjunto de estados.

## O que ainda não entra na Test 1

- portas;
- matriz/linha/destino física;
- articulação;
- limpadores/buzina e animações adicionais;
- fallback universal de modelo;
- sincronização física completa do tráfego IA;
- suporte amplo a outras versões do OMSI.

## Arquitetura

### Cliente

O `MultiplayerClientService` possui o único `RemotePhysicalVehicleCoordinator` responsável pelo ciclo físico da sessão.

Isso evita que a mesma telemetria remota gere dois fluxos concorrentes de spawn/update.

O coordenador só tenta renderizar fisicamente quando:

1. o recurso experimental foi ativado pelo usuário;
2. o plugin bridge está conectado;
3. o plugin anuncia capacidade de spawn e transform;
4. o jogador remoto está em jogo;
5. existe identidade real do veículo remoto;
6. o mapa/protocolo são compatíveis;
7. o limite de segurança ainda permite outro veículo.

### Bridge local

O bridge usa Windows Named Pipe local e protocolo versionado `v2`.

O protocolo transporta, quando disponíveis:

- PlayerId/DisplayName;
- mapa e fingerprint;
- veículo e fingerprint;
- HOF e fingerprint;
- linha/rota/próxima parada/destino;
- pose absoluta e local;
- quaternion;
- velocidade/aceleração;
- portas;
- luzes;
- seta;
- buzina;
- limpadores;
- freio de estacionamento;
- ré.

Ter o campo no protocolo não significa que o backend físico já aplique todos eles. A Alpha.13 deve continuar anunciando somente capacidades realmente implementadas.

## Telemetria real da pose

No OMSI 2.3.004, o provider externo já lê do veículo do jogador:

- `Position` local;
- `AbsPosition` para navegação/distância;
- quaternion `Rotation`;
- `Tacho`/groundspeed/velocity para velocidade.

A pose física enviada ao outro PC usa os valores locais/quaternion reais. Não há geração de coordenada fictícia nem conversão aproximada para o primeiro teste.

## Native interop x86

O plugin carrega o shim:

```text
plugins\NavBR.OmsiInterop.dll
```

A runtime é bloqueada para:

- processo `Omsi.exe`;
- x86;
- OMSI 2.3.004/2.3.4 conforme identificação suportada;
- ABI/interoperabilidade esperadas;
- probes de endereços nativos válidos.

O shim atual oferece operações protegidas para:

- enumerar RoadVehicles;
- validar ponteiro de RoadVehicle;
- criar veículo;
- aplicar transform/groundspeed;
- aplicar estado visual básico;
- marcar veículo NavBR para remoção.

Nenhum ponteiro recebido da rede é usado diretamente. Os ponteiros físicos são descobertos e registrados localmente pelo plugin após o spawn.

## Segurança do spawn

O caminho do veículo remoto é normalizado e precisa resolver para um arquivo `.bus`/`.ovh` dentro da instalação OMSI local.

Depois do `MakeVehicle`, o plugin compara a lista de RoadVehicles antes/depois e só aceita um novo ponteiro quando consegue identificar a criação de forma inequívoca.

Updates posteriores só são aplicados a veículos registrados como pertencentes ao NavBR.

## Opt-in

A ativação pública fica em Multiplayer:

**Ônibus dos jogadores no OMSI (TESTE ALPHA)**

Ao ativar, o NavBR persiste o consentimento experimental para cliente/plugin. Ao desativar, o cliente ainda pode enviar despawn dos veículos que ele criou para permitir limpeza segura.

## Teste recomendado

Consulte:

- [ALPHA13_TEST1_COMMUNITY.md](ALPHA13_TEST1_COMMUNITY.md)

O primeiro cenário deve ser dois PCs na mesma LAN. Depois de confirmar spawn/movimento/estabilidade, avançamos para Internet/NAT variados.

## Portas — próxima etapa

O protocolo já possui `DoorFlags`, mas o backend atual ainda não aciona portas.

Não haverá um nome de trigger universal hardcoded para todos os ônibus. A direção planejada é usar **perfis de compatibilidade por modelo**. Se não houver perfil validado, a porta permanece sem sincronização em vez de tentar um trigger desconhecido.

Uma rota técnica pública conhecida para triggers de RoadVehicle será usada apenas depois de validarmos o mecanismo e a licença/referência adequada, sem copiar implementação proprietária de outros multiplayer.

## Matriz/HOF — próxima etapa

O protocolo já transporta HOF, linha e destino quando a telemetria real fornece esses dados.

A aplicação física da matriz dependerá de:

- veículo remoto correto instalado;
- HOF compatível;
- perfil/capacidade do modelo;
- método seguro para aplicar o destino sem quebrar scripts específicos do ônibus.

## Articulação — próxima etapa

Ônibus articulados exigem tratamento adicional de seções/trailers. A Alpha.13 Test 1 não deve fingir suporte: o teste inicial mede somente o veículo físico base e sua pose.

## Diagnóstico

Falhas de spawn/update/despawn são registradas em diagnóstico técnico com código/erro, evitando repetição infinita da mesma mensagem por jogador.

A camada física é best-effort: falha do plugin não deve derrubar SignalR, chat, voz, HUD ou a sessão multiplayer normal.

## Referências de mercado

Busweave e BusDriverMP demonstram publicamente que multiplayer físico no OMSI pode sincronizar mais estados, incluindo portas/matriz/articulação em determinados veículos.

Eles são benchmarks de comportamento. O NavBR não depende de código, assets, protocolo ou infraestrutura proprietária desses projetos.

## Critério para a próxima Test

Antes de portas/matriz/articulação, precisamos confirmar em testes reais:

- spawn consistente;
- posição correta;
- orientação correta;
- movimento aceitável;
- sem duplicação;
- despawn correto;
- reconexão correta;
- estabilidade do OMSI;
- funcionamento nos dois sentidos entre PCs.
