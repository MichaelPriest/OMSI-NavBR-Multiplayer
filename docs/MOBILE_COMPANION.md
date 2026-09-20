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
- pareamento informando **IP/endereço do PC + código de pareamento**;
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
3. em **Configurações > Instalações > Mobile Companion**, copie um IP/endereço LAN e o código;
4. no APK, informe o IP do PC, por exemplo `192.168.0.10`;
5. informe o código de pareamento;
6. mantenha celular e PC na mesma rede Wi-Fi/LAN;
7. abra o OMSI e carregue ônibus/mapa/rota;
8. use GPS, IBIS e Status no celular.

Se a porta não for informada, o APK usa **27731** automaticamente.

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
- Mobile Companion: **27731**;
- Company Node: 27740.

## Build Android

O CI compila a PWA e depois gera:

`OMSI-NavBR-Mobile-Alpha1-debug.apk`

O APK é publicado como artifact:

`OMSI-NavBR-Mobile-Android-Alpha1`

## Sem mocks

Quando OMSI, rota, HOF, posição ou Plugin Bridge não fornecerem um dado real, o APK mostra indisponível/aguardando. Não são geradas linhas, rotas, destinos, paradas ou posições artificiais.
