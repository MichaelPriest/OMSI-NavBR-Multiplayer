# OMSI NavBR Multiplayer v0.3.0-alpha.24

## Português (Brasil)

A Alpha.24 corrige a regressão de interface da Alpha.23 e recoloca na WinUI 3 os modos multiplayer que continuavam disponíveis no backend.

### Modos de conexão e criação de sala

A página Multiplayer volta a expor claramente:

1. **Servidor NavBR oficial** — usa o servidor online padrão, sem abrir TCP 27730 no PC do jogador e compatível com redes atrás de CGNAT.
2. **LAN** — este PC executa o host local para jogadores na mesma rede.
3. **Online através do Host** — este PC executa o host e tenta disponibilizar TCP 27730 pela Internet usando UPnP quando possível.
4. **Relay personalizado** — permite informar outro servidor/relay quando necessário.
5. **Entrar em servidor/sala existente** — conexão direta a uma sala já criada.

### Salas e convites

- navegador de salas públicas voltou para a WinUI;
- favoritos de salas públicas;
- copiar convite NavBR;
- colar convite NavBR compatível com o formato `NAVBR_INVITE_V1`;
- sala pública/privada e senha;
- botão explícito para parar hospedagem;
- endereços LAN/Internet exibidos no app;
- servidor oficial, servidor atual e relay personalizado são mantidos separadamente para impedir que `localhost` seja reutilizado por engano ao trocar de modo.

### Rede / NAT

A WinUI agora também expõe:

- UPnP automático;
- diagnóstico de rede/NAT;
- regra de Firewall TCP 27730;
- teste externo opcional da porta;
- estado conhecido de alcance do host.

O teste externo continua opcional. Serviço de probe não configurado não significa que a sala LAN/host falhou.

### Chat e voz

Chat e voz também foram migrados para a página Multiplayer nativa:

- chat da sala;
- ativar/desativar voz;
- canais Geral, Empresa/Equipe, CCO/Dispatcher e Proximidade;
- raio de proximidade de 20 a 1000 m;
- microfone e saída de áudio;
- deafen;
- qualidade/jitter/perda/buffer;
- mute e ganho por jogador remoto.

O protocolo e o backend de voz/chat existentes foram preservados.

### Multiplayer físico e Performance Bridge

As otimizações da Alpha.23 continuam:

- RuntimeHost x86 + WinUI 3 x64;
- plugin Native AOT x86;
- ônibus físicos via plugin real do OMSI;
- governador adaptativo;
- snapshots escopados;
- telemetria com caches e menos chamadas de memória;
- tráfego AI filtrado por distância;
- métricas de tempo da leitura x86.

### Gates obrigatórios

A Alpha.24 só pode ser publicada se o mesmo SHA tiver `build.yml` concluído com sucesso, incluindo:

- Native AOT x86;
- exports e smoke do plugin;
- Build client;
- WinUI 3 x64;
- XAML startup smoke;
- WinUI x64 → RuntimeHost x86 IPC smoke;
- smoke multiplayer local;
- instalador x64;
- instalação/desinstalação silenciosa.

O smoke IPC da página Multiplayer agora exige explicitamente:

- jogadores completos;
- chat;
- dispositivos e mixers de voz;
- rede/NAT;
- diretório de salas públicas.

### Ainda experimental

Esta continua sendo uma prerelease de teste. A prioridade de validação real permanece:

- dois PCs / dois OMSI;
- Servidor NavBR oficial;
- LAN;
- Online através do Host;
- troca de Kachel e movimento físico contínuo;
- reconexão/despawn;
- voz e chat entre jogadores reais;
- impacto em FPS e microtravadas.

Ônibus articulados continuam fora do conjunto físico considerado validado.

## English

Alpha.24 restores the multiplayer connection modes that remained available in the backend but were hidden by the simplified Alpha.23 WinUI migration.

The native Multiplayer page now exposes the official NavBR server, LAN hosting, Internet peer-host with UPnP, custom relay, public rooms/favorites, invite copy/paste, NAT/firewall tools, room chat, and voice configuration including channels, devices, proximity, deafen, remote mute and gain.

The release remains gated on a successful build of the exact same commit, including WinUI x64, RuntimeHost x86 IPC, Native AOT x86 plugin and installer smokes.
