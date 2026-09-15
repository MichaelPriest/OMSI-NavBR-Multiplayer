# Rede multiplayer

> **Escopo atual:** esta camada de rede sincroniza dados entre clientes NavBR. Ela **ainda não cria nem movimenta o ônibus do outro jogador dentro do mundo 3D do OMSI**. Para o estado completo, veja [Estado real do multiplayer](MULTIPLAYER_STATUS.md).

## Modelo atual

O NavBR usa peer-host no sentido de que o **PC do criador da sala executa o servidor da sessão**. Os demais clientes não se conectam uns aos outros diretamente; todos se conectam ao host da sala.

```text
Convidado A ─┐
             ├── TCP 27730 / SignalR ── PC do criador da sala
Convidado B ─┘                              ├─ presença
                                            ├─ telemetria
                                            ├─ chat
                                            └─ voz Opus
```

A telemetria recebida pode alimentar marcadores no GPS/HUD do NavBR e, na alpha.10 experimental, o bridge local do plugin. **Receber essa telemetria não significa que uma entidade física tenha sido criada no OMSI.**

## Porta

Padrão inicial: `27730/TCP`.

## LAN

Na mesma rede local, use um dos endereços IPv4 exibidos pelo NavBR, por exemplo:

```text
http://192.168.1.50:27730
```

## Internet

Nesta alpha ainda não existe um serviço de rendezvous/NAT traversal. O host precisa estar alcançável. Dependendo da rede isso pode exigir:

- regra de entrada no Windows Firewall;
- port forwarding TCP 27730 no roteador;
- IP público ou hostname alcançável.

CGNAT pode impedir port forwarding tradicional. UPnP/PCP/NAT-PMP, relay ou WebRTC/ICE são candidatos para fases futuras.

## O que não é compartilhado pelo servidor atual

O host da sala não transforma o OMSI em um simulador de mundo compartilhado completo. Hoje ele não sincroniza:

- criação física de ônibus remotos no OMSI;
- tráfego AI;
- passageiros;
- semáforos;
- colisões;
- estado global do cenário.

Portas, luzes, setas, matriz e outros estados de um futuro ônibus remoto só serão tratados depois que existir uma representação física segura dentro do simulador.

## Segurança atual

- o servidor associa telemetria/chat/voz à presença da conexão, em vez de confiar somente no PlayerId enviado pelo cliente;
- tamanhos de chat e frame de voz são limitados;
- campos de telemetria são validados/normalizados antes da retransmissão;
- esta alpha ainda não oferece criptografia própria, senha de sala ou autenticação de conta;
- em Internet pública, um proxy HTTPS/TLS ou uma futura camada segura será necessária antes de classificar o modo como pronto para produção.
