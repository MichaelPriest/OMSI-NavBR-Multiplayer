# Alpha.11 Test 3 — teste público da comunidade

`v0.3.0-alpha.11-test.3` é uma **pré-release experimental** da Alpha.11.

Esta revisão parte diretamente da Test 2 e corrige o comportamento do menu lateral da janela principal, preservando HUD, GPS, multiplayer, plugin/bridge e ônibus remoto 3D experimental.

## Correção principal da Test 3

- O corpo do menu lateral agora possui **rolagem vertical própria**.
- A barra de rolagem aparece automaticamente quando os itens não cabem na altura disponível.
- O rodapé com idioma e **Minimizar para bandeja** permanece fixo e acessível.
- A rolagem não altera o conteúdo nem a navegação da área principal.

### Validar em resoluções diferentes

Teste principalmente:

1. janela no tamanho padrão;
2. janela reduzida até o limite mínimo permitido;
3. escala do Windows em 100%, 125% e 150%, se disponível;
4. roda do mouse sobre o menu lateral;
5. arraste da barra de rolagem;
6. acesso a todos os botões em **Navegação** e **Ferramentas Alpha.11**;
7. idioma e botão de minimizar permanecendo visíveis no rodapé.

## Demais pontos da Alpha.11

- HUD/minimapa abrindo durante o gameplay e escondendo em menus/janelas auxiliares do OMSI.
- Velocidade do ônibus no HUD usando `Groundspeed` com fallback para a velocidade linear real.
- HUD compacto e transparente.
- Linha, destino, próxima parada e marcadores da rota ativa.
- Multiplayer peer-host TCP 27730, chat e voz.
- Sincronização de telemetria e tráfego.
- Plugin Native AOT x86 + interop nativo para OMSI 2.3.004.
- **Ônibus remoto físico 3D (EXPERIMENTAL)**, desligado por padrão.
- Diagnósticos automáticos opcionais para o teste público.

## Ônibus remoto físico 3D

O recurso continua **desligado por padrão**.

Para testar:

1. Instale/atualize o plugin NavBR pelo próprio aplicativo com o OMSI fechado.
2. Abra Multiplayer e marque **Ônibus remoto 3D (EXPERIMENTAL)**.
3. Os jogadores devem usar OMSI 2.3.004 e o mesmo mapa/build compatível.
4. Cada computador precisa possuir localmente o modelo usado pelo outro jogador no mesmo caminho relativo em `Vehicles\`.
5. Entre na mesma sala e observe spawn, posição, movimento, orientação, luzes e remoção do veículo remoto.

O NavBR não transfere nem redistribui ônibus pagos/proprietários.

## Diagnósticos automáticos

A opção **Enviar diagnósticos automáticos do teste** continua opcional e desligada por padrão. Ela pode registrar versão do NavBR, contexto técnico do OMSI, mapa/veículo, estado do plugin/bridge e erros técnicos necessários para reproduzir falhas.

Ela não envia conteúdo do chat, áudio/voz, senhas, tokens, arquivos pessoais ou nome do usuário do Windows.

## Limitações conhecidas

- O backend 3D permanece experimental.
- Sincronização entre tiles diferentes ainda precisa de validação ampla em mapas reais.
- Nem todos os ônibus/add-ons estão garantidos nesta fase.
- Tráfego IA físico compartilhado completo ainda não deve ser considerado finalizado.
- Comece os testes 3D com poucos jogadores antes de aumentar a carga.

Se houver crash ou congelamento ligado ao 3D, desative o recurso e reinicie o OMSI. HUD e multiplayer 2D continuam utilizáveis sem o backend físico experimental.
