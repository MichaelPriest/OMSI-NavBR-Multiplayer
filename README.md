# OMSI NavBR Multiplayer

Companion app independente para **OMSI 2**, com navegação/HUD, telemetria, multiplayer peer-host, chat/voz, CCO, perfil do motorista, Hardware Cockpit e integração experimental com veículos remotos físicos no OMSI.

## Versão pública para testes

A pré-release atual é **`v0.3.0-alpha.13-test.1`**.

- [Release Alpha.13 Test 1](https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.13-test.1)
- cliente principal: **EXE standalone Windows x86**;
- também há ZIP do cliente, servidor dedicado x64, plugin experimental x86, documentação e hashes SHA256.

> A Alpha.13 inicia a validação do **ônibus remoto físico online**. Recursos incompletos continuam marcados como Experimentais/Em desenvolvimento e não usam dados simulados.

## Destaques da Alpha.13 Test 1

- primeiro teste público focado em ônibus dos outros jogadores dentro do OMSI;
- spawn/update/despawn experimental integrado à telemetria real da sala;
- posição, rotação e velocidade usando pose nativa do OMSI 2.3.004;
- luzes e setas quando suportadas pelo backend atual;
- coordenador físico único para evitar spawn/update duplicados;
- sessão continua ativa mesmo ao fechar a Central Multiplayer;
- peer-host TCP `27730` e servidor dedicado opcional;
- salas públicas/privadas, chat e voz PTT;
- diagnóstico de NAT, firewall, UPnP, latência, jitter e perda;
- GPS/HUD, perfil, histórico real de viagens, empresa, CCO e Hardware Cockpit preservados da Alpha.12;
- base de entitlement/licenciamento separada do núcleo, ainda **sem bloqueio comercial** na Alpha/Beta;
- interface em pt-BR, English, Español, Deutsch e Français.

## O que o teste físico ainda não sincroniza

A Test 1 é deliberadamente limitada. Ainda entram nas próximas etapas:

- portas;
- matriz/linha/destino física;
- articulação de ônibus articulado;
- animações específicas por modelo;
- sincronização completa de tráfego IA.

Primeiro queremos validar que **PC A vê o ônibus do PC B e PC B vê o ônibus do PC A** com pose e movimento corretos e sem instabilidade.

## Requisitos principais

- OMSI alvo inicial: **2.3.004**;
- cliente: **.NET 10 / C# / WPF x86**;
- servidor: **ASP.NET Core + SignalR**;
- host da sala: o próprio PC de quem cria a sala;
- porta inicial: **TCP 27730**;
- projeto público e em desenvolvimento ativo.

Para o ônibus remoto 3D, ambos os PCs precisam ter o plugin experimental instalado e o veículo remoto precisa existir localmente no PC que irá renderizá-lo.

## Instalação rápida

1. Baixe o **EXE standalone x86** da Alpha.13 Test 1.
2. Execute o NavBR.
3. Abra o OMSI e carregue mapa/ônibus.
4. Para multiplayer normal, crie ou entre em uma sala.
5. Para testar ônibus físico, instale o plugin experimental e ative **Ônibus dos jogadores no OMSI (TESTE ALPHA)**.
6. Faça o primeiro teste preferencialmente entre dois PCs na mesma rede local.

O plugin de escrita no OMSI não é obrigatório para GPS, HUD, chat, voz ou telemetria multiplayer básica.

## Multiplayer

O computador de quem cria a sala pode funcionar como servidor da própria sessão.

- porta padrão: `TCP 27730`;
- telemetria, presença, chat e voz passam pelo SignalR;
- salas privadas não aparecem no navegador público;
- UPnP é opcional;
- servidor dedicado continua disponível;
- relay/fallback permanece experimental para cenários como CGNAT/double NAT.

## Alpha.13

- [`docs/ALPHA13_TEST1_COMMUNITY.md`](docs/ALPHA13_TEST1_COMMUNITY.md) — checklist do teste físico com 2 PCs;
- [`docs/ALPHA13_MASTER_SCOPE.md`](docs/ALPHA13_MASTER_SCOPE.md) — foco e evolução da Alpha.13;
- [`docs/ALPHA13_TEST1_RELEASE_NOTES.md`](docs/ALPHA13_TEST1_RELEASE_NOTES.md) — notas desta pré-release;
- [`docs/ALPHA12_MASTER_SCOPE.md`](docs/ALPHA12_MASTER_SCOPE.md) — escopo consolidado herdado da Alpha.12.

## Downloads no portal

O portal mostra separadamente:

- downloads de cada build/release;
- downloads acumulados de cada Alpha, somando todas as suas Test builds;
- downloads totais do projeto.

## Segurança

A telemetria externa do OMSI permanece **read-only**. Escritas experimentais ficam isoladas no plugin/bridge, exigem ativação explícita e falham de forma segura quando a capacidade não está disponível.

O projeto não redistribui mapas, ônibus, HOFs ou outros conteúdos proprietários/pagos do OMSI.

## Distribuição futura

A arquitetura passa a separar o entitlement/licenciamento do núcleo do NavBR para permitir futuramente Steam, chave própria ou outra loja. **Alpha/Beta continuam abertas nesta fase de testes.**

## Documentação adicional

- [`docs/HARDWARE_COCKPIT.md`](docs/HARDWARE_COCKPIT.md) — Hardware Cockpit Arduino/ESP32;
- [`docs/NETWORKING.md`](docs/NETWORKING.md) — rede;
- [`docs/PEER_HOST.md`](docs/PEER_HOST.md) — host local;
- [`docs/OMSI_PLUGIN_EXPERIMENTAL.md`](docs/OMSI_PLUGIN_EXPERIMENTAL.md) — plugin/bridge experimental;
- [`docs/MANUAL_DE_USO.md`](docs/MANUAL_DE_USO.md) — manual de uso.

## Portal

O GitHub Pages concentra download, releases, documentação, contagem por Alpha e estado dos testes públicos.

## Licença

Consulte [`LICENSE`](LICENSE) e [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
