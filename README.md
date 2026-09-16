# OMSI NavBR Multiplayer

[![Downloads](https://img.shields.io/github/downloads/MichaelPriest/OMSI-NavBR-Multiplayer/total?label=downloads&color=22c77a)](https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases)

Aplicativo de navegação e multiplayer para **OMSI 2**, independente da Steam.

> Versão em desenvolvimento: **0.3.0-alpha.11**  
> Teste público atual: **[v0.3.0-alpha.11-test.2](https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.11-test.2)**  
> Release oficial anterior: **[v0.3.0-alpha.10](https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.10)**

**Site oficial:** https://michaelpriest.github.io/OMSI-NavBR-Multiplayer/  
**Todas as releases:** https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases  
**Manual:** [docs/MANUAL_DE_USO.md](docs/MANUAL_DE_USO.md)  
**Checklist comunitário da Test 2:** [docs/ALPHA11_TEST2_COMMUNITY.md](docs/ALPHA11_TEST2_COMMUNITY.md)

## Alpha.11 Test 2

A `v0.3.0-alpha.11-test.2` é uma **pré-release pública para a comunidade**. Ela reúne a correção da velocidade, melhorias do HUD, paradas por rota ativa, infraestrutura de diagnóstico e a primeira rodada pública do ônibus remoto físico 3D experimental.

Principais mudanças:

- velocidade do HUD corrigida usando `Groundspeed` do OMSI com fallback para a velocidade linear real;
- HUD/minimapa mais compacto e transparente;
- HUD aprende a janela real de gameplay do OMSI e se esconde em menus/opções/timetable;
- com rota ativa, o HUD mostra **somente as paradas da viagem/linha ativa**; sem rota ativa, pode mostrar todas as paradas válidas do mapa;
- manual para iniciantes disponível dentro do próprio aplicativo e sem necessidade de internet;
- créditos visíveis no app: **Desenvolvedor: MichaelPriest • Com apoio da IA ChatGPT**;
- multiplayer peer-host pela porta TCP `27730`, chat de texto e voz push-to-talk;
- meta inicial de até **32 jogadores por sala**;
- snapshots de tráfego com até 48 veículos relevantes separados dos jogadores;
- plugin Native AOT x86 + `NavBR.OmsiInterop.dll` para OMSI 2.3.004;
- ônibus remoto físico 3D disponível como **EXPERIMENTAL / opt-in**;
- diagnósticos automáticos opcionais, desligados por padrão, com fila local e envio sanitizado em lote.

### Ônibus remoto físico 3D

O recurso 3D é experimental e vem desligado por padrão. Ele existe nesta Test 2 para validação comunitária de `spawn → atualização → despawn` de veículos remotos.

Para testar com menor risco:

1. use OMSI **2.3.004**;
2. instale/atualize o plugin pelo próprio NavBR com o OMSI fechado;
3. use o mesmo mapa/build compatível nos computadores;
4. cada PC precisa ter localmente o modelo de ônibus usado pelo outro jogador no mesmo caminho relativo dentro de `Vehicles\`;
5. ative **Ônibus remoto 3D (EXPERIMENTAL)** na janela Multiplayer;
6. comece com poucos jogadores e reporte posicionamento, rotação, luzes, FPS e qualquer crash.

O NavBR **não transfere nem redistribui ônibus pagos/proprietários**.

> O backend 3D ainda não deve ser tratado como estável. Posicionamento entre tiles, compatibilidade com add-ons e estabilidade precisam de testes reais amplos antes de promover o recurso.

## HUD, GPS e rota

A Alpha.11 trabalha com:

- posição local e absoluta do veículo;
- `GridX/GridY` + coordenadas locais de tile;
- heading/quaternion;
- velocidade;
- linha/track, destino e próxima parada quando o timetable fornece os dados;
- rota ativa a partir de `.ttp/.ttr`, tiles `.map`, splines `.sli` e paths de objetos/crossings `.sco`;
- roadmaps como `whole.roadmap.bmp` quando disponíveis;
- marcadores de jogadores remotos suavizados;
- ocultação do HUD em janelas auxiliares do OMSI.

### Regra das paradas

- **Rota/viagem ativa:** somente as paradas daquela rota devem aparecer.
- **Sem rota ativa:** todas as paradas válidas do mapa podem aparecer.
- O NavBR tenta resolver a sequência real de `[station]` no `.ttp`; quando isso não é possível, usa a geometria da rota como fallback sem voltar a poluir o HUD com o mapa inteiro.

## Multiplayer

Na série 0.3 o fluxo principal é **peer-host**:

1. quem cria a sala hospeda o servidor no próprio PC;
2. porta inicial: TCP `27730`;
3. convidados entram pelo endereço do host e nome/convite da sala;
4. SignalR transporta presença, telemetria, chat, voz e estados compartilhados;
5. servidor dedicado x64 continua disponível como alternativa.

Meta inicial: **até 32 jogadores por sala**, sujeita a validação de desempenho. O limite de até 48 veículos de tráfego IA por snapshot é separado do número de jogadores.

Atalhos padrão:

- `F9` — chat de texto;
- `F10` — segurar para falar.

## Diagnósticos automáticos

A Alpha.11 Test 2 inclui diagnóstico remoto **opcional e desligado por padrão**. Quando o usuário autoriza, o cliente pode enviar eventos técnicos sanitizados, como:

- versão do NavBR e OMSI;
- mapa e identificação técnica do veículo;
- estado do plugin/bridge e do recurso 3D;
- erros técnicos e exceções;
- informações básicas necessárias para reproduzir falhas.

Não são enviados chat, áudio/voz, senha, token ou arquivos pessoais. Se estiver offline, os eventos ficam em uma fila local limitada e são enviados em lote quando possível.

## Manual para iniciantes

O aplicativo possui um botão **Manual de uso** na barra lateral da Alpha.11. O guia funciona offline e explica em linguagem simples:

- primeiros passos;
- HUD, GPS, velocidade e paradas;
- criar/entrar em sala;
- chat e voz;
- ônibus remoto 3D experimental;
- diagnósticos;
- solução de problemas comuns.

O manual interno acompanha Português, English, Español, Deutsch e Français.

## Plugin e integração OMSI

A camada experimental inclui:

- plugin Native AOT x86;
- `.opl` para carregamento pelo OMSI;
- Named Pipe local cliente ↔ plugin;
- protocolo bridge versionado;
- fila de comandos executada pelo callback/thread do OMSI;
- shim C++ x86 para o ABI Borland/Delphi do OMSI 2.3.004;
- registro protegido `VehicleInstanceId → ponteiro OMSI` para veículos criados pelo NavBR;
- validação de ponteiros e lista `RoadVehicles` antes de writes físicos;
- opt-in explícito para operações experimentais.

A integração normal de telemetria continua externa e prioritariamente de leitura. As escritas físicas pertencem exclusivamente ao caminho experimental do plugin.

## Pacotes da Test 2

A pré-release publica:

```text
OMSI-NavBR-Multiplayer-v0.3.0-alpha.11-test.2-win-x86.exe
OMSI-NavBR-Multiplayer-v0.3.0-alpha.11-test.2-win-x86.zip
OMSI-NavBR-Server-v0.3.0-alpha.11-test.2-win-x64.zip
OMSI-NavBR-Plugin-v0.3.0-alpha.11-test.2-win-x86.zip
ALPHA11_TEST2_COMMUNITY.md
SHA256SUMS.txt
LICENSE
THIRD_PARTY_NOTICES.md
```

Para a maioria dos usuários, o **EXE standalone x86** é o arquivo recomendado.

## Compatibilidade

- Windows;
- OMSI 2.3.004 como alvo principal;
- cliente WPF x86;
- servidor dedicado x64 opcional;
- .NET 10 self-contained nos pacotes publicados;
- independente de Steam API/Steamworks.

## Stack

- .NET 10 / C# / WPF x86
- ASP.NET Core / Kestrel / SignalR
- NAudio + Concentus/Opus
- Windows Named Pipes
- Native AOT
- C++/MSVC x86 no interop experimental
- `.resx` / `ResourceManager`

## Documentação

- [Manual de uso](docs/MANUAL_DE_USO.md)
- [Alpha.11 Test 2 — comunidade](docs/ALPHA11_TEST2_COMMUNITY.md)
- [Desenvolvimento Alpha.11](docs/ALPHA11_DEVELOPMENT.md)
- [Plugin OMSI experimental](docs/OMSI_PLUGIN_EXPERIMENTAL.md)
- [HUD e voz](docs/HUD_AND_VOICE.md)
- [Telemetria](docs/TELEMETRY.md)
- [Arquitetura](docs/ARCHITECTURE.md)
- [Releases](docs/RELEASES.md)
- [Roadmap](docs/ROADMAP.md)

## Créditos

**Desenvolvedor:** MichaelPriest  
**Apoio ao desenvolvimento:** IA ChatGPT

## Licença

O código próprio do OMSI NavBR Multiplayer é disponibilizado sob licença **MIT**. Dependências e avisos de terceiros estão em `THIRD_PARTY_NOTICES.md` e `licenses/`.
