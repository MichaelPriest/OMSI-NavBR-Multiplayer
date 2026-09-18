# Hospedagem gratuita do NavBR.Server no Render

O OMSI NavBR Multiplayer pode usar um **Web Service do Render** como servidor online central para salas.

## O que muda para o jogador

No modo **Servidor Online**:

- o jogador não precisa abrir TCP 27730 no roteador;
- não depende de UPnP;
- CGNAT do jogador não impede a conexão;
- todos entram pela mesma URL HTTPS do Render;
- presença, telemetria, chat, voz e estado da sala continuam usando o SignalR existente.

O modo peer-host local continua disponível como alternativa.

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

## Render Free

O plano gratuito é adequado para testes comunitários, mas tem limitações:

- o serviço pode hibernar após período sem tráfego;
- a primeira conexão após hibernação pode levar mais tempo;
- o filesystem é efêmero;
- um novo deploy/restart derruba conexões WebSocket existentes.

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
