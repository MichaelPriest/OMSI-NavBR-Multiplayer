# Releases

O OMSI NavBR Multiplayer usa versionamento semântico (SemVer) e GitHub Actions para publicar versões testáveis.

## Convenção de versão

- `v0.x.y-alpha.n` — desenvolvimento inicial; funcionalidades e interface ainda podem mudar;
- `v0.x.y-beta.n` — funcionalidades principais integradas, com foco maior em estabilidade;
- `v0.x.y` — versão estável.

## Estado atual

A release mais recente é:

```text
v0.3.0-alpha.9
```

Ela é publicada como **Prerelease**, porque HUD, traçado detalhado, destino/próxima parada, voz, atalhos e multiplayer peer-host ainda precisam de validação real mais ampla no OMSI e entre computadores.

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

## Artefatos oficiais

Cada prerelease publica:

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

é a opção mais simples para a maioria dos usuários. Ele é self-contained e inclui o runtime necessário.

### Cliente ZIP

O ZIP x86 contém a publicação completa do cliente e é útil para diagnóstico, distribuição manual ou quando o usuário prefere trabalhar com a pasta completa da aplicação.

### Servidor ZIP

O servidor dedicado x64 é opcional. Na série 0.3, o modo principal de multiplayer é **peer-host**: o próprio PC de quem cria a sala inicia o host da sessão.

## Site e contadores

O GitHub Pages consulta os assets das releases para mostrar:

- versão atual;
- downloads por arquivo;
- downloads por release;
- total de downloads oficiais.

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

Antes de uma nova etapa grande de desenvolvimento, a prioridade é validar a alpha.9 em ambiente real:

1. HUD durante gameplay e menus;
2. posição, velocidade e heading;
3. traçado detalhado da rota;
4. destino e próxima parada;
5. multiplayer entre dois computadores;
6. chat e voz push-to-talk;
7. atalhos e conflitos com `keyboard.cfg`.

Os problemas encontrados nesses testes devem ser corrigidos antes de considerar os recursos correspondentes estáveis.
