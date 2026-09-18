# Rede multiplayer

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

## Relay experimental

O relay usa NavBR.Server remoto configurado. Não existe endpoint público embutido e o recurso permanece experimental.

## Salas privadas

- senha não vai no convite;
- senha não é persistida;
- servidor valida antes da entrada;
- salas privadas não aparecem no navegador público.

## Segurança

- payloads/tamanhos validados;
- servidor associa dados à conexão autenticada;
- escrita física no OMSI permanece local, opt-in e separada da rede.
