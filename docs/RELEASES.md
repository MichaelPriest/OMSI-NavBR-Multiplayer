# Releases

O OMSI NavBR Multiplayer usa versionamento semântico (SemVer) e GitHub Actions para publicar builds de teste e releases gerais.

## Convenção

- `v0.x.y-alpha.n` — prerelease geral da série alpha;
- `v0.x.y-alpha.n-test.m` — prerelease pública de integração/teste comunitário;
- `v0.x.y-beta.n` — fase beta;
- `v0.x.y` — release estável.

## Estado atual

### Teste público atual

```text
v0.3.0-alpha.14-test.6
```

Release:

https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.14-test.6

A Test 6 é a prerelease pública atual da Alpha.14. Ela consolida as correções mais recentes de Navegação, Personagem/RP, simulador e ônibus remoto físico.

### Destaques da Alpha.14 Test 6

- interface principal React/WebView2;
- três modos de multiplayer: Servidor NavBR oficial, LAN e Online através do Host;
- Navegação 2D/3D com roadmap real e retorno à rota;
- Personagem/RP com resolução do motorista humano ativo;
- ônibus remoto físico experimental com spawn/update/despawn, Kachel/tile, interpolação e diagnóstico por jogador;
- simulador com verificação física real por `MakeVehicle`;
- cliente Windows x86, plugin Native AOT x86, servidor dedicado e simulador;
- checksums SHA-256 nos pacotes publicados.

### Pacotes publicados

A Test 6 publica:

```text
OMSI-NavBR-Multiplayer-v0.3.0-alpha.14-test.6-win-x86.exe
OMSI-NavBR-Multiplayer-v0.3.0-alpha.14-test.6-win-x86.zip
OMSI-NavBR-Server-v0.3.0-alpha.14-test.6-win-x64.zip
OMSI-NavBR-Plugin-v0.3.0-alpha.14-test.6-win-x86.zip
OMSI-NavBR-Multiplayer-Simulator-v0.3.0-alpha.14-test.6-win-x64-dev.zip
SHA256SUMS.txt
```

O **EXE standalone x86** é a opção recomendada para a maioria dos usuários.

### Recursos experimentais

Ônibus remoto físico e Personagem/RP continuam experimentais. O NavBR não redistribui mapas, ônibus, HOFs ou outros conteúdos pagos/proprietários do OMSI.

Os gates de CI validam build, arquitetura x86, bridge, cliente, servidor, simulador, pacote e checksums, mas não substituem validação visual em uma instalação real do OMSI.

## Histórico recente

- `v0.3.0-alpha.11-test.1` — primeira build pública da Alpha.11;
- `v0.3.0-alpha.14-test.6` — prerelease pública atual com integração React, correções de navegação/RP e ônibus físico experimental;
- `v0.3.0-alpha.14-test.5` — rodada de validação anterior;
- `v0.3.0-alpha.11-test.2` — velocidade corrigida, HUD compacto, filtro de paradas, manual interno, diagnósticos e primeira rodada pública do 3D experimental;
- `v0.3.0-alpha.10` — release oficial anterior, baseada na linha de integração Alpha.10.

## Site e catálogo

O portal oficial é:

https://michaelpriest.github.io/OMSI-NavBR-Multiplayer/

O GitHub Pages lê `site/releases.json` e apresenta os downloads publicados. O workflow de release atualiza esse catálogo após a publicação.

## Créditos

**Desenvolvedor:** MichaelPriest  
**Apoio ao desenvolvimento:** IA ChatGPT
