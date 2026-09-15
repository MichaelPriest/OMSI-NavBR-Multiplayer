# Plugin OMSI experimental — veículos remotos

> Documento de engenharia. Esta funcionalidade **não faz parte da alpha.9 estável/testável** e permanece isolada até validação real no OMSI 2.3.004.

## Objetivo

Investigar uma integração opcional capaz de representar jogadores remotos **dentro do próprio OMSI**, mantendo o aplicativo NavBR responsável por rede, salas, chat, voz, compatibilidade e interpolação de alto nível.

Projetos como BusdriverMP mostram que o conceito de sincronizar ônibus remotos no OMSI é viável, mas o NavBR não depende do código, assets ou protocolo desses projetos. O desenvolvimento deve usar somente interfaces/documentação permitidas e componentes com licença compatível.

## Princípio de arquitetura

O multiplayer atual continua funcionando sem plugin:

```text
Omsi.exe
   ↓ leitura externa
NavBR.Client
   ↕ SignalR / TCP 27730
outros jogadores
   ↓
GPS / HUD
```

A arquitetura experimental adiciona uma ponte opcional:

```text
Omsi.exe
   ↕ interface de plugin OMSI
NavBR.OmsiPlugin
   ↕ Windows Named Pipe local
NavBR.Client
   ↕ SignalR / multiplayer NavBR
outros jogadores
```

O servidor da sala **não conversa diretamente com o plugin**. O cliente continua sendo a camada que valida sessão, compatibilidade e dados recebidos.

## Interface de plugin do OMSI

A integração usa o modelo DLL + `.opl` carregado pelo OMSI:

```text
<OMSI>\plugins\<plugin>.dll
<OMSI>\plugins\<plugin>.opl
```

Callbacks principais do protótipo:

- `PluginStart`
- `PluginFinalize`
- `AccessVariable`
- `AccessStringVariable`
- `AccessSystemVariable`
- `AccessTrigger`

O protótipo .NET usa **DNNE** para gerar a camada nativa/exportações C necessárias para o OMSI carregar o assembly x86.

## Fase 0 — teste de carga

Projeto experimental:

```text
src/NavBR.OmsiPluginExperimental/
```

Nesta fase o plugin:

- é x86;
- recebe uma system variable para heartbeat;
- escreve diagnóstico em log;
- não altera variáveis;
- não aciona triggers;
- não cria veículos;
- não injeta patches no executável.

Log esperado:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-plugin.log
```

O CI já compila e valida o pacote x86 experimental. Ainda falta validar o carregamento real no OMSI 2.3.004.

Critério de aceite:

1. OMSI 2.3.004 inicia normalmente;
2. `PluginStart` aparece no log;
3. heartbeats aparecem durante a sessão;
4. `PluginFinalize` aparece no encerramento normal;
5. não há crash ou regressão perceptível de performance.

## Fase 1 — bridge local

A **alpha.10 em desenvolvimento** já implementa a primeira versão do bridge local entre `NavBR.Client` e o plugin usando **Windows Named Pipes**.

Características atuais:

- comunicação somente no computador local;
- nenhuma porta pública adicional;
- pipe restrito ao usuário atual do Windows;
- protocolo versionado `v1`;
- handshake `plugin-hello` / `client-hello`;
- reconexão automática local;
- limite de tamanho das mensagens;
- telemetria local e remota pode ser encaminhada ao plugin;
- mensagens de remoção de jogador e limpeza da sala evitam estado remoto fantasma;
- falha do bridge não derruba a sessão multiplayer;
- o plugin continua sem aplicar qualquer dado ao OMSI nesta fase.

Fluxo atual:

```text
Jogador remoto
   ↓ SignalR
NavBR.Client
   ↓ mensagem versionada
Named Pipe local
   ↓
NavBR.OmsiPlugin
   ↓
registro remoto / diagnóstico
   ↓
navbr-plugin.log
```

## Filtro de compatibilidade antes do futuro spawn

A alpha.10 também envia ao plugin o contexto do veículo local, incluindo mapa e `MapCompatibilityId` quando disponível.

Antes de um remoto ser considerado candidato a futura representação física:

1. jogador local deve estar em jogo;
2. jogador remoto deve estar em jogo;
3. se ambos possuem `MapCompatibilityId`, os IDs precisam ser iguais;
4. se o fingerprint não estiver disponível, o nome do mapa precisa coincidir;
5. remotos incompatíveis continuam podendo existir no multiplayer/HUD, mas não são selecionados pelo plugin para futuro spawn.

Isso evita tentar representar fisicamente um jogador que esteja em outro mapa ou em uma versão incompatível do mesmo mapa.

## Registro seguro de veículos remotos

O plugin experimental mantém um registro local temporário dos estados recebidos.

Proteções atuais:

- máximo de **64 estados remotos** mantidos simultaneamente;
- validação de `PlayerId` e tamanho dos campos de texto;
- rejeição de coordenadas, heading ou velocidade com `NaN`/`Infinity`;
- remoção explícita quando o jogador sai;
- limpeza ao perder/reiniciar a sessão;
- expiração automática após **5 segundos** sem atualização;
- heartbeat informa total recebido e total compatível com o mapa local.

Esses limites existem antes mesmo da criação de qualquer entidade física no OMSI.

## Fase 2 — um veículo remoto de teste

Só depois de validar carga e bridge no OMSI real:

1. selecionar um único jogador remoto compatível;
2. usar o estado já validado/interpolado;
3. investigar criação/associação segura de uma entidade experimental;
4. aplicar posição e orientação;
5. remover a entidade ao desconectar ou expirar;
6. medir estabilidade e performance.

Estado mínimo:

```text
PlayerId
MapCompatibilityId
Timestamp
Position X/Y/Z
Heading
Speed
VehicleCompatibilityId (futuro)
```

## Suavização de movimento

A base de suavização já está implementada na alpha.10 **antes de qualquer escrita no OMSI**.

Para cada jogador remoto, o plugin mantém os dois snapshots mais recentes e amostra o movimento com atraso de interpolação de aproximadamente **100 ms**.

Já são interpolados:

- X;
- Y;
- Z;
- velocidade;
- heading pelo menor arco angular.

A intenção é que o primeiro ônibus remoto experimental consuma esse estado suavizado, e não os pacotes brutos da rede.

Ainda ficam para uma etapa posterior:

- extrapolação limitada para perda curta de pacotes;
- snap somente acima de erro máximo seguro;
- política de distância/LOD;
- frequência de aplicação dentro do OMSI.

## Estados adicionais

Somente após posição/orientação serem estáveis:

- articulação;
- portas;
- luzes;
- setas/pisca-alerta;
- buzina;
- linha/destino/matriz;
- outros estados compatíveis.

Nem todo ônibus usa os mesmos nomes de variáveis. Portanto esses estados precisam de uma camada de **perfil de veículo**, não de valores hardcoded globais.

## Compatibilidade de veículos

O NavBR deverá distinguir:

- mesmo ônibus/versão disponível localmente;
- ônibus compatível por perfil;
- ônibus remoto não instalado;
- ônibus sem suporte de sincronização avançada.

Fallback planejado:

- mostrar um modelo padrão compatível quando possível; ou
- manter o jogador apenas no HUD/mapa, sem criar entidade física.

Nunca assumir que todos os participantes possuem os mesmos add-ons.

## Segurança e estabilidade

Regras obrigatórias:

- plugin opcional;
- multiplayer externo continua funcionando sem plugin;
- falha do bridge não pode derrubar a sala;
- validar todos os dados recebidos antes de aplicar no OMSI;
- limitar quantidade de entidades remotas;
- limitar distância de atualização;
- remover entidades ao trocar de mapa/sair da sala;
- nunca executar comandos arbitrários recebidos pela rede;
- não carregar código enviado por outros jogadores;
- logs de diagnóstico sem tokens/senhas/dados pessoais.

## Estado atual

- ✅ projeto x86 experimental criado;
- ✅ exports/callbacks básicos implementados;
- ✅ CI compila e valida o pacote do plugin em commits anteriores;
- ✅ artefato experimental de CI é gerado separadamente;
- ✅ bridge local NavBR ↔ plugin implementado por Named Pipe na alpha.10;
- ✅ protocolo inclui contexto local, atualização, remoção e limpeza de estados remotos;
- ✅ frames remotos do multiplayer são encaminhados ao bridge em modo diagnóstico;
- ✅ limite de 64 remotos + timeout de 5 s;
- ✅ filtro por mapa / fingerprint antes do futuro spawn;
- ✅ interpolação básica de posição, velocidade e heading implementada;
- 🧪 CI do bloco mais recente ainda precisa permanecer verde após estas mudanças;
- 🧪 falta validar carregamento real do plugin no OMSI 2.3.004;
- 🧪 falta validar handshake e fluxo SignalR → cliente → pipe → plugin em dois PCs;
- ⬜ criação de uma entidade remota experimental;
- ⬜ sincronização física dentro do OMSI.
