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
- Navegação/GPS, Central Multiplayer, CCO, Empresa/Frota, Perfil, Hardware Cockpit, Instalações OMSI, Diagnóstico e Rede migrados para React;
- Mapa 3D, HUD e RP continuam chamando os componentes nativos.

## Navegação

- geometria real da rota OMSI;
- paradas ordenadas quando resolvidas com segurança;
- próxima parada, progresso, distância restante e manobra;
- detecção de desvio de rota;
- ETA adaptativa somente após amostras confiáveis;
- modos Seguir ônibus e Rota completa;
- atalho para o Mapa 3D nativo.

## Central Multiplayer

- Criar/Entrar em sala sem wizard sobreposto;
- salas públicas e privadas;
- senha efêmera;
- diretório, busca e favoritos;
- compatibilidade de mapa/plugin/HOF antes da entrada direta;
- jogadores, latência, mapa real da sessão, chat e voz;
- Personagem/RP;
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
- personagem real do mapa;
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

## Observação

A Alpha.14 continua prerelease. Escrita física no OMSI e Personagem/RP permanecem experimentais, opt-in e fail-safe.
