# OMSI NavBR Multiplayer

Companion app independente da Steam para **OMSI 2**, com navegação/HUD, telemetria, multiplayer peer-host, chat/voz, integração experimental com veículos remotos no OMSI e Hardware Cockpit.

## Versão pública para testes

A pré-release atual é **`v0.3.0-alpha.12-test.3`**.

- [Baixar / ver a release Alpha.12 Test 3](https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.12-test.3)
- cliente principal: **EXE standalone Windows x86**;
- também há ZIP do cliente, servidor dedicado x64, plugin experimental x86, documentação e hashes SHA256.

> A Alpha.12 ainda está em desenvolvimento. Recursos incompletos aparecem no aplicativo como **Em desenvolvimento** ou **Experimental** em vez de ficarem escondidos do escopo da versão.

## Destaques da Alpha.12 Test 3

- novo shell e HUD Alpha.12;
- lógica de HUD baseada na janela real de gameplay do OMSI;
- GPS, mapa, rota, linha, destino e próxima parada;
- perfil do motorista e estatísticas locais;
- empresa virtual e frota local;
- CCO/Dispatcher local com monitoramento multiplayer;
- multiplayer peer-host na porta TCP `27730`;
- diagnóstico de conectividade e saúde da sessão;
- salas privadas com senha efêmera e salas públicas do servidor configurado;
- UPnP opt-in;
- chat e voz PTT;
- canais Geral, Empresa/Equipe, CCO e Proximidade;
- mute, deafen, ganho individual e seleção de dispositivos;
- plugin/bridge v2 e ônibus remoto físico experimental;
- Hardware Cockpit Serial para Arduino/ESP32;
- interface em pt-BR, English, Español, Deutsch e Français.

## Desenvolvimento após a Test 3

A branch `feature/alpha12-full-expansion` continua evoluindo depois da pré-release Test 3.

A voz multiplayer possui **jitter buffer adaptativo**, reordenação por sequência, recuperação FEC para perda isolada e indicadores ao vivo de jitter/perda na Central Multiplayer. Esses avanços ainda precisam de validação em sessão real entre dois ou mais PCs antes de serem considerados estáveis.

A interface também recebeu o passe **Figma Fidelity Alpha.12**: shell de 252 px, Home operacional, Navegação com seletor `2D | 3D`, Multiplayer integrado, CCO com mapa real, grupo OPERAÇÃO completo e grupo SISTEMA com Hardware, HUD, Configurações e Saúde da Sessão. As telas usam os tokens visuais do design aprovado e mantêm dados reais; quando a fonte não fornece um valor, a interface mostra `—`/indisponível em vez de inventar telemetria.

Detalhes técnicos:

- [`docs/ALPHA12_FIGMA_PREVIEW.md`](docs/ALPHA12_FIGMA_PREVIEW.md)
- [`docs/ALPHA12_VOICE_RESILIENCE.md`](docs/ALPHA12_VOICE_RESILIENCE.md)

## Status do projeto

- versão de desenvolvimento: `0.3.0-alpha.12-dev`;
- OMSI alvo inicial: **2.3.004**;
- perfil técnico legado preservado: **2.2.032**;
- cliente: **.NET 10 / C# / WPF x86**;
- servidor: **ASP.NET Core + SignalR**;
- host da sala: o próprio PC de quem cria a sala;
- servidor dedicado continua disponível como alternativa;
- projeto público e em desenvolvimento ativo.

## Alpha.12

O escopo consolidado da Alpha.12 está em:

- [`docs/ALPHA12_MASTER_SCOPE.md`](docs/ALPHA12_MASTER_SCOPE.md)

Entre os módulos da Alpha.12 estão multiplayer/rede, voz avançada, ônibus remotos 3D, tráfego IA compartilhado, Hardware Cockpit, perfil, empresas virtuais, CCO, replay/Ghost, mapa web, eventos, permissões, SDK, workshop e base para companion/mobile.

## Instalação rápida

1. Baixe o **EXE standalone x86** da release Alpha.12 Test 3.
2. Execute o NavBR.
3. Confirme a instalação do OMSI ou ajuste o perfil se necessário.
4. Abra o OMSI e carregue mapa/ônibus.
5. Use **Navegação** para HUD/GPS ou **Multiplayer** para criar/entrar em uma sala.

O plugin experimental de escrita no OMSI continua separado e protegido por opt-in. Ele não é obrigatório para usar telemetria, GPS, chat ou o multiplayer básico.

## Multiplayer

O computador de quem cria a sala pode funcionar como servidor da própria sessão.

- porta padrão: `TCP 27730`;
- telemetria, presença, chat e voz passam pelo SignalR;
- salas privadas não aparecem no navegador público;
- UPnP é opcional;
- CGNAT/NAT/roteadores ainda podem exigir configuração adicional para acesso pela Internet.

Para começar, prefira testar primeiro entre dois PCs na mesma rede local.

## Segurança

A leitura de telemetria do OMSI permanece **read-only**. Escritas experimentais no simulador ficam isoladas no plugin/bridge, exigem ativação explícita e continuam marcadas como experimentais.

O projeto não redistribui mapas, ônibus ou outros conteúdos proprietários/pagos do OMSI.

## Documentação

- [`docs/ALPHA12_TEST3_COMMUNITY.md`](docs/ALPHA12_TEST3_COMMUNITY.md) — checklist da Test 3;
- [`docs/ALPHA12_FIGMA_PREVIEW.md`](docs/ALPHA12_FIGMA_PREVIEW.md) — implementação visual e contrato de dados reais;
- [`docs/ALPHA12_VOICE_RESILIENCE.md`](docs/ALPHA12_VOICE_RESILIENCE.md) — jitter buffer, FEC e métricas de voz;
- [`docs/HARDWARE_COCKPIT.md`](docs/HARDWARE_COCKPIT.md) — protocolo e exemplo físico;
- [`docs/NETWORKING.md`](docs/NETWORKING.md) — rede;
- [`docs/PEER_HOST.md`](docs/PEER_HOST.md) — host local;
- [`docs/OMSI_PLUGIN_EXPERIMENTAL.md`](docs/OMSI_PLUGIN_EXPERIMENTAL.md) — plugin/bridge experimental;
- [`docs/MANUAL_DE_USO.md`](docs/MANUAL_DE_USO.md) — manual de uso.

## Portal

O GitHub Pages do projeto concentra download, visão geral da Alpha.12, links de documentação, contribuição e apoio ao desenvolvimento.

## Licença

Consulte [`LICENSE`](LICENSE) e [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
