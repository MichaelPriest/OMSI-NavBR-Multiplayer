# Alpha.12 — diagnóstico NAT / Internet

A Alpha.12 separa **diagnóstico local**, **classificação do ambiente NAT**, **teste externo de porta** e **fallback por relay**.

## O que o NavBR verifica localmente

- IPv4 ativos deste PC;
- se existe processo escutando em `TCP 27730`;
- regra de entrada do Windows Firewall;
- descoberta do gateway via UPnP;
- estado da preferência de UPnP automático;
- IPv4 WAN informado pelo gateway quando disponível;
- classificação básica do endereço WAN.

## Classificações

### IPv4 público no gateway

O endereço WAN informado pelo roteador não pertence às faixas privadas, CGNAT ou reservadas conhecidas. Isso favorece host direto, mas sozinho não prova que `TCP 27730` esteja acessível pela Internet.

### CGNAT provável

Endereços em `100.64.0.0/10` são tratados como sinal forte de Carrier-Grade NAT. Um mapeamento UPnP no roteador local normalmente não atravessa o NAT da operadora.

### Double NAT / WAN privada provável

Se o gateway informa endereço privado (`10/8`, `172.16/12` ou `192.168/16`) na WAN, o NavBR sinaliza provável double NAT ou outro roteador acima do gateway local.

### WAN reservada / não pública

Faixas de documentação, benchmark, multicast e outros endereços não públicos não são tratadas como IPv4 público utilizável para host direto.

## Teste externo real de TCP 27730

A Alpha.12 já possui suporte a um **probe externo opcional**. O cliente usa o endpoint configurado em `NAVBR_EXTERNAL_PROBE_URL`; no servidor, o recurso só é habilitado quando `NAVBR_ENABLE_EXTERNAL_PORT_PROBE` está ativo.

O serviço:

1. observa o IP de origem da própria solicitação;
2. recusa endereços privados/reservados;
3. tenta conexão somente na porta fixa `TCP 27730`;
4. aplica timeout curto;
5. possui rate limit por IP;
6. não recebe senha de sala nem um IP arbitrário informado pelo cliente.

Quando nenhum serviço de probe está configurado, a interface deve continuar mostrando que o alcance externo **não foi verificado**, em vez de produzir um falso positivo.

## Relay de aplicação

A Alpha.12 agora possui a primeira etapa do fallback para CGNAT/double NAT.

No assistente **Sala → Privacidade → Rede**, o usuário pode ativar **relay de aplicação (experimental)** e informar o endereço de uma instância NavBR Server. Nesse modo:

- o peer-host local não é iniciado;
- TCP 27730 não precisa ser publicada no roteador do criador;
- UPnP é desativado para aquela criação de sala;
- telemetria, chat, voz e demais mensagens da sala usam o mesmo Hub SignalR remoto;
- o criador continua sendo o primeiro participante/autoridade inicial da sessão;
- o formato de convite passa a indicar `mode=relay`.

O modo é **opt-in** e não existe um endpoint público de relay codificado no cliente. O operador precisa fornecer um servidor NavBR remoto válido.

## Quando usar cada opção

- **LAN:** peer-host direto; normalmente sem UPnP.
- **Internet com IPv4 público:** peer-host direto, com firewall/UPnP/port forwarding conforme necessário.
- **CGNAT ou double NAT sem encaminhamento viável:** relay de aplicação, se houver servidor NavBR remoto disponível.
- **Dúvida se a porta realmente está acessível:** probe externo, quando configurado.

## Privacidade

A descoberta UPnP e a classificação NAT são locais. O probe externo usa apenas os dados mínimos necessários para testar o endereço de origem observado. Senhas privadas de sala são efêmeras, não entram no convite e não fazem parte do probe externo.
