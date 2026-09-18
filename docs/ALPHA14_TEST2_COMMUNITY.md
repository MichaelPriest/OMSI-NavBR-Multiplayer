# Alpha.14 Test 2 — teste comunitário

A Test 2 concentra o teste em **Central Multiplayer com abas** e **Personagem/RP pelo HUD**.

## 1. Central Multiplayer

Abra a Central e confirme que existem as abas:

- Visão geral;
- Sala;
- Jogadores;
- Chat & Voz;
- Personagem / RP;
- Avançado.

Verifique que trocar de aba não desconecta a sala e não perde telemetria/chat.

## 2. Personagem pelo HUD

1. carregue completamente mapa e ônibus;
2. ative Personagem/RP;
3. selecione o motorista real;
4. volte ao OMSI;
5. confirme o botão no HUD;
6. clique em **Ativar personagem**;
7. teste W/S, A/D e Shift;
8. use **Voltar ao ônibus** ou Esc.

Se ainda faltar mapa, seleção ou plugin, o botão do HUD deve abrir a configuração em vez de tentar uma posse inválida.

## 3. Estado multiplayer

Com dois PCs:

- jogador no ônibus deve aparecer como **No ônibus**;
- jogador em RP deve aparecer como a pé/correndo/parado;
- o mapa da sessão deve trocar o marcador sem inventar posições;
- sair do RP deve limpar o estado remoto;
- fechar a Central não deve encerrar a sessão.

## 4. Ônibus físico

Repita o teste básico da Test 1:

- A vê B;
- B vê A;
- movimento;
- luzes/setas;
- despawn;
- reconexão;
- sem duplicação.

## 5. Informe no relato

- mapa;
- ônibus;
- personagem;
- se o botão do HUD apareceu;
- se foi possível clicar sem bloquear o OMSI;
- se o motorista voltou ao ônibus;
- se as abas permaneceram funcionais;
- se foi modo normal ou multiplayer;
- diagnóstico relevante, se houver.

## Limitações conhecidas

Ainda não considerar concluídos:

- câmera dedicada do personagem;
- correção de altura em terreno inclinado;
- gestos/animações;
- personagem remoto físico completo;
- portas/matriz/articulação do ônibus remoto.
