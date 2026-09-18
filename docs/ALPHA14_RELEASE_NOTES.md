# Alpha.14 — notas da versão pública

Versão: **v0.3.0-alpha.14**

A Alpha.14 consolida as builds Test 1–3.x e passa a ser a prerelease pública principal do OMSI NavBR Multiplayer.

## Interface principal

- **React + TypeScript + Vite em WebView2 como shell principal**;
- host .NET/WPF x86 preservado para todos os serviços nativos;
- shell WPF anterior mantido como fallback técnico;
- WPF só é ocultado depois que o WebView2 confirma carregamento;
- o ícone da bandeja abre/oculta a interface React;
- Home com Executar OMSI e dados reais da operação;
- Navegação/GPS, Central Multiplayer, CCO, Empresa/Frota, Perfil, Personagem/RP, Ghost/Replay, Hardware Cockpit, Instalações OMSI, HUD, Roadmap Studio, Diagnóstico e Rede migrados para React;
- Mapa 3D e a interação de mover o HUD continuam nativos; renderização do HUD, geração de roadmap e runtime físico do RP continuam no C#.

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
- peer-host TCP 27730 e servidor dedicado opcional.

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

- peer-host padrão TCP 27730;
- regra Windows Firewall para todos os perfis de rede;
- elevação UAC e verificação posterior;
- aba Rede separa Firewall, listener local, UPnP, NAT/CGNAT e teste externo;
- UPnP automático não pode ser alterado durante hospedagem;
- teste externo só é executado quando o serviço de callback estiver configurado.

## Plugin / RP

- Named Pipe \`OMSI.NavBR.Multiplayer.Plugin.v3\`;
- ABI/state v3;
- capabilities atualizadas em runtime;
- personagem real do mapa, selecionado pela interface React;
- HUD e auto-prompt direcionam para a tela RP React;
- restauração de pose/vínculo/IA;
- ônibus remoto físico continua experimental.

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

A Alpha.14 continua prerelease. Escrita física no OMSI e Personagem/RP permanecem experimentais, opt-in e fail-safe.


## HUD / Roadmap Studio

- configuração do HUD no React com presets, temas, ancoragem, tamanho, opacidade e módulos;
- alterações reaplicadas ao vivo pelo overlay nativo através de `MultiplayerSettingsStore.SettingsSaved`;
- Mover HUD permanece sobre o overlay nativo;
- Roadmap Studio no React usa os geradores C# existentes;
- modo por tiles preserva as imagens `.roadmap.bmp` existentes e cria backup quando necessário;
- modo vetorial gera `whole.roadmap.bmp` diretamente de `global.cfg` + splines dos tiles;
- progresso, dimensões, quantidade de tiles/splines, tamanho e backup são mostrados pela interface.
