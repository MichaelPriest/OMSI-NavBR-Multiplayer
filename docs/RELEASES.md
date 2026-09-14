# Releases

O OMSI NavBR Multiplayer usa versionamento semântico (SemVer) e GitHub Actions para publicar versões testáveis.

## Convenção de versão

- `v0.x.y-alpha.n` — desenvolvimento inicial, APIs e interface ainda podem mudar;
- `v0.x.y-beta.n` — funcionalidades principais já integradas, foco em estabilidade;
- `v0.x.y` — versão estável.

## Publicação

O workflow `.github/workflows/release.yml` é acionado por tags `v*`.

Exemplo:

```text
v0.2.0-alpha.1
```

O workflow:

1. restaura e compila o projeto com .NET 10;
2. publica o cliente Windows x86 self-contained;
3. publica o servidor Windows x64 self-contained;
4. gera arquivos ZIP;
5. cria um GitHub Prerelease com notas automáticas.

## Artefatos

Cada prerelease gera:

```text
OMSI-NavBR-Multiplayer-<versão>-win-x86.zip
OMSI-NavBR-Server-<versão>-win-x64.zip
```

O cliente é x86 porque o alvo inicial de integração é o OMSI 2. O servidor não depende da arquitetura do simulador e é publicado em x64.

## Critério para publicar alpha

Uma alpha pode ser publicada quando:

- o CI estiver verde;
- o aplicativo iniciar sem erro;
- a nova funcionalidade estiver suficientemente completa para teste;
- limitações conhecidas estiverem documentadas no `CHANGELOG.md`.

Não é necessário esperar todas as fases do projeto para publicar uma nova alpha. O objetivo é disponibilizar builds progressivamente para validação real no OMSI.
