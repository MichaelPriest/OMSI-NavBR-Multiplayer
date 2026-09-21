# HUD Imersivo / Operação

O modo **Imersivo / Operação** é um preset opcional do HUD do OMSI NavBR Multiplayer.
Ele não substitui o HUD clássico: o usuário pode alternar entre os dois nas
Configurações > HUD.

## Objetivo

A proposta visual é aproximar o HUD de interfaces de operação usadas em
simuladores modernos, mantendo a cabine visível e distribuindo as informações
principais nas bordas da tela.

## Dados exibidos

O modo imersivo usa somente estado real que já existe no NavBR:

- linha, rota, destino e próxima parada;
- velocidade, atraso e combustível;
- mapa/roadmap real, rota e marcadores;
- rua/mapa atual;
- jogadores remotos no mesmo mapa;
- distância, velocidade e latência dos jogadores quando disponíveis;
- estado de voz/PTT;
- mensagens recentes do chat;
- portas, parada solicitada, freio de estacionamento, ré, setas, luzes e limpador.

Não são criados passageiros, temperatura, horário OMSI ou qualquer outra
telemetria que o runtime ainda não forneça.

## Layout

- **Barra superior:** operação, navegação e telemetria essencial.
- **Inferior esquerdo:** minimapa real do NavBR.
- **Inferior direito:** multiplayer, jogadores próximos, chat e voz/PTT.
- **Faixa de estado:** eventos reais do ônibus.

## Configuração

O preset usa o mesmo sistema modular já existente. Por isso respeita:

- ativação/desativação do HUD;
- opacidade;
- módulo de minimapa;
- módulo multiplayer;
- indicadores;
- escala do minimapa;
- escala do painel multiplayer.

O seletor rápido **HUD atual / HUD imersivo** preserva o preset clássico usado
antes da troca durante a sessão.

## Segurança de implementação

O C# continua sendo a autoridade. O modo imersivo é somente uma camada de
apresentação sobre telemetria, navegação, voz e multiplayer já existentes.
Nenhum mock ou estado sintético é usado em produção.

## Estado

Implementação em desenvolvimento no PR #36, branch
`feature/immersive-operation-hud`.
