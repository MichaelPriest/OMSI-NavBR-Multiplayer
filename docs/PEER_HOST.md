# Salas peer-host

> **Importante:** o peer-host atual conecta os jogadores e troca presença, telemetria, chat e voz, mas **ainda não cria o ônibus do outro jogador dentro do mapa 3D do OMSI**. Os remotos aparecem atualmente no mapa/minimapa/HUD do NavBR. Veja também [Estado real do multiplayer](MULTIPLAYER_STATUS.md).

Na série `0.3.0-alpha`, o modo multiplayer padrão do NavBR usa o **PC de quem cria a sala como servidor da sessão**.

## O que a sala faz hoje

Ao entrar na mesma sala, os clientes podem trocar:

- presença/nickname;
- telemetria;
- posição, heading, velocidade e contexto de mapa;
- chat de texto;
- voz push-to-talk;
- informações usadas para desenhar marcadores remotos no GPS/HUD do NavBR.

Isso **não significa ainda que os outros ônibus sejam adicionados ao tráfego/mundo 3D do OMSI**.

Não existem ainda, dentro do simulador, sincronização física de ônibus remotos, colisões compartilhadas, portas/luzes/matriz de outro jogador nem tráfego AI compartilhado.

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

Depois da conexão, o resultado esperado nas builds atuais é ver o outro jogador no **NavBR**, não como um novo ônibus físico dentro do OMSI.

## Internet

A alpha não possui NAT traversal automático. Para jogadores fora da rede do host, pode ser necessário:

- permitir o NavBR/porta TCP `27730` no Windows Firewall;
- encaminhar a porta TCP `27730` do roteador para o PC do host;
- usar o IP/endereço público alcançável do host.

A intenção futura é tentar UPnP/PCP/NAT-PMP ou outra técnica de conexão assistida para reduzir a configuração manual.

## Chat e voz

Atalhos atuais da série mais recente:

- `F9`: abrir o chat de texto do HUD;
- `F10`: push-to-talk; mantenha pressionado enquanto fala.

Os atalhos são configuráveis e podem ser bloqueados quando entram em conflito com `Inputs/keyboard.cfg` do OMSI.

O microfone só é transmitido enquanto o push-to-talk está ativo e o chat de voz está habilitado.

## Servidor dedicado

Quem preferir pode continuar usando o pacote `OMSI-NavBR-Server-vX.X.X-win-x64.zip`. Ele executa a mesma aplicação de host usada dentro do cliente, mas como processo independente.

O servidor dedicado **não adiciona multiplayer visual 3D por si só**. Ele apenas substitui onde o servidor da sessão é executado.

## Futuro multiplayer visual dentro do OMSI

A alpha.10 possui um plugin experimental separado para investigar essa etapa. Atualmente ele recebe estado remoto para diagnóstico pelo bridge local, mas ainda não cria nem move veículos no OMSI.

A representação física só será tentada depois de validar o plugin/bridge no OMSI real e encontrar um mecanismo seguro de veículo AI/instância equivalente.