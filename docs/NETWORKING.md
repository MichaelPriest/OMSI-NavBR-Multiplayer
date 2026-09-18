# Rede multiplayer

## Peer-host direto

O PC de quem cria a sala pode executar o servidor da própria sessão em **TCP 27730**.

Presença, telemetria, chat, voz, estado operacional e canais experimentais passam pelo SignalR.

## Windows Firewall

A Alpha.14 cria uma regra de entrada chamada **OMSI NavBR Multiplayer - TCP 27730**.

- protocolo TCP;
- porta local 27730;
- perfis Privado, Público e Domínio;
- solicita UAC;
- verifica a regra depois de criar;
- se o UAC for cancelado, o app informa que a regra não foi alterada.

A regra é de porta para cobrir tanto o host embutido do cliente quanto o servidor dedicado.

## UPnP / NAT

UPnP é opcional. Pela Internet, o host direto ainda pode exigir port forwarding e um endereço público alcançável.

CGNAT/double NAT podem impedir conexão direta mesmo com Firewall correto.

## Relay experimental

O relay de aplicação usa um NavBR.Server remoto configurado pelo usuário/operador. Ele não abre host local nem depende de UPnP.

O relay continua experimental e não existe endpoint público embutido no app.

## Salas privadas

- senha não vai no convite;
- senha não é persistida no perfil;
- o servidor valida a senha antes de autorizar a entrada;
- salas privadas não aparecem no navegador público.

## Segurança

- payloads e tamanhos são validados;
- o servidor associa dados à conexão autenticada da sala;
- escrita física no OMSI continua local, opt-in e separada da camada de rede.
