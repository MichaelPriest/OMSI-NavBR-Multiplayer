# Hospedagem gratuita do NavBR.Server no Render

O OMSI NavBR Multiplayer pode usar um **Web Service do Render** como servidor online central para salas.

## O que muda para o jogador

O servidor online padrão do app é:

`https://omsi-navbr-multiplayer-server.onrender.com`

No modo **Servidor Online**:

- o jogador não precisa abrir TCP 27730 no roteador;
- não depende de UPnP;
- CGNAT do jogador não impede a conexão;
- todos entram pela mesma URL HTTPS do Render;
- presença, telemetria, chat, voz e estado da sala continuam usando o SignalR existente.

Há três formas de hospedar uma sala:

- **Host online sem portas (recomendado para Internet):** o PC que cria a sala continua sendo o dono e a autoridade da sessão, mas não aceita conexões de entrada. Host e convidados conectam ao Render por HTTPS/WebSocket. Funciona sem UPnP, sem redirecionamento da TCP 27730 e também atrás de CGNAT;
- **Somente LAN:** o PC que criou a sala executa o NavBR.Server local e outros PCs da mesma rede entram pelo IP local;
- **Internet via UPnP:** o mesmo PC executa o servidor local e o NavBR tenta mapear automaticamente a TCP 27730 no roteador.

No modo **Host online sem portas**, o Render é apenas o transporte intermediário/servidor de sessão. A propriedade da sala continua sendo atribuída ao jogador que a criou primeiro; se ele sair, a autoridade pode ser transferida para outro jogador conectado.

O servidor Render não é necessário quando uma das modalidades peer-host local é usada.

## Deploy com um clique

Use o botão **Deploy to Render** no README do repositório.

O Blueprint `render.yaml` cria:

- Web Service;
- runtime Docker;
- plano `free`;
- região `virginia`;
- health check em `/health`;
- uma instância;
- deploy automático desligado para evitar reiniciar salas a cada commit da `main`.

## Depois do deploy

1. Abra o serviço no painel do Render.
2. Copie a URL pública `https://<servico>.onrender.com`.
3. No NavBR, abra **Multiplayer → Sala**.
4. Marque **Usar servidor online**.
5. Cole a URL em **URL do servidor online**.
6. Informe sala/apelido/senha, se houver.
7. Clique em **Criar sala online**.

Os outros jogadores usam a mesma URL e o mesmo ID da sala.

## Render Free — limites atuais (revisado em 18/09/2026)

O plano gratuito continua adequado para Alpha e testes comunitários, não para uma infraestrutura multiplayer de produção.

Segundo a documentação atual do Render:

- a instância Free de Web Service fornece **0,1 CPU e 512 MB de RAM**;
- há **750 horas de instância Free por workspace por mês**;
- o serviço entra em spin-down depois de **15 minutos sem tráfego de entrada**;
- desde fevereiro de 2026, **mensagens WebSocket recebidas também contam como atividade** e evitam o spin-down enquanto a sessão está realmente trocando dados;
- o retorno de um serviço adormecido pode levar aproximadamente **1 minuto**;
- o filesystem é efêmero;
- Free fica limitado a **uma única instância**, sem horizontal scaling;
- o plano Hobby atual inclui **5 GB/mês de outbound bandwidth**;
- respostas WebSocket enviadas aos jogadores contam como outbound bandwidth;
- o plano Hobby inclui **500 minutos/mês de build pipeline**.

Fontes oficiais:

- https://render.com/docs/free
- https://render.com/docs/blueprint-spec
- https://render.com/docs/outbound-bandwidth
- https://render.com/docs/websocket
- https://render.com/docs/new-workspace-plans

### Quantas salas e jogadores?

O Render **não define um número de “salas NavBR” ou “jogadores NavBR”**. Esses são objetos da aplicação.

Na implementação atual do NavBR.Server:

- não existe hard cap de salas;
- não existe hard cap de jogadores por sala;
- cada jogador conectado mantém uma conexão SignalR/WebSocket;
- salas e presença ficam em memória;
- `/health` agora informa `activeRooms` e `activeConnections` para acompanhar carga real.

Portanto, o limite prático no Free será atingido por **CPU, 512 MB de RAM e principalmente pelos 5 GB de saída mensal**, especialmente quando voz, telemetria e várias salas estiverem ativas ao mesmo tempo.

Não foi colocado um limite artificial nesta etapa. Antes de fixar um valor de jogadores/salas, a recomendação do projeto é executar testes de carga e observar os contadores de `/health`, memória, CPU, latência e consumo de outbound bandwidth.

O NavBR mantém salas e presença em memória. Isso é intencional nesta fase: quando não há jogadores conectados, não existe estado de sala que precise ser persistido.

## Atualizações

O `render.yaml` usa `autoDeployTrigger: off` para não reiniciar sessões ativas a cada commit.

Para atualizar o servidor:

1. abra o serviço no Render;
2. escolha **Manual Deploy**;
3. faça deploy do commit desejado da `main`;
4. espere `/health` ficar saudável.

## Porta

O Render fornece a variável `PORT`. O `NavBR.Server` lê essa variável e escuta em:

`http://0.0.0.0:$PORT`

Sem `PORT`, o comportamento local anterior é preservado.

## Segurança atual

- limites de payload;
- rate limiting HTTP/SignalR;
- salas privadas com senha derivada por PBKDF2;
- associação de telemetria à conexão do jogador;
- limites de políticas de sala;
- sem transferência de conteúdo pago/proprietário do OMSI.

A hospedagem gratuita é indicada para Alpha/testes, não como infraestrutura de produção de grande escala.
