# OMSI NavBR Multiplayer

Companion app independente para **OMSI 2**, com navegação/HUD, telemetria, multiplayer peer-host, chat/voz, CCO, perfil do motorista, Hardware Cockpit, integração experimental com veículos remotos físicos e **modo Personagem/RP**.

## Versão pública atual

A próxima publicação pública é **v0.3.0-alpha.14**.

- cliente principal: **EXE standalone Windows x86**;
- ZIP do cliente;
- servidor dedicado Windows x64;
- plugin OMSI Native AOT x86;
- simulador multiplayer de desenvolvimento/teste;
- documentação e SHA256SUMS.

> A Alpha.14 continua sendo uma **prerelease pública**. Recursos de escrita física no OMSI permanecem experimentais e opt-in.

## Destaques da Alpha.14

- shell Figma consolidado e funções restauradas;
- Home com **Executar OMSI**, Navegação, Mapa 3D, Multiplayer, CCO, Empresa/Frota, Perfil e ferramentas;
- **Mover HUD** visível no topo, Sistema e ações rápidas;
- selects/ComboBox com tema escuro consistente;
- Central Multiplayer sem o wizard legado sobreposto;
- abas: Visão geral, Sala, Jogadores, Chat & Voz, Personagem/RP e Avançado;
- salas públicas/privadas, senha, convite, peer-host TCP 27730, UPnP e relay experimental;
- Firewall do Windows configurável para TCP 27730 em todos os perfis de rede;
- Plugin Bridge **v3** + interop RP v3;
- modo Personagem/RP disponível também sem multiplayer;
- ônibus remoto físico experimental;
- simulador multiplayer com bots no **mesmo mapa**, **próximos do host** e herdando **linha/rota/destino/próxima parada** da operação ativa da sala;
- interface pt-BR, English, Español, Deutsch e Français.

## Executar OMSI pelo NavBR

Na Home há um atalho **Executar OMSI**. O NavBR usa a instalação real detectada/cadastrada em **Instalações OMSI** e prioriza o perfil preferido.

Se nenhuma instalação válida for encontrada, o app abre a seleção de instalações em vez de usar um caminho fixo ou depender da Steam.

## Multiplayer

O computador de quem cria a sala pode funcionar como servidor da própria sessão.

- porta padrão: TCP 27730;
- telemetria, presença, chat e voz passam pelo SignalR;
- salas privadas não aparecem no navegador público;
- UPnP é opcional;
- servidor dedicado continua disponível;
- relay/fallback permanece experimental;
- o host direto pode exigir Firewall/port forwarding dependendo da rede.

## Simulador Multiplayer

O simulador é somente para desenvolvimento/teste e não injeta dados fake na interface de produção.

Quando executado contra uma sala real:

1. detecta o mapa da autoridade/jogador real;
2. herda MapName e compatibilidade;
3. espera telemetria real para usar a posição do host como centro;
4. posiciona os bots em um raio curto, por padrão **18 m**;
5. herda a operação ativa da sala: **linha, rota, destino e próxima parada**;
6. rejeita no modo --verify bots que publiquem em mapa diferente.

Se 127.0.0.1:27730 estiver vazio, o pacote do simulador pode iniciar automaticamente o NavBR.Server incluído.

Veja [docs/MULTIPLAYER_SIMULATOR.md](docs/MULTIPLAYER_SIMULATOR.md).

## Modo Personagem / RP

O modo RP é experimental.

- usa personagens reais da lista Drivers do mapa;
- controle inicial: W/S, A/D, Shift e Esc;
- bridge/plugin v3;
- restauração de vínculo/IA ao voltar ao ônibus;
- sincronização RP separada no multiplayer.

Ainda exigem validação física mais ampla: câmera dedicada, terreno inclinado, animações/gestos, interação com objetos/veículos e personagem remoto físico completo.

## Requisitos principais

- OMSI alvo inicial: **2.3.004**;
- cliente: **.NET 10 / C# / WPF x86**;
- servidor: **ASP.NET Core + SignalR**;
- host da sala: o próprio PC de quem cria a sala;
- porta padrão: **TCP 27730**;
- projeto público.

## Documentação

- [docs/ALPHA14_RELEASE_NOTES.md](docs/ALPHA14_RELEASE_NOTES.md) — notas da Alpha.14 pública;
- [docs/ALPHA14_COMMUNITY.md](docs/ALPHA14_COMMUNITY.md) — roteiro de teste;
- [docs/ALPHA14_MASTER_SCOPE.md](docs/ALPHA14_MASTER_SCOPE.md) — escopo consolidado;
- [docs/MULTIPLAYER_SIMULATOR.md](docs/MULTIPLAYER_SIMULATOR.md) — simulador;
- [docs/NETWORKING.md](docs/NETWORKING.md) — rede/Firewall/UPnP/relay;
- [docs/PEER_HOST.md](docs/PEER_HOST.md) — host local;
- [docs/OMSI_PLUGIN_EXPERIMENTAL.md](docs/OMSI_PLUGIN_EXPERIMENTAL.md) — plugin v3;
- [docs/HARDWARE_COCKPIT.md](docs/HARDWARE_COCKPIT.md) — Hardware Cockpit;
- [docs/MANUAL_DE_USO.md](docs/MANUAL_DE_USO.md) — manual.

## Segurança

A telemetria externa do OMSI permanece **read-only**. Escritas experimentais ficam isoladas no plugin/bridge, exigem ativação explícita e falham de forma segura quando a capacidade não está disponível.

O projeto não redistribui mapas, ônibus, HOFs ou outros conteúdos proprietários/pagos do OMSI.

## Portal

O GitHub Pages concentra downloads, releases, documentação e estado dos testes públicos.

## Licença

Consulte [LICENSE](LICENSE) e [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
