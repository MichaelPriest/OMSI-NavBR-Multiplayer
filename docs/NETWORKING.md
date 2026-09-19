# Rede multiplayer

## Três modos de multiplayer

### 1. Servidor NavBR oficial

O `NavBR.Server` roda no servidor oficial do projeto no Render. O cliente usa HTTPS/WSS e não precisa abrir TCP 27730, configurar UPnP ou depender de IP público no PC do jogador.

A infraestrutura atual usa Render Free e é explicitamente tratada como **gratuita e limitada** para Alpha/testes. Veja [RENDER_HOSTING.md](RENDER_HOSTING.md).

### 2. LAN

O PC que cria a sala executa o `NavBR.Server` em **TCP 27730** para jogadores na mesma rede local.

### 3. Online através do Host

O PC que cria a sala executa o `NavBR.Server` em **TCP 27730** e recebe jogadores pela Internet. Esse modo é independente do Servidor NavBR oficial e pode exigir Firewall, UPnP ou port forwarding dependendo da rede.

Presença, telemetria, chat, voz e estado operacional usam SignalR nos três modos.

## Firewall Windows

A regra de entrada chama-se **OMSI NavBR Multiplayer - TCP 27730**.

- TCP 27730;
- perfis Privado, Público e Domínio;
- regra baseada em porta;
- UAC;
- verificação depois da criação.

Em **Configurações → Rede**, React chama o mesmo \`WindowsFirewallService\` e mostra se a regra foi realmente encontrada. A tela permite aplicar/corrigir e verifica novamente depois.

Uma regra correta **não prova** alcance pela Internet.

## Listener local

A tela Rede verifica separadamente se existe listener TCP local em 27730. Sem sala local/servidor ativo, é normal o listener aparecer inativo mesmo com Firewall correto.

## UPnP / NAT

\`NatDiagnosticsService\` mostra:

- IPv4 locais;
- listener TCP;
- regra do Firewall;
- gateway UPnP;
- endereço WAN reportado;
- classificação Public WAN / CGNAT / Private WAN / Reserved / Unknown.

UPnP é opcional e só é alterável quando a hospedagem local está parada.

CGNAT/double NAT podem impedir conexões diretas mesmo com Firewall e UPnP corretos.

## Teste externo

O probe externo TCP 27730 é independente. Ele só funciona quando o serviço de callback estiver configurado. Seu resultado não é inferido a partir do Firewall, UPnP ou IP WAN.

Quando o serviço não está configurado, a interface mostra **Teste externo indisponível** em vez de tratar a situação como erro. Sala local/LAN, Firewall e UPnP continuam operando normalmente.

A Central Multiplayer também expõe o estado de alcance do host separadamente:
- host inativo;
- verificando UPnP;
- somente LAN;
- UPnP mapeado sem confirmação externa;
- endereço de Internet disponível via UPnP.

## Servidor remoto / compatibilidade interna

A infraestrutura interna herdada do antigo relay é usada para conectar o cliente a um `NavBR.Server` remoto. Na interface principal, o servidor padrão é apresentado como **Servidor NavBR oficial**. O endpoint oficial atual é `https://omsi-navbr-multiplayer-server.onrender.com`, mantendo suporte a URL personalizada para desenvolvimento/testes.

## Salas privadas

- senha não vai no convite;
- senha não é persistida;
- servidor valida antes da entrada;
- salas privadas não aparecem no navegador público.

## Segurança

- payloads/tamanhos validados;
- servidor associa dados à conexão autenticada;
- escrita física no OMSI permanece local, opt-in e separada da rede.
