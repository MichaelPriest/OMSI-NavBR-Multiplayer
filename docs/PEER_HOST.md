# Salas peer-host

Na fase `0.3.0-alpha.2`, o modo multiplayer padrão do NavBR usa o **PC de quem cria a sala como servidor da sessão**.

## Criar uma sala

1. Abra o OMSI e o NavBR.
2. Abra **Multiplayer**.
3. Defina apelido e nome/código da sala.
4. Clique em **Criar sala neste PC**.
5. O NavBR inicia o host local na porta TCP `27730` e conecta o próprio criador.
6. Compartilhe com os convidados um endereço alcançável do seu PC e o nome da sala.

Em LAN, a janela mostra os endereços IPv4 encontrados automaticamente.

## Entrar em uma sala

O convidado informa o endereço do host, por exemplo:

```text
http://192.168.1.50:27730
```

Depois informa o mesmo nome da sala e clica em **Entrar na sala**.

## Internet

A alpha não possui NAT traversal automático. Para jogadores fora da rede do host, pode ser necessário:

- permitir o NavBR/porta TCP `27730` no Windows Firewall;
- encaminhar a porta TCP `27730` do roteador para o PC do host;
- usar o IP/endereço público alcançável do host.

A intenção futura é tentar UPnP/PCP/NAT-PMP ou outra técnica de conexão assistida para reduzir a configuração manual.

## Chat e voz

- `T`: abre o chat de texto do HUD;
- `N`: push-to-talk; mantenha pressionado enquanto fala;
- voz: PCM 48 kHz mono → Opus → SignalR/WebSocket → host → clientes.

O microfone só é transmitido enquanto o push-to-talk está ativo e o chat de voz está habilitado.

## Servidor dedicado

Quem preferir pode continuar usando o pacote `OMSI-NavBR-Server-vX.X.X-win-x64.zip`. Ele executa a mesma aplicação de host usada dentro do cliente, mas como processo independente.
