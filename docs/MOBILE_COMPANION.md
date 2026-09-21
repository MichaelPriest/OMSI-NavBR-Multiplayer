# NavBR Mobile Companion — Alpha 2 Android

## Estado

**Em desenvolvimento ativo na branch `feature/mobile-companion-alpha2` e PR #34 (DRAFT, sem merge).**

A entrega principal da Alpha 1 passa a ser um **APK Android**, mantendo a PWA como base visual compartilhada.

Arquitetura:

`APK Android -> NavBR Client no PC -> C# authority -> Plugin Bridge/OMSI`

O celular nunca acessa memória do OMSI diretamente.

## Tecnologia

- React + TypeScript + Vite;
- Capacitor 8;
- Android APK;
- NavBR desktop hospedando a API local;
- C# como autoridade do estado;
- Plugin Bridge como único caminho para futuras escritas no OMSI.

## Alpha 1

A primeira versão funcional inclui:

- APK Android instalável;
- **descoberta automática do NavBR na mesma rede Wi-Fi/LAN**;
- conexão automática sem digitar IP ou código quando o broadcast UDP estiver disponível;
- pareamento manual por **IP/endereço do PC + código** mantido como fallback;
- código novo a cada abertura do NavBR;
- comunicação LAN pela porta TCP **27731**;
- API protegida por código e limitada a rede local/loopback;
- GPS com telemetria e navegação reais;
- rota, retorno à rota e próximas paradas quando disponíveis;
- IBIS separado do GPS;
- IBIS mostrando linha, rota/curso, destino, HOF, próxima parada e atraso reais;
- Status do OMSI e Plugin Bridge;
- PWA ainda disponível como alternativa pelo navegador;
- card no desktop mostrando IPs LAN e código.

## Como usar o APK

1. instale o APK no Android;
2. abra o NavBR no PC;
3. mantenha celular e PC na mesma rede Wi-Fi/LAN;
4. abra o APK: ele procura automaticamente o NavBR e conecta sozinho;
5. abra o OMSI e carregue ônibus/mapa/rota;
6. use GPS, IBIS e Status no celular.

A descoberta automática usa UDP **27732** e o estado usa HTTP **27731**. Se o roteador, isolamento de Wi-Fi ou firewall bloquear broadcast, o APK mantém o modo manual por IP + código.

## Segurança

O APK acessa o host local do NavBR por HTTP na LAN nesta Alpha porque o PC não possui certificado HTTPS local confiável. O host:

- aceita somente clientes loopback ou de faixas privadas locais;
- exige código de pareamento;
- gera novo código a cada abertura do NavBR;
- não expõe escrita direta na memória do OMSI.

## IBIS

A leitura continua vindo somente do estado real do OMSI.

Na Alpha 2, a referência técnica do **OmsiHook/Omsi-Extensions** foi revisada. O OmsiHook demonstra duas famílias de integração relevantes: acesso a variáveis/stringvars do script e acionamento de triggers do veículo. Para esta etapa, o NavBR adota somente a segunda via, porque ela pode reutilizar a ponte local já protegida e validada.

Fluxo dos controles IBIS:

1. o desktop lê o catálogo `[mouseevent]` real do ônibus carregado;
2. desse catálogo, identifica conservadoramente famílias de eventos de IBIS/AFR/impressora/matrix;
3. o APK mostra somente esses nomes reais;
4. cada pressão e liberação é revalidada novamente pelo desktop;
5. o comando só segue se `local-vehicle-trigger` estiver disponível e a autorização experimental do usuário estiver ligada;
6. o alvo continua sendo exclusivamente o `RoadVehicle` local do jogador;
7. nenhum nome de trigger é inventado e nenhum mock é usado.

A escrita direta de linha/rota/destino/HOF via variáveis ou stringvars continua **desativada**. Ela só será adicionada quando o NavBR resolver com segurança os arrays de script do veículo atual, como a arquitetura do OmsiHook faz, em vez de assumir nomes/offsets universais.

### Designer IBIS fiel ao cockpit

A interface principal do IBIS no Mobile Companion deixa de ser uma lista técnica de triggers e passa a representar um equipamento físico:

- carcaça, visor LCD e teclado em layout de IBIS usado no OMSI;
- leitura no visor continua vindo exclusivamente da telemetria real: linha, curso/rota, destino, HOF, próxima parada e atraso;
- detecção visual de famílias como IBIS clássico, ATRON, ALMEX, EFAD/AFR e matrix quando os nomes reais dos eventos permitem identificar o equipamento;
- teclas sem correspondência real continuam visíveis para preservar o layout do aparelho, porém ficam bloqueadas;
- teclas mapeadas executam `press/release` somente no `[mouseevent]` real encontrado e revalidado pelo desktop;
- eventos reais que não puderem ser associados com segurança a uma tecla ficam disponíveis apenas no painel técnico secundário para diagnóstico.

O objetivo é ter o mesmo fluxo operacional do aparelho no jogo sem criar uma segunda lógica falsa no celular. O OMSI/ônibus continua sendo a autoridade da função.


## Portas

- multiplayer local: 27730;
- Mobile Companion HTTP: **27731**;
- descoberta automática Mobile Companion UDP: **27732**;
- Company Node: 27740.

## Build Android

O CI compila a PWA e depois gera:

`OMSI-NavBR-Mobile-Alpha2-debug.apk`

O APK é publicado como artifact:

`OMSI-NavBR-Mobile-Android-Alpha2`

## Sem mocks

Quando OMSI, rota, HOF, posição ou Plugin Bridge não fornecerem um dado real, o APK mostra indisponível/aguardando. Não são geradas linhas, rotas, destinos, paradas ou posições artificiais.


## Alpha 2 em desenvolvimento

A branch `feature/mobile-companion-alpha2` amplia o app sem substituir a Alpha 1 de teste.

Incluído nesta etapa:

- painel **Ônibus** com telemetria real de combustível, acelerador, freio, direção, aceleração, portas, luzes, setas, limpador, freio de estacionamento, ré, pedido de parada e buzina;
- estado multiplayer real reutilizado da Central Multiplayer;
- mapa relativo dos players compatíveis na mesma sessão;
- lista de players com linha, rota, ônibus, destino, velocidade, distância e atividade de voz;
- painel **Voz** com liga/desliga, canal, recepção/deafen e mixer remoto;
- **PTT acionado pelo celular**, usando nesta etapa o microfone configurado no PC;
- lease de PTT com liberação automática caso o celular perca a conexão;
- endpoint mobile de comandos autenticado pelo token da sessão;
- catálogo de mouse events reais detectados no veículo exposto para o futuro painel personalizável;
- IBIS mantém leitura real e agora também expõe **teclas/eventos reais do próprio veículo** quando identificados no catálogo `[mouseevent]`; escrita genérica de variáveis/stringvars continua desativada.

O app não envia comandos genéricos/falsos ao ônibus local.


### Controles reais do ônibus no celular

A Alpha 2 agora possui uma capacidade adicional, desligada por padrão:

`local-vehicle-trigger`

Fluxo de segurança:

1. o desktop lê somente eventos `[mouseevent]` reais do arquivo/modelo do ônibus carregado;
2. o usuário precisa ativar **Controles do ônibus pelo celular (EXPERIMENTAL)** no NavBR desktop;
3. o Plugin Bridge precisa anunciar a capacidade `local-vehicle-trigger`;
4. cada comando vindo do APK é revalidado contra o catálogo atual do ônibus;
5. somente o `RoadVehicle` do jogador local é usado como alvo;
6. o comando é executado no callback/thread correto do OMSI;
7. nomes de triggers são retidos de forma limitada e deduplicada para evitar ponteiros Delphi inválidos.

No APK, a aba **Ônibus** possui busca, favoritos e botões dinâmicos. O app não inventa nomes como porta/luz/buzina: ele mostra somente os eventos reais encontrados no veículo.

A autorização usa a flag própria `experimental-mobile-vehicle-controls.enabled` e não depende da opção de ônibus físicos remotos.
