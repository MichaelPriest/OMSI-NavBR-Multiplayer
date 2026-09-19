# Releases

O OMSI NavBR Multiplayer usa SemVer e GitHub Actions para publicar prereleases e releases.

## Estado atual / Current state

```text
v0.3.0-alpha.16
```

Release:

https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.16

### Português (pt-BR)

A Alpha.16 consolida e valida:
- React/WebView2 como interface principal;
- multiplayer Servidor NavBR, LAN e Online através do Host;
- navegação 2D/3D com roadmap real;
- Plugin Bridge v3 + state interop ABI v7;
- ônibus remoto físico com Kachel resolvida localmente por GridX/GridY;
- RP com probe nativo separado do backend de ônibus físico e restauração confirmada do motorista;
- players reais e simulados exibidos no roadmap 2D da Navegação e da Operação/CCO;
- retorno à rota com grafo ampliado por paths de cruzamentos/objetos;
- simulador com PhysicalGridX/PhysicalGridY para validação do spawn físico;
- updater do plugin por fingerprint SHA-256;
- Portal V2 no GitHub Pages.

O ônibus físico e o RP físico continuam experimentais até validação ampla no OMSI real.

### English (en)

Alpha.16 consolidates and validates:
- React/WebView2 as the main desktop UI;
- NavBR Server, LAN and Internet Host multiplayer modes;
- 2D/3D navigation using the real roadmap;
- Plugin Bridge v3 + state interop ABI v7;
- experimental remote physical buses resolving the local Kachel from GridX/GridY;
- Character/RP with a native readiness probe separated from the physical-bus backend and confirmed driver restoration;
- real and simulated players rendered on the 2D Navigation and Operations/CCO roadmap;
- route-rejoin graph expanded with crossing/object paths;
- simulator PhysicalGridX/PhysicalGridY for physical-spawn validation;
- SHA-256 based plugin bundle update detection;
- Portal V2 on GitHub Pages.

Physical bus injection and physical Character/RP remain experimental until broader real-OMSI validation.

## Pacotes / Packages

O workflow geral publica:
- `OMSI-NavBR-Multiplayer-v0.3.0-alpha.16-win-x86.exe`;
- `OMSI-NavBR-Multiplayer-v0.3.0-alpha.16-win-x86.zip`;
- `OMSI-NavBR-Server-v0.3.0-alpha.16-win-x64.zip`.

O EXE standalone x86 é a opção recomendada para a maioria dos usuários.

## Histórico recente / Recent history

- `v0.3.0-alpha.16` — validação do mapa 2D multiplayer, retorno à rota, RP desacoplado e spawn físico via simulador;
- `v0.3.0-alpha.15` — consolidação de runtime físico, RP, plugin updater e Portal V2;
- `v0.3.0-alpha.14-test.6` — última Test da série Alpha.14;
- `v0.3.0-alpha.14` — Alpha.14 pública;
- `v0.3.0-alpha.13-test.1` — testes físicos iniciais.

## Portal

https://michaelpriest.github.io/OMSI-NavBR-Multiplayer/

## Créditos / Credits

**Desenvolvedor / Developer:** MichaelPriest  
**Apoio ao desenvolvimento / Development assistance:** IA ChatGPT
