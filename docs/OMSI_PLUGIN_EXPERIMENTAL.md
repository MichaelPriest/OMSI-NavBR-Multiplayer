# Plugin OMSI experimental — veículos remotos

> Documento de engenharia. Esta funcionalidade **não faz parte da alpha.9 estável/testável** e permanece isolada até validação real no OMSI 2.3.004.

## Objetivo

Investigar uma integração opcional capaz de representar jogadores remotos **dentro do próprio OMSI**, mantendo o aplicativo NavBR responsável por rede, salas, chat, voz, compatibilidade e interpolação de alto nível.

A existência de projetos como BusdriverMP mostra que o conceito de sincronizar ônibus remotos no OMSI é viável, mas o NavBR não depende do código, assets ou protocolo desses projetos. O desenvolvimento deve usar somente interfaces/documentação permitidas e componentes com licença compatível.

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
   ↕ bridge local versionado
NavBR.Client
   ↕ multiplayer NavBR
outros jogadores
```

O servidor da sala **não conversa diretamente com o plugin**. O cliente continua sendo a camada que valida sessão, compatibilidade e dados recebidos.

## Interface oficial de plugin do OMSI

A interface documentada do OMSI usa:

```text
<OMSI>\plugins\<plugin>.dll
<OMSI>\plugins\<plugin>.opl
```

O `.opl` informa a DLL e as listas ordenadas de variáveis/triggers que o OMSI deve disponibilizar ao plugin.

Callbacks principais usados pelo protótipo:

- `PluginStart`
- `PluginFinalize`
- `AccessVariable`
- `AccessStringVariable`
- `AccessSystemVariable`
- `AccessTrigger`

O protótipo .NET usa **DNNE** para gerar a camada nativa/exportações C necessárias para o OMSI carregar o assembly x86.

## Fase 0 — teste de carga

Projeto:

```text
src/NavBR.OmsiPluginExperimental/
```

Nesta fase o plugin:

- é x86;
- recebe apenas a system variable `Time`;
- escreve heartbeat de diagnóstico;
- não altera variáveis;
- não aciona triggers;
- não cria veículos;
- não injeta patches no executável.

Log:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-plugin.log
```

Critério de aceite:

1. OMSI 2.3.004 inicia normalmente;
2. `PluginStart` aparece no log;
3. heartbeats aparecem durante a sessão;
4. `PluginFinalize` aparece no encerramento normal;
5. não há crash ou regressão perceptível de performance.

## Fase 1 — bridge local

Depois do teste de carga, criar uma comunicação local entre `NavBR.Client` e o plugin.

Requisitos:

- somente localhost/IPC;
- protocolo versionado;
- nenhuma porta pública adicional;
- autenticação/segredo efêmero local ou ACL de processo/usuário;
- limites de tamanho/frequência;
- timeouts;
- desligamento seguro se o cliente desaparecer.

Candidatos:

- named pipes do Windows;
- memória compartilhada com sincronização explícita;
- loopback TCP somente se houver vantagem clara.

Named pipes são a preferência inicial por isolamento local e simplicidade de ACL.

## Fase 2 — um veículo remoto de teste

Antes de sincronizar vários jogadores:

1. selecionar um único jogador remoto;
2. transmitir um estado mínimo;
3. criar/associar uma entidade experimental;
4. aplicar posição e orientação;
5. remover a entidade ao desconectar;
6. medir estabilidade e performance.

Estado mínimo pretendido:

```text
PlayerId
MapCompatibilityId
Timestamp
Position X/Y/Z
Heading
Speed
VehicleCompatibilityId (futuro)
```

## Fase 3 — suavização dentro do OMSI

A rede não deve mover o ônibus remoto diretamente a cada pacote.

O cliente/plugin deve manter snapshots e usar:

- interpolação temporal;
- limite de extrapolação;
- snap somente quando o erro ultrapassar um limite seguro;
- timeout para remover/frear entidade sem atualização;
- frequência limitada para reduzir custo dentro do OMSI.

## Fase 4 — estados adicionais

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

## Referências técnicas

- OMSI Wiki — Plug-In Interface: documentação da DLL/OPL e callbacks;
- `space928/Omsi-Extensions` — exemplo open source de plugin OMSI moderno em .NET x86 e OmsiHook;
- DNNE — geração de exports nativos para assemblies .NET;
- BusdriverMP — referência funcional/UX de multiplayer OMSI, sem reutilização de código proprietário.

A licença de cada dependência deverá ser verificada antes de qualquer incorporação. O protótipo atual usa DNNE (MIT) apenas para a camada de exports nativos.
