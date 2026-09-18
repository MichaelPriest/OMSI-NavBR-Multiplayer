# Rede multiplayer

## Servidor online

O mesmo `NavBR.Server` pode ser hospedado publicamente, inclusive em um Web Service gratuito do Render. Nesse modo o cliente usa HTTPS/WSS e não precisa abrir TCP 27730, configurar UPnP ou depender de IP público no PC do jogador.

A interface chama esse transporte de **Servidor Online**. O estado interno ainda reutiliza a infraestrutura de servidor remoto/relay da Alpha.14 para manter compatibilidade com o protocolo atual.

Veja [RENDER_HOSTING.md](RENDER_HOSTING.md).

## Peer-host

O PC que cria a sala pode hospedar a própria sessão em **TCP 27730**. Presença, telemetria, chat, voz e estado operacional passam pelo SignalR.

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

## Servidor remoto / compatibilidade de relay

A infraestrutura antes apresentada como relay é usada para conectar o cliente a um `NavBR.Server` remoto. Na interface principal ela aparece como **Servidor Online**. O endpoint público não é codificado no cliente: a URL gerada pelo provedor deve ser configurada na tela da sala.

## Salas privadas

- senha não vai no convite;
- senha não é persistida;
- servidor valida antes da entrada;
- salas privadas não aparecem no navegador público.

## Segurança

- payloads/tamanhos validados;
- servidor associa dados à conexão autenticada;
- escrita física no OMSI permanece local, opt-in e separada da rede.
