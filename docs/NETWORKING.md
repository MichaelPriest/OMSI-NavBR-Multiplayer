# Rede multiplayer

> **Escopo atual:** esta camada sincroniza dados entre clientes NavBR. Ela ainda não transforma automaticamente a sessão em um mundo 3D compartilhado completo do OMSI. Para o estado detalhado, veja [Estado real do multiplayer](MULTIPLAYER_STATUS.md).

## Modos de transporte

A Alpha.12 mantém **peer-host direto** como padrão e adiciona um **relay de aplicação experimental e opt-in** para redes em que o host direto não é viável.

### Peer-host direto — padrão

O PC de quem cria a sala executa o servidor ASP.NET Core + SignalR da própria sessão em `TCP 27730`.

```text
Convidado A ─┐
             ├── TCP 27730 / SignalR ── PC do criador
Convidado B ─┘                              ├─ presença
                                            ├─ telemetria
                                            ├─ chat
                                            ├─ voz Opus
                                            └─ tráfego compartilhado experimental
```

Na LAN, o NavBR exibe os endereços IPv4 utilizáveis. Pela Internet, o host pode precisar de regra de firewall, UPnP/port forwarding e um IPv4 público alcançável.

### Relay de aplicação — experimental

No passo **Sala → Privacidade → Rede**, o usuário pode optar por um servidor NavBR remoto como relay. Nesse modo o cliente **não abre o host local nem tenta mapear TCP 27730 via UPnP**. Todos os participantes se conectam ao mesmo Hub SignalR remoto e continuam usando o mesmo protocolo de sala.

```text
Criador ──────┐
              ├── HTTPS/HTTP + SignalR ── servidor NavBR configurado
Convidado A ──┤                              ├─ sala
Convidado B ──┘                              ├─ telemetria
                                             ├─ chat
                                             ├─ voz
                                             └─ autoridade da sessão
```

O criador entra primeiro e continua sendo a autoridade inicial da sessão/tráfego. O relay é transporte; ele não cria um segundo protocolo multiplayer.

O NavBR **não fornece nesta etapa um endereço público de relay embutido**. O endereço precisa apontar para uma instância NavBR Server configurada pelo operador/usuário. Não invente ou assuma um endpoint público.

## Convites

O formato `NAVBR_INVITE_V1` agora diferencia:

- `mode=peer-host` — endereço do host direto;
- `mode=relay` — endereço do servidor relay.

Senha de sala não é colocada no convite. Salas privadas continuam exigindo a senha informada separadamente.

## NAT, UPnP e CGNAT

O NavBR possui diagnóstico local de NAT, firewall e UPnP. O host direto continua preferencial quando a rede permite.

CGNAT (`100.64.0.0/10`) e double NAT podem impedir o encaminhamento tradicional. Nesses casos o relay de aplicação pode ser usado manualmente, desde que exista um servidor NavBR remoto alcançável.

O probe externo opcional é separado do relay. Quando configurado, um servidor NavBR pode testar o alcance de `TCP 27730` usando somente o IP de origem observado e controles de rate limit; ele não recebe uma senha de sala para executar o teste.

## Segurança e privacidade

- o servidor associa telemetria/chat/voz à presença da conexão, em vez de confiar apenas no `PlayerId` enviado pelo cliente;
- campos e tamanhos de payload são validados antes da retransmissão;
- salas privadas usam senha efêmera; a senha não é persistida no perfil nem incluída no convite;
- relay é opt-in e o endereço fica sob controle do usuário/operador;
- para uso pela Internet, prefira um servidor relay/dedicado atrás de HTTPS/TLS;
- diagnósticos de UPnP são locais; o probe externo só ocorre quando a infraestrutura correspondente é configurada.

## O que o transporte não resolve sozinho

Conseguir conectar dois jogadores não significa que o OMSI já possua um mundo compartilhado completo. A representação física de ônibus remotos, sincronização integral de passageiros, colisões e demais estados do cenário continuam sendo camadas separadas e experimentais.
