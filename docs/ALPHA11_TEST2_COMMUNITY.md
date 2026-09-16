# Alpha.11 Test 2 — teste público da comunidade

`v0.3.0-alpha.11-test.2` é uma **pré-release experimental**. O objetivo é coletar testes reais antes de considerar os recursos da Alpha.11 estáveis.

## Principais pontos para testar

- HUD/minimapa abrindo durante o gameplay e escondendo em menus/janelas auxiliares do OMSI.
- Velocidade do ônibus no HUD usando `Groundspeed` com fallback para a velocidade linear real.
- HUD mais compacto e transparente.
- Linha, destino, próxima parada e marcadores de parada.
- Multiplayer, peer host TCP 27730, chat e voz.
- Sincronização de telemetria e tráfego.
- **Ônibus remoto físico 3D (EXPERIMENTAL)**.

## Ônibus remoto físico 3D

O recurso fica **desligado por padrão**.

Para testar:

1. Instale/atualize o plugin NavBR pelo próprio aplicativo com o OMSI fechado.
2. Abra a janela Multiplayer e marque **Ônibus remoto 3D (EXPERIMENTAL)**.
3. Na primeira ativação, feche e abra novamente o OMSI para que o processo carregue as flags experimentais.
4. Os dois jogadores devem usar OMSI 2.3.004 e estar no mesmo mapa/build compatível.
5. Cada computador precisa possuir localmente o modelo `.bus`/`.ovh` que o outro jogador está dirigindo, no mesmo caminho relativo em `Vehicles\`.
6. Entre na mesma sala e aproxime os jogadores para observar spawn, movimento, luzes e remoção do ônibus remoto.

O NavBR **não transfere nem redistribui ônibus pagos/proprietários**. O recurso usa apenas arquivos já instalados legalmente em cada computador.

## O que observar no teste 3D

- se o ônibus remoto aparece;
- se aparece no local correto;
- se acompanha o movimento sem saltos excessivos;
- se orientação/rotação estão corretas;
- se velocidade visual acompanha o jogador remoto;
- se faróis, freio e setas são atualizados;
- se o ônibus desaparece quando o jogador sai da sala;
- se ocorre crash, congelamento ou queda brusca de FPS;
- nome do mapa, ônibus usado e distância aproximada entre os jogadores quando ocorreu o problema.

## Limitações conhecidas

- Backend 3D é experimental e pode ser instável.
- A sincronização entre tiles diferentes ainda precisa de validação ampla em mapas reais.
- Não é garantido que todos os ônibus/add-ons se comportem corretamente nesta fase.
- Tráfego IA físico compartilhado completo ainda não deve ser considerado finalizado.
- Até 32 jogadores por sala é a meta inicial; o teste físico 3D deve começar com poucas pessoas antes de aumentar a carga.

Se houver travamento, desative o recurso 3D e reinicie o OMSI. HUD e multiplayer 2D continuam utilizáveis sem o backend físico experimental.
