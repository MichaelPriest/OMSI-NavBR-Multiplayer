# NavBR Mobile Companion — Alpha 1 Android

## Estado

**Em desenvolvimento ativo na branch `feature/mobile-companion-alpha1` e PR #33.**

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

Nesta Alpha 1 o IBIS é **somente leitura real**.

Ainda não existe no Plugin Bridge uma capacidade nativa segura para escrever linha, rota, destino e HOF no ônibus do jogador. Nenhum comando fake é enviado.

Próxima etapa:

1. adicionar capacidades IBIS explícitas ao Plugin Bridge;
2. ler catálogo real de linha/rota/HOF do mapa/ônibus;
3. permitir somente opções realmente disponíveis;
4. executar a alteração no thread correto do OMSI;
5. confirmar o resultado antes de atualizar o APK.

## Portas

- multiplayer local: 27730;
- Mobile Companion HTTP: **27731**;
- descoberta automática Mobile Companion UDP: **27732**;
- Company Node: 27740.

## Build Android

O CI compila a PWA e depois gera:

`OMSI-NavBR-Mobile-Alpha1-debug.apk`

O APK é publicado como artifact:

`OMSI-NavBR-Mobile-Android-Alpha1`

## Sem mocks

Quando OMSI, rota, HOF, posição ou Plugin Bridge não fornecerem um dado real, o APK mostra indisponível/aguardando. Não são geradas linhas, rotas, destinos, paradas ou posições artificiais.
