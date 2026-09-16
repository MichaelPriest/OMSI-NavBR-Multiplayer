# OMSI NavBR Multiplayer

[![Downloads](https://img.shields.io/github/downloads/MichaelPriest/OMSI-NavBR-Multiplayer/total?label=downloads&color=22c77a)](https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases)

Aplicativo de navegação e multiplayer para **OMSI 2**, independente da Steam, com HUD/GPS, peer-host, salas públicas/privadas, chat, voz, CCO, integração experimental com o OMSI e Hardware Cockpit.

> Versão em desenvolvimento: **0.3.0-alpha.12**  
> Teste público atual: **[v0.3.0-alpha.12-test.2](https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.12-test.2)**  
> Release anterior: **[v0.3.0-alpha.12-test.1](https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.12-test.1)**

**Site oficial:** https://michaelpriest.github.io/OMSI-NavBR-Multiplayer/  
**Releases:** https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases  
**Checklist Alpha.12 Test 2:** [docs/ALPHA12_TEST2_COMMUNITY.md](docs/ALPHA12_TEST2_COMMUNITY.md)  
**Escopo mestre Alpha.12:** https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/blob/feature/alpha12-full-expansion/docs/ALPHA12_MASTER_SCOPE.md  
**Hardware Cockpit:** [docs/HARDWARE_COCKPIT.md](docs/HARDWARE_COCKPIT.md)

## Alpha.12 Test 2

A `v0.3.0-alpha.12-test.2` é a segunda pré-release pública da Alpha.12. Ela corrige o pacote interno do plugin da Test 1 e traz a primeira rodada ampla da nova interface de central operacional.

### Correção importante do plugin

Na Test 1, o instalador interno exigia `NavBR.OmsiInterop.dll`, mas o script que montava o bundle embutido no cliente não incluía essa DLL. A Test 2 corrige o empacotamento e passa a validar obrigatoriamente as três peças antes de publicar:

- `NavBR.OmsiPlugin.dll`;
- `NavBR.OmsiInterop.dll`;
- `NavBR.OmsiPlugin.opl`.

O pipeline da Test 2 também abre o ZIP interno e falha se qualquer uma delas estiver ausente.

### Interface Alpha.12

- shell em formato de central operacional;
- Home com resumo da operação atual;
- Navegação com painel de linha, rota, destino, próxima parada, rua, velocidade e atraso;
- Central Multiplayer remodelada;
- CCO / Operação remodelado;
- perfil do motorista em formato de carreira/estatísticas;
- empresa e frota em dashboard;
- configurações organizadas por categorias;
- barra lateral com rolagem;
- ferramentas técnicas preservadas no modo avançado.

### Multiplayer e recursos preservados

- multiplayer peer-host TCP `27730`;
- diagnóstico de conectividade, NAT e UPnP opt-in;
- salas privadas com senha efêmera e navegador de salas públicas do servidor;
- chat e PTT;
- voz Geral, Empresa/Equipe, CCO e Proximidade;
- mute, deafen, ganho por jogador e seleção de dispositivos de áudio;
- plugin Native AOT x86 e bridge v2 experimentais;
- Hardware Cockpit Serial.

### Em desenvolvimento na Alpha.12

- presença global e descoberta opcional de salas pela Internet;
- NAT traversal/fallback/relay avançado;
- ônibus remoto físico 3D completo e tráfego IA compartilhado;
- Hardware Cockpit Wi-Fi/ESP32, displays e entradas físicas;
- navegação avançada com ETA/distâncias/manobras quando houver dados reais suficientes;
- CCO avançado, permissões e moderação;
- replay/Ghost, mapa web ao vivo e eventos;
- SDK/API, workshop e companion/mobile.

## Multiplayer

O fluxo principal continua **peer-host**: quem cria a sala hospeda no próprio PC pela porta TCP `27730`. O servidor dedicado x64 permanece opcional. A Alpha.12 adiciona salas privadas e um navegador das salas públicas anunciadas pelo servidor configurado; salas privadas não aparecem nesse diretório.

## Voz

Atalhos padrão:

- `F9` — chat;
- `F10` — segurar para falar.

A Alpha.12 inclui canais Geral, Empresa/Equipe, CCO e Proximidade, além de mute/deafen, ganho individual e seleção de microfone/saída.

## HUD, GPS e OMSI

O HUD preserva a janela real de gameplay do OMSI e deve ficar oculto em menus, opções e janelas auxiliares. A telemetria normal permanece prioritariamente de leitura; operações físicas continuam isoladas atrás de opt-in experimental.

## Pacotes da Alpha.12 Test 2

```text
OMSI-NavBR-Multiplayer-v0.3.0-alpha.12-test.2-win-x86.exe
OMSI-NavBR-Multiplayer-v0.3.0-alpha.12-test.2-win-x86.zip
OMSI-NavBR-Server-v0.3.0-alpha.12-test.2-win-x64.zip
OMSI-NavBR-Plugin-v0.3.0-alpha.12-test.2-win-x86.zip
ALPHA12_TEST2_COMMUNITY.md
ALPHA12_MASTER_SCOPE.md
HARDWARE_COCKPIT.md
SHA256SUMS.txt
LICENSE
THIRD_PARTY_NOTICES.md
```

Para a maioria dos usuários, o **EXE standalone x86** é o pacote recomendado.

## Compatibilidade

- Windows;
- OMSI 2.3.004 como alvo principal;
- cliente WPF x86;
- servidor dedicado x64 opcional;
- .NET 10 self-contained;
- independente da Steam API/Steamworks.

## Documentação

- [Alpha.12 Test 2 — comunidade](docs/ALPHA12_TEST2_COMMUNITY.md)
- [Hardware Cockpit Bridge](docs/HARDWARE_COCKPIT.md)
- [Manual de uso](docs/MANUAL_DE_USO.md)
- [Plugin OMSI experimental](docs/OMSI_PLUGIN_EXPERIMENTAL.md)
- [HUD e voz](docs/HUD_AND_VOICE.md)
- [Telemetria](docs/TELEMETRY.md)
- [Arquitetura](docs/ARCHITECTURE.md)
- [Releases](docs/RELEASES.md)
- [Roadmap](docs/ROADMAP.md)

## Apoie o desenvolvimento

O **OMSI NavBR Multiplayer** é um projeto independente e público. Contribuições são voluntárias e ajudam com desenvolvimento, infraestrutura e testes da comunidade.

**Pix — chave aleatória:** `b07a9cc9-b10d-48a8-b201-d28bddc4399a`

## Créditos

**Desenvolvedor:** MichaelPriest  
**Apoio ao desenvolvimento:** IA ChatGPT

## Licença

O código próprio do OMSI NavBR Multiplayer é disponibilizado sob licença **MIT**. Dependências e avisos de terceiros estão em `THIRD_PARTY_NOTICES.md` e `licenses/`.
