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

Ela é publicada como **Prerelease**, porque HUD, traçado detalhado, destino/próxima parada, voz, atalhos e multiplayer peer-host ainda precisam de validação real mais ampla no OMSI e entre computadores.

A alpha.10 está em desenvolvimento. A prerelease permanente de integração mais recente é:

```text
v0.3.0-alpha.10-test.2
```

Essa tag é **somente para teste de integração**. Ela não substitui a alpha.9 como download recomendado geral e não significa que a alpha.10 esteja pronta para merge/release final.

A `v0.3.0-alpha.10-test.1` permanece publicada como snapshot histórico. A `test.2` acrescenta o diagnóstico de instalação do plugin no cliente (`install`, `files`, `manifest` e `plugin-dir`) e é a build indicada para a rodada atual de testes.

## Prereleases de teste de integração

Builds `-test.n` existem para que um pacote importante de validação não dependa do prazo de retenção dos artefatos do GitHub Actions.

Regras:

- ficam publicadas na área de GitHub Releases até remoção manual;
- são marcadas como **Prerelease**;
- cada `test.n` é imutável na prática: um workflow não deve sobrescrever uma tag de teste já publicada;
- o commit exato usado no teste deve ficar associado à tag;
- devem incluir checksums SHA-256 quando houver binários;
- não entram na vitrine normal do GitHub Pages nem substituem o botão principal de download;
- o próximo teste corrigido deve usar uma nova tag, por exemplo `test.3`, em vez de substituir `test.2`.

### Alpha.10 test.2

A `v0.3.0-alpha.10-test.2` aponta para o commit:

```text
83009c5222f5089ac9b8244c65a8e4de526df5fa
```

Pacote recomendado para o teste:

```text
OMSI-NavBR-alpha10-test.2-integration-win-x86.zip
```

Ele reúne:

- cliente standalone;
- pasta completa do plugin x86;
- instalador e removedor do plugin;
- checklist de teste da alpha.10;
- identificação do build/commit.

Também são publicados separadamente:

```text
OMSI-NavBR-Multiplayer-v0.3.0-alpha.10-test.2-win-x86.exe
OMSI-NavBR-Multiplayer-v0.3.0-alpha.10-test.2-win-x86.zip
OMSI-NavBR-Plugin-v0.3.0-alpha.10-test.2-win-x86.zip
OMSI-NavBR-Server-v0.3.0-alpha.10-test.2-win-x64.zip
SHA256SUMS.txt
LICENSE
THIRD_PARTY_NOTICES.md
```

Checksums verificados da publicação `test.2`:

```text
bd2a9aff5c2af1f6c6ddb544adb50d8bbc8b1896e8723209a5079835f0497429  OMSI-NavBR-alpha10-test.2-integration-win-x86.zip
78873e8f5d87ed10088f337deff136083a7e6963e08f780303c46f49709f9e90  OMSI-NavBR-Multiplayer-v0.3.0-alpha.10-test.2-win-x86.exe
920a49391226d6e6f396fbf344c8461397273a8c48f99039039aeafbb13d9c4e  OMSI-NavBR-Multiplayer-v0.3.0-alpha.10-test.2-win-x86.zip
52d1cb448587d58bc1d2efb97eaca947c882052302c14ae01fbfbac3ec773b8a  OMSI-NavBR-Plugin-v0.3.0-alpha.10-test.2-win-x86.zip
bae3e887dfad4f4b2d79d66b076cde72a74896c1f165426f39ed91288c50d8a5  OMSI-NavBR-Server-v0.3.0-alpha.10-test.2-win-x64.zip
```

O plugin dessa build continua experimental: **não escreve variáveis, não aciona triggers e não cria/move ônibus físicos dentro do OMSI**.

### Alpha.10 test.1

A `v0.3.0-alpha.10-test.1` permanece disponível apenas para comparação/rastreabilidade da primeira integração permanente. Ela não deve substituir a `test.2` nos testes atuais.

## Publicação

O workflow `.github/workflows/release.yml` compila e publica os pacotes oficiais quando a versão está pronta para release.

O processo atual:

1. restaura e compila os projetos com .NET 10;
2. compila cliente WPF x86;
3. compila servidor ASP.NET Core/SignalR;
4. publica o cliente Windows x86 self-contained;
5. gera o EXE standalone single-file x86;
6. valida o executável e o ícone embutido;
7. publica o servidor dedicado Windows x64;
8. gera ZIP do cliente e ZIP do servidor;
9. inclui os avisos legais;
10. cria/atualiza o GitHub Prerelease;
11. atualiza o catálogo do GitHub Pages e os contadores de downloads.

A alpha.10 também possui um workflow específico de prerelease de integração. Ele repete as validações críticas antes de publicar o pacote permanente de teste.

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

O arquivo:

```text
OMSI-NavBR-Multiplayer-v<versão>-win-x86.exe
```

é a opção mais simples para a maioria dos usuários. Ele é self-contained e inclui o runtime necessário para o cliente.

### Cliente ZIP

O ZIP x86 contém a publicação completa do cliente e é útil para diagnóstico, distribuição manual ou quando o usuário prefere trabalhar com a pasta completa da aplicação.

### Servidor ZIP

O servidor dedicado x64 é opcional. Na série 0.3, o modo principal de multiplayer é **peer-host**: o próprio PC de quem cria a sala inicia o host da sessão.

## Site e contadores

O GitHub Pages consulta os assets das releases para mostrar:

- versão geral atual;
- downloads por arquivo;
- downloads por release;
- total de downloads oficiais.

Tags de integração `-test` ficam deliberadamente fora do catálogo normal do site para não confundir um build experimental com o download recomendado. A seção experimental do site oferece explicitamente o pacote `test.2` quando necessário.

A atualização ocorre após releases e também periodicamente pelo workflow de Pages.

## Critério para publicar uma alpha

Uma alpha pode ser publicada quando:

- o CI estiver verde;
- cliente e servidor compilarem;
- EXE standalone e pacotes ZIP forem gerados corretamente;
- a nova funcionalidade estiver suficientemente completa para teste;
- limitações conhecidas estiverem documentadas;
- os recursos ainda não validados em runtime estiverem claramente identificados como experimentais ou aguardando teste real.

Não é necessário esperar todas as fases do projeto para publicar uma nova alpha. O objetivo é disponibilizar builds progressivamente para validação real no OMSI.

## Critério para avançar além da alpha.9

Antes de considerar a alpha.10 pronta como prerelease geral, continuam prioritários os testes reais:

1. HUD durante gameplay e menus;
2. posição, velocidade e heading;
3. traçado detalhado da rota;
4. destino e próxima parada;
5. multiplayer entre dois computadores;
6. chat e voz push-to-talk;
7. atalhos e conflitos com `keyboard.cfg`;
8. lista de mapas/roadmaps da alpha.10;
9. carregamento real do plugin no OMSI 2.3.004;
10. diagnóstico de instalação do plugin (`install=INSTALLED`, `files=3/3`, `manifest=YES`);
11. painel do bridge com `status=CONNECTED`, `process-match=YES` e `heartbeat=LIVE`;
12. fluxo SignalR → cliente → Named Pipe → plugin entre dois PCs.

Os problemas encontrados nesses testes devem ser corrigidos antes de considerar os recursos correspondentes estáveis.
