# Alpha.14 — notas da versão pública

Versão pública atual: **v0.3.0-alpha.14-test.4**

Candidata privada atual: **v0.3.0-alpha.14-test.5**

A Alpha.14 consolida as builds de teste anteriores e a **v0.3.0-alpha.14-test.4** passa a ser a release pública atual do OMSI NavBR Multiplayer.

## Interface principal

- **React + TypeScript + Vite em WebView2 como shell principal**;
- host .NET/WPF x86 preservado temporariamente apenas para serviços nativos ainda acoplados ao `MainWindow`;
- shell WPF anterior retirado do fluxo do usuário; `MainWindow` permanece somente como objeto técnico em memória e não recebe `Show()`;
- antigos installers e renderização visual do shell Alpha.11/12 deixam de rodar; falha do WebView2 é exibida no painel de erro da própria janela nova, sem revelar o layout antigo;
- fechar a interface React mantém o NavBR na bandeja, e o ícone da bandeja sempre reabre o React;
- Home com Executar OMSI e dados reais da operação;
- Navegação/GPS, Central Multiplayer, CCO, Empresa/Frota, Perfil, Personagem/RP, Ghost/Replay, Hardware Cockpit, Instalações OMSI, HUD, Roadmap Studio, Diagnóstico e Rede migrados para React;
- Instalações OMSI permitem selecionar uma pasta real pelo Windows, validar `Omsi.exe`, abrir a instalação no Explorer, editar nome/argumentos, definir perfil preferido, remover e iniciar o simulador; quando não existe perfil válido, **Executar OMSI** direciona para essa tela React em vez de abrir o editor WPF;
- o Mapa 3D faz parte da Navegação React e usa o roadmap real; somente a interação **Mover HUD** continua nativa. Renderização/interação do overlay, geração de roadmap e runtime físico do RP continuam sob autoridade do C#.

## Navegação

- geometria real da rota OMSI;
- paradas ordenadas quando resolvidas com segurança;
- próxima parada, progresso, distância restante e manobra;
- detecção de desvio de rota;
- ETA adaptativa somente após amostras confiáveis;
- modos Seguir ônibus e Rota completa;
- alternância 2D/3D dentro da própria Navegação React, com roadmap real, seguir ônibus, visão aérea e ônibus remotos compatíveis.

## Central Multiplayer

- Criar/Entrar em sala sem wizard sobreposto;
- salas públicas e privadas;
- senha efêmera;
- diretório, busca e favoritos;
- compatibilidade de mapa/plugin/HOF antes da entrada direta;
- jogadores, latência, mapa real da sessão, chat e voz;
- seleção de microfone/saída e mixer temporário de mute/ganho por jogador usando o `VoiceChatService` nativo;
- Personagem/RP com catálogo real de `Map.Drivers`, seleção e comandos Sair/Retornar ao ônibus;
- três modos separados: Servidor NavBR oficial, LAN e Online através do Host;
- no **Online através do Host**, o servidor TCP 27730 inicia antes da tentativa de UPnP;
- UPnP roda em segundo plano com timeout de 8 segundos, sem bloquear a interface;
- se o roteador não responder ou o mapeamento falhar, a sala continua disponível em LAN.

## CCO / Empresa

- motoristas remotos vindos do feed real da sessão;
- ocorrências operacionais, com reconhecer/resolver quando há autoridade;
- Empresa/Frota e Perfil usando os stores nativos;
- cadastro de frota usa o ônibus real atualmente carregado no OMSI.

## Hardware Cockpit

- uma única conexão serial compartilhada entre React e WPF;
- protocolo \`NAVBR_HW_V1\`;
- envio pelo tick oficial de telemetria a aproximadamente 5 Hz;
- COM/baud persistidos;
- auto-reconnect tenta somente a mesma COM explicitamente escolhida;
- preview do pacote real enviado ao Arduino/ESP32.

## Rede

- TCP 27730 para LAN/Online através do Host;
- regra Windows Firewall para todos os perfis de rede;
- elevação UAC e verificação posterior;
- aba Rede separa Firewall, listener local, UPnP, NAT/CGNAT e teste externo;
- UPnP automático não pode ser alterado durante hospedagem;
- hotfix público `29f30b2`: DNS/socket/UPnP deixaram de bloquear o fluxo do clique em **Online através do Host**;
- teste externo só é executado quando o serviço de callback estiver configurado.

## Plugin / RP

- Named Pipe \`OMSI.NavBR.Multiplayer.Plugin.v3\`;
- ABI/state v3;
- capabilities atualizadas em runtime;
- personagem real do mapa, selecionado pela interface React;
- HUD e auto-prompt direcionam para a tela RP React;
- restauração de pose/vínculo/IA;
- ônibus remoto físico continua experimental;
- ônibus remoto físico agora usa interpolação adaptativa no thread do OMSI em vez de aplicar cada frame como salto direto;
- posição, quaternion e velocidade são suavizados; frames fora de ordem são ignorados e teleportes grandes usam snap seguro;
- falhas transitórias de atualização têm tolerância curta para reduzir respawns desnecessários;
- materialização física usa culling por proximidade: spawn até 750 m e despawn acima de 1 km, preservando jogadores distantes na sessão sem criar objetos 3D desnecessários.

## Simulador

- reutiliza host existente ou inicia servidor empacotado;
- suporta sala privada;
- herda mapa e compatibilidade da sessão;
- usa a posição real do host como centro;
- bots próximos por padrão em raio de 18 m;
- herda linha, rota, destino e próxima parada;
- \`--verify\` valida movimento e consistência de mapa.

## Ghost / Replay

- gravação read-only da telemetria local a 10 Hz;
- arquivos `.navbrghost` salvos e carregados pelos serviços nativos existentes;
- biblioteca local e importação validada no React;
- analytics reais de duração, distância estimada e velocidades;
- prévia read-only do trajeto usando coordenadas reais dos frames;
- replay Ghost 3D experimental com velocidade configurável e loop;
- spawn/update/despawn continuam no Plugin Bridge, não no JavaScript.

## Observação

A Alpha.14 Test 4 está liberada publicamente. A publicação atual inclui o hotfix `29f30b2` para o travamento do **Online através do Host**. Escrita física no OMSI e Personagem/RP permanecem experimentais, opt-in e fail-safe.


## HUD / Roadmap Studio

- configuração do HUD no React com presets, temas, ancoragem, tamanho, opacidade e módulos;
- alterações reaplicadas ao vivo pelo overlay nativo através de `MultiplayerSettingsStore.SettingsSaved`;
- Mover HUD permanece sobre o overlay nativo;
- Roadmap Studio no React usa diretamente `OmsiRoadmapGeneratorService` e `OmsiRoadmapVectorGeneratorService`, sem duplicar o algoritmo no frontend;
- modo por tiles preserva as imagens `.roadmap.bmp` existentes e cria backup quando necessário;
- modo vetorial gera `whole.roadmap.bmp` diretamente de `global.cfg` + splines dos tiles;
- progresso, dimensões, quantidade de tiles/splines, tamanho e backup são mostrados pela interface.


## Alpha.14 Test 5 — candidata de validação

- Navegação 2D passa a renderizar o roadmap real catalogado do mapa, inclusive `roadmap.bmp` quando aplicável;
- Navegação 3D usa o mesmo roadmap catalogado em vez de depender apenas de `whole.roadmap.bmp`;
- fora da rota, o NavBR calcula um caminho real de retorno pelas splines próximas e mantém a rota original separada;
- RP auto-seleciona o humano que está realmente no estado `DrivingBus`/motorista ativo, com fallback pelo ponteiro de definição vivo;
- ônibus remoto físico sincroniza Kachel validada pelo mapa local, suaviza movimento e confirma materialização por IDs;
- simulador físico usa posição absoluta + local + Kachel do host, pode usar MAN EN92 padrão e rotas diferentes do HOF;
- `--verify-physical` falha se a telemetria chegar, mas os bots não forem confirmados como `RoadVehicles` reais no OMSI.
