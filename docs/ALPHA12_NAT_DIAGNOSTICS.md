# Alpha.12 — diagnóstico NAT / Internet

A Alpha.12 passa a separar claramente **diagnóstico local**, **classificação do ambiente NAT** e **teste externo real de porta**.

## O que o NavBR já consegue verificar localmente

- IPv4 ativos deste PC;
- se existe processo escutando em `TCP 27730`;
- regra de entrada do Windows Firewall;
- descoberta do gateway via UPnP;
- estado da preferência de UPnP automático;
- IPv4 WAN informado pelo gateway quando disponível;
- classificação básica do endereço WAN.

## Classificações

### IPv4 público no gateway

O endereço WAN informado pelo roteador não pertence às faixas privadas, CGNAT ou reservadas conhecidas. Isso é um bom sinal para host direto, mas **não prova** que `TCP 27730` esteja acessível pela Internet.

### CGNAT provável

Endereços em `100.64.0.0/10` são tratados como sinal forte de Carrier-Grade NAT. Nesse cenário, um mapeamento UPnP no roteador local normalmente não atravessa o NAT da operadora.

### Double NAT / WAN privada provável

Se o gateway informa endereço privado (`10/8`, `172.16/12` ou `192.168/16`) na interface WAN, o NavBR sinaliza provável double NAT ou outro roteador acima do gateway local.

### WAN reservada / não pública

Faixas de documentação, benchmark, multicast e outros endereços não públicos não são tratados como IPv4 público utilizável para host direto.

## Limite importante: teste externo

O próprio PC do host não consegue provar sozinho que uma conexão vinda da Internet alcança sua porta. Para isso é necessário um **servidor externo de callback/probe** que tente abrir uma conexão até o endereço público do host.

Por isso a interface atual mostra explicitamente **“ainda não verificado de fora”** em vez de gerar um falso positivo de “porta aberta”.

A próxima etapa de infraestrutura será definir o serviço opcional de callback/presença global. Esse serviço poderá:

1. receber uma solicitação autenticada/limitada do host;
2. tentar conexão TCP externa à porta publicada;
3. retornar resultado sanitizado;
4. alimentar o navegador global de salas somente quando o host optar por publicidade;
5. nunca expor senha de sala ou outros segredos.

## Fallback futuro

Para usuários atrás de CGNAT/double NAT sem possibilidade de encaminhamento, a Alpha.12 mantém como objetivo um fallback/relay opcional. O peer-host direto continuará sendo o caminho preferido quando a rede permitir.

## Privacidade

O diagnóstico atual não envia o endereço do usuário para um serviço público do NavBR. A descoberta UPnP é local. Um futuro teste externo deverá ser opt-in e documentar claramente quais dados mínimos são enviados ao serviço de callback.
