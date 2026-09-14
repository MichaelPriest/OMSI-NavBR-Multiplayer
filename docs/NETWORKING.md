# Rede multiplayer

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

## Segurança atual

- o servidor associa telemetria/chat/voz à presença da conexão, em vez de confiar somente no PlayerId enviado pelo cliente;
- tamanhos de chat e frame de voz são limitados;
- esta alpha ainda não oferece criptografia própria, senha de sala ou autenticação de conta;
- em Internet pública, um proxy HTTPS/TLS ou uma futura camada segura será necessária antes de classificar o modo como pronto para produção.
