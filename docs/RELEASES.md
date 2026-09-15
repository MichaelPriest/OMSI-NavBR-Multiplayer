# Releases

O OMSI NavBR Multiplayer usa versionamento semântico (SemVer) e GitHub Actions para publicar versões testáveis.

## Convenção de versão

- `v0.x.y-alpha.n` — desenvolvimento inicial; funcionalidades e interface ainda podem mudar;
- `v0.x.y-alpha.n-test.m` — **prerelease permanente de teste de integração**, destinada a validar uma alpha ainda em desenvolvimento sem promovê-la como versão geral;
- `v0.x.y-beta.n` — funcionalidades principais integradas, com foco maior em estabilidade;
- `v0.x.y` — versão estável.

## Estado atual

A prerelease geral mais recente continua sendo:

```text
v0.3.0-alpha.9
```

Ela continua como **download geral recomendado**, porque a alpha.10 ainda possui recursos que aguardam validação real no OMSI e entre computadores.

A alpha.10 está em desenvolvimento. A prerelease permanente de integração mais recente é:

```text
v0.3.0-alpha.10-test.6
```

A `test.6` é **somente para teste de integração**. Ela não substitui a alpha.9 como versão geral e não significa que a alpha.10 esteja pronta para merge/release final.

As tags `test.1` a `test.5` permanecem publicadas como snapshots históricos e nunca são sobrescritas.

## Prereleases de teste de integração

Builds `-test.n` existem para que pacotes importantes de validação não dependam do prazo de retenção dos artefatos do GitHub Actions.

Regras:

- ficam publicadas na área de GitHub Releases até remoção manual;
- são marcadas como **Prerelease**;
- cada `test.n` é tratada como imutável: o workflow recusa sobrescrever uma tag já publicada;
- o commit exato usado no teste fica associado à tag;
- incluem checksums SHA-256;
- não substituem a release geral no catálogo normal do GitHub Pages;
- a seção experimental do site pode apontar explicitamente para o teste atual;
- uma correção sempre recebe uma nova tag `test.n+1`.

## Alpha.10 test.6

Release:

```text
v0.3.0-alpha.10-test.6
```

Commit publicado:

```text
7a0fbe3ffbcc338e776add1e1dec1bb5ac7154ea
```

Pacote integrado recomendado para uma rodada completa de teste:

```text
OMSI-NavBR-alpha10-test.6-integration-win-x86.zip
```

Para uso normal, o **EXE standalone** é a opção preferida:

```text
OMSI-NavBR-Multiplayer-v0.3.0-alpha.10-test.6-win-x86.exe
```

### Mudanças importantes da test.6

- plugin OMSI publicado como **Native AOT x86 autocontido**;
- o plugin continua x86 porque o OMSI 2 é 32-bit, mas **não exige mais instalação separada do Microsoft .NET Runtime x86**;
- plugin embutido no próprio EXE do NavBR;
- instalação, atualização e remoção pelo painel `PLUGIN BRIDGE v1 • EXP`;
- pacote instalado no OMSI reduzido a `NavBR.OmsiPlugin.dll` + `NavBR.OmsiPlugin.opl`;
- detecção da instalação pelo caminho/processo conhecido, registro Aerosoft `Product_Path`, Steam App ID `252530`, `appmanifest_252530.acf` e bibliotecas Steam adicionais;
- seletor manual de pasta permanece fallback;
- novo ícone compacto pino laranja + ônibus, gerado em 10 resoluções para Windows;
- correção do `XamlParseException` do HUD e validação automática dos tokens de cor XAML;
- HUD moderno com barra superior translúcida;
- GPS heading-up;
- linha, destino e próxima parada fora do mapa;
- chat acoplado ao HUD e proteção de input durante digitação;
- manobras/curvas somente quando a geometria é confiável;
- `navbr.log` automático;
- referências técnicas OMSI Launcher e OmsiHook documentadas para a próxima fase de integração.

### Gates de CI da test.6

Antes da publicação, o workflow validou:

- plugin Native AOT `win-x86` compilado;
- DLL PE/I386;
- exports `PluginStart`, `PluginFinalize`, `AccessVariable`, `AccessTrigger`, `AccessStringVariable` e `AccessSystemVariable`;
- instalação e remoção em OMSI simulado **sem preparar .NET Runtime x86**;
- payload do plugin embutido no cliente;
- build do cliente WPF x86;
- smoke test do Named Pipe/bridge;
- EXE standalone self-contained;
- recurso de ícone dentro do EXE;
- servidor dedicado x64;
- montagem do pacote integrado.

Esses gates não substituem teste real dentro do OMSI 2.3.004.

### Assets principais e SHA-256

```text
da1ab08ee6f5bb777f1c730d446a90a9d52e9417534c9bcecb3f7191463cd8e2  OMSI-NavBR-alpha10-test.6-integration-win-x86.zip
1ea18dc0456c5c2e65071091462ea1cfeed76e6f2bc1e197aa04ae54eade48b2  OMSI-NavBR-Multiplayer-v0.3.0-alpha.10-test.6-win-x86.exe
c2993234b09d73f60daf4eaf2a503f52854dd7594e56069cbb977af1bd469466  OMSI-NavBR-Multiplayer-v0.3.0-alpha.10-test.6-win-x86.zip
8c0f80b95cb9ab85b19bc06665bd8ac306ca240151378b61aa8eb4d88953884b  OMSI-NavBR-Plugin-v0.3.0-alpha.10-test.6-win-x86.zip
f45648f11515391abc857af61af3c1a479e264ff97fe37dbe0d4777410c30383  OMSI-NavBR-Server-v0.3.0-alpha.10-test.6-win-x64.zip
```

O arquivo `SHA256SUMS.txt` também é publicado junto da Release.

## Histórico alpha.10

- `test.1` — primeira integração permanente;
- `test.2` — diagnóstico de instalação e bundle permanente mais completo;
- `test.3` — preflight do Runtime x86 e nova rodada de aceitação;
- `test.4` — primeira rodada do HUD/GPS moderno;
- `test.5` — plugin embutido/gerenciado pelo EXE e correções de HUD/ícone em desenvolvimento;
- `test.6` — Native AOT x86 sem runtime externo, novo ícone compacto e fluxo atual de instalação/detecção.

Snapshots anteriores permanecem disponíveis somente para comparação e rastreabilidade; o teste atual deve usar a `test.6`.

## Limite atual do multiplayer 3D

Mesmo na `test.6`, o plugin **ainda não cria nem move ônibus físicos de outros jogadores no mundo 3D do OMSI**.

Hoje, jogadores remotos podem aparecer no GPS/mapa/HUD do NavBR, presença, chat e voz, e seus estados podem chegar ao bridge/plugin experimental para diagnóstico. A representação física 3D continua como próxima camada de pesquisa.

As referências `NyCodeGHG/omsi-launcher` e `space928/Omsi-Extensions` / `OmsiHook` são usadas para orientar descoberta da instalação, arquitetura de integração e investigação futura de `PlayerVehicle`, `RoadVehicles` e ciclo de vida de entidades. Não se deve assumir que escrever coordenadas em memória seja suficiente ou seguro para criar um veículo remoto.

## Publicação

O workflow `.github/workflows/release.yml` compila e publica os pacotes oficiais quando uma versão está pronta para release geral.

O processo geral inclui:

1. restauração e build com .NET 10;
2. cliente WPF Windows x86;
3. servidor ASP.NET Core/SignalR;
4. cliente self-contained;
5. EXE standalone single-file x86;
6. validação do executável/ícone;
7. servidor dedicado Windows x64;
8. ZIP do cliente e servidor;
9. avisos legais;
10. GitHub Prerelease;
11. catálogo/contadores do GitHub Pages.

A alpha.10 usa um workflow separado para snapshots de integração. Ele repete os gates críticos antes de publicar uma prerelease permanente `-test.n`.

## Artefatos oficiais

Cada prerelease geral publica:

```text
OMSI-NavBR-Multiplayer-v<versão>-win-x86.exe
OMSI-NavBR-Multiplayer-v<versão>-win-x86.zip
OMSI-NavBR-Server-v<versão>-win-x64.zip
LICENSE
THIRD_PARTY_NOTICES.md
```

### EXE standalone

`OMSI-NavBR-Multiplayer-v<versão>-win-x86.exe` é a opção mais simples para a maioria dos usuários. O cliente é self-contained.

Na alpha.10/test.6, o EXE também carrega internamente o pacote do plugin experimental para instalá-lo na pasta correta do OMSI quando o usuário escolher essa opção.

### Cliente ZIP

O ZIP x86 contém a publicação completa do cliente e é útil para diagnóstico ou distribuição manual.

### Servidor ZIP

O servidor dedicado x64 é opcional. Na série 0.3, o modo principal continua **peer-host**: o próprio PC de quem cria a sala inicia o host da sessão.

## Site e contadores

O GitHub Pages mostra a release geral separadamente da prerelease experimental.

- a alpha.9 continua no fluxo geral/recomendado;
- tags `-test` ficam fora do catálogo automático de releases gerais;
- a seção experimental aponta explicitamente para `alpha.10-test.6`.

Isso evita que uma build de integração substitua acidentalmente o download normal.

## Critério para avançar além da alpha.9

Antes de considerar a alpha.10 pronta como prerelease geral, continuam prioritários os testes reais:

1. HUD durante gameplay e menus;
2. posição, velocidade e heading;
3. GPS heading-up e traçado detalhado da rota;
4. destino, próxima parada e manobras;
5. chat sem vazamento de clique/teclado para o OMSI;
6. voz push-to-talk;
7. atalhos e conflitos com `keyboard.cfg`;
8. lista de mapas/roadmaps;
9. novo ícone em Explorer/janela/taskbar;
10. instalação real do plugin no OMSI 2.3.004;
11. painel com `package=EMBEDDED`, `deployment=NATIVE-AOT-X86`, `install=INSTALLED`, `files=2/2`, `manifest=YES`, `runtime=BUILT-IN`;
12. bridge com `status=CONNECTED`, `process-match=YES`, `heartbeat=LIVE` e callbacks aumentando;
13. SignalR → cliente → Named Pipe → plugin entre dois PCs;
14. presença/GPS/chat/PTT/reconnect sem duplicação de remotos;
15. somente depois investigar criação segura de uma entidade AI/equivalente para representação física remota.

Os problemas encontrados nesses testes devem ser corrigidos antes de considerar os recursos correspondentes estáveis.
