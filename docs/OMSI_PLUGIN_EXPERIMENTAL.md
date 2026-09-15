# Plugin OMSI experimental — veículos remotos

> Documento de engenharia. Esta funcionalidade **não faz parte da alpha.9 publicada** e permanece experimental até validação real no OMSI 2.3.004.

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

### Limite importante da interface documentada

A documentação pública do OMSI descreve a interface de plugin como acesso de leitura/escrita às **variáveis do veículo local**, acesso a string/system variables e acionamento de triggers. Ela **não documenta uma chamada pública para criar/spawnar arbitrariamente um novo veículo**.

Consequência para o NavBR:

- `AccessVariable` não deve ser tratado como uma API de criação de ônibus;
- `AccessTrigger` não deve ser usado como substituto de um mecanismo de spawn;
- a primeira representação física remota precisa de uma investigação separada e reversível;
- nenhuma técnica de patch/injeção no executável será introduzida apenas para forçar essa etapa.

Referência pública da interface:

- [OMSIWiki — Plug-In Interface](https://wiki.omnibussimulator.de/omsiwikineu/index.php?title=Plug-In_Interface)

## Referência arquitetural: BusdriverMP

Informações públicas do BusdriverMP são úteis como **referência de comportamento**, não como código-base.

O projeto informa publicamente que:

- jogadores veem os ônibus uns dos outros em tempo real;
- posição, portas, setas, matriz, articulação e outros estados podem ser sincronizados;
- mapas não precisam ser preparados especificamente para o multiplayer;
- quando ambos possuem o mesmo ônibus, o mesmo modelo pode ser usado;
- quando o ônibus remoto não está disponível localmente, pode existir fallback para um veículo padrão, como EN92/GN92;
- discussões públicas do projeto fazem referência a versões **AI** dos ônibus na representação dos demais jogadores.

Isso sugere uma direção melhor para a investigação do NavBR: **representação remota baseada em veículo/instância AI controlável ou mecanismo equivalente**, com fallback de modelo, em vez de assumir que a interface `.opl` fornece um spawn direto.

O mecanismo interno do BusdriverMP não é público e **não deve ser inferido nem copiado**. O NavBR precisa provar seu próprio caminho técnico.

Referências públicas:

- [BusdriverMP](https://busdrivermp.de/)
- [Projeto Bremen / OMSI WebDisk](https://reboot.omsi-webdisk.de/community/user-post-list/49-projekt-bremen/)

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

O CI compila e valida o pacote x86 experimental. Ainda falta validar o carregamento real no OMSI 2.3.004.

Critério de aceite:

1. OMSI 2.3.004 inicia normalmente;
2. `PluginStart` aparece no log;
3. heartbeats aparecem durante a sessão;
4. `PluginFinalize` aparece no encerramento normal;
5. não há crash ou regressão perceptível de performance.

## Fase 1 — bridge local

A **alpha.10 em desenvolvimento** implementa o bridge local entre `NavBR.Client` e o plugin usando **Windows Named Pipes**.

Características atuais:

- comunicação somente no computador local;
- nenhuma porta pública adicional;
- pipe restrito ao usuário atual do Windows;
- protocolo versionado `v1`;
- handshake `plugin-hello` / `client-hello`;
- mensagem de retorno `plugin-status`;
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
plugin-status + navbr-plugin.log
```

### Painel de diagnóstico da alpha.10

A janela principal do cliente inclui o painel técnico:

```text
PLUGIN BRIDGE v1 • EXP
```

Ele mostra, entre outros campos:

- `status=CONNECTED|WAITING`;
- PID informado pelo plugin;
- `process-match=YES|NO|UNKNOWN`;
- versão do plugin;
- `heartbeat=LIVE|NONE|STALE`;
- idade do heartbeat;
- total de callbacks;
- índice da system variable;
- total de estados remotos;
- total de remotos compatíveis;
- mapa atual;
- fingerprint de compatibilidade resumido.

No teste real saudável esperamos principalmente:

```text
status=CONNECTED
process-match=YES
heartbeat=LIVE
```

Checklist completo:

- [ALPHA10_PLUGIN_TEST_CHECKLIST.md](ALPHA10_PLUGIN_TEST_CHECKLIST.md)

## Filtro de compatibilidade antes da futura representação física

A alpha.10 envia ao plugin o contexto do veículo local, incluindo mapa e `MapCompatibilityId` quando disponível.

Antes de um remoto ser considerado candidato a futura representação física:

1. jogador local deve estar em jogo;
2. jogador remoto deve estar em jogo;
3. se ambos possuem `MapCompatibilityId`, os IDs precisam ser iguais;
4. se o fingerprint não estiver disponível, o nome do mapa precisa coincidir;
5. remotos incompatíveis continuam podendo existir no multiplayer/HUD, mas não são selecionados pelo plugin para futura representação.

O fingerprint acompanha o **mapa atual**, inclusive quando o jogador troca de mapa depois de já ter entrado na sala.

## Registro seguro de veículos remotos

O plugin experimental mantém um registro local temporário dos estados recebidos.

Proteções atuais:

- máximo de **64 estados remotos** mantidos simultaneamente;
- validação de `PlayerId` e tamanho dos campos de texto;
- rejeição de coordenadas, heading ou velocidade com `NaN`/`Infinity`;
- remoção explícita quando o jogador sai;
- limpeza ao perder/reiniciar a sessão;
- expiração automática após **5 segundos** sem atualização;
- heartbeat informa total recebido e total compatível com o mapa local;
- `plugin-status` só é aceito pelo cliente quando o PID coincide com o PID que realizou o handshake;
- contadores negativos/inválidos do status são rejeitados.

Esses limites existem antes mesmo da criação de qualquer entidade física no OMSI.

## Fase 2 — uma representação remota de teste

Só depois de validar carga e bridge no OMSI real:

1. selecionar um único jogador remoto compatível;
2. usar o estado já validado/interpolado;
3. investigar um caminho reversível para representação por **veículo AI/instância equivalente**;
4. provar criação/associação e remoção sem patch no executável;
5. aplicar posição e orientação somente se o mecanismo escolhido permitir isso com estabilidade;
6. remover a representação ao desconectar, trocar de mapa ou expirar;
7. medir estabilidade, colisões e performance.

Se não for encontrado um mecanismo seguro e documentável, o NavBR deve permanecer com o jogador apenas no HUD/mapa em vez de forçar uma implementação invasiva.

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

A intenção é que a primeira representação remota experimental consuma esse estado suavizado, e não os pacotes brutos da rede.

Ainda ficam para uma etapa posterior:

- extrapolação limitada para perda curta de pacotes;
- snap somente acima de erro máximo seguro;
- política de distância/LOD;
- frequência de aplicação dentro do OMSI.

## Compatibilidade e fallback de ônibus

A estratégia passa a considerar explicitamente o padrão observado em soluções multiplayer existentes: usar o mesmo modelo quando disponível e um fallback quando não estiver.

O NavBR deverá distinguir:

- mesmo arquivo/modelo `.bus` disponível localmente;
- ônibus compatível por perfil NavBR;
- ônibus remoto não instalado;
- ônibus sem suporte de sincronização avançada.

Fallback planejado:

- usar um ônibus padrão/base compatível com o tipo (por exemplo, solo ou articulado) se o mecanismo AI permitir; ou
- manter o jogador apenas no HUD/mapa, sem representação física.

Nunca assumir que todos os participantes possuem os mesmos add-ons.

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

## Automação de testes

O CI da alpha.10 já cobre:

- build do cliente, servidor e plugin x86;
- validação dos arquivos necessários ao pacote do plugin;
- instalação e remoção do plugin em uma instalação OMSI simulada;
- handshake real por Named Pipe entre um plugin simulado e `OmsiPluginBridgeServer`;
- recebimento de `plugin-status`;
- empacotamento do cliente standalone;
- validação do recurso de ícone no EXE;
- geração do bundle temporário `OMSI-NavBR-alpha10-integration-test-win-x86` para teste manual.

O smoke test do bridge também verifica rejeição de PID/status inválidos no bloco de desenvolvimento mais recente.

## Segurança e estabilidade

Regras obrigatórias:

- plugin opcional;
- multiplayer externo continua funcionando sem plugin;
- falha do bridge não pode derrubar a sala;
- validar todos os dados recebidos antes de aplicar no OMSI;
- limitar quantidade de entidades/estados remotos;
- limitar distância de atualização quando houver representação física;
- remover entidades ao trocar de mapa/sair da sala;
- nunca executar comandos arbitrários recebidos pela rede;
- não carregar código enviado por outros jogadores;
- não usar patch/injeção no executável para contornar a ausência de API documentada de spawn;
- logs de diagnóstico sem tokens/senhas/dados pessoais.

## Estado atual

- ✅ projeto x86 experimental criado;
- ✅ exports/callbacks básicos implementados;
- ✅ bridge local NavBR ↔ plugin implementado por Named Pipe;
- ✅ protocolo inclui contexto local, atualização, remoção, limpeza e `plugin-status`;
- ✅ painel `PLUGIN BRIDGE v1 • EXP` implementado no cliente alpha.10;
- ✅ frames remotos do multiplayer são encaminhados ao bridge em modo diagnóstico;
- ✅ limite de 64 remotos + timeout de 5 s;
- ✅ filtro por mapa / fingerprint antes da futura representação;
- ✅ interpolação básica de posição, velocidade e heading implementada;
- ✅ instalador/removedor experimentais com manifesto e proteção contra sobrescrita de arquivos alheios;
- ✅ CI valida handshake/status e instalação/remoção;
- ✅ bundle integrado de teste é gerado pelo CI;
- 🧪 falta validar carregamento real do plugin no OMSI 2.3.004;
- 🧪 falta validar `CONNECTED` + `process-match=YES` + `heartbeat=LIVE` no OMSI real;
- 🧪 falta validar fluxo SignalR → cliente → pipe → plugin em dois PCs;
- 🔬 investigar mecanismo seguro de representação por AI/instância equivalente;
- ⬜ primeira representação remota experimental;
- ⬜ sincronização física dentro do OMSI.
