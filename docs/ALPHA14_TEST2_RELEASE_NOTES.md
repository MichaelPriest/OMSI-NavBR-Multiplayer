# Alpha.14 Test 2 — release notes

A **Alpha.14 Test 2** atualiza o primeiro ciclo de Personagem/RP e corrige a experiência da Central Multiplayer.

## Destaques

- Central Multiplayer voltou a usar **abas reais**, em vez de uma tela genérica única:
  - Visão geral;
  - Sala;
  - Jogadores;
  - Chat & Voz;
  - Personagem / RP;
  - Avançado.
- Personagem/RP ganhou aba própria.
- Ônibus físico e diagnóstico permanecem separados em Avançado.
- botão **Personagem** adicionado diretamente ao HUD;
- o botão do HUD muda conforme o estado:
  - **Personagem** quando falta configurar/selecionar;
  - **Ativar personagem** quando o RP está pronto;
  - **Voltar ao ônibus** quando o personagem está ativo;
- botão do HUD preserva o comportamento click-through do overlay e só libera interação na área da ação;
- lista da sessão diferencia jogador **No ônibus**, **RP a pé**, **RP correndo** e **RP parado**;
- mapa da sessão diferencia ônibus e personagem RP sem inventar posição;
- seletor continua usando somente personagens reais da lista `Drivers` do mapa;
- modo normal e multiplayer continuam compartilhando o mesmo personagem selecionado.

## Personagem / RP

Permanece experimental e opt-in.

Fluxo esperado:

1. carregar mapa e ônibus no OMSI;
2. selecionar um personagem real;
3. usar o botão do HUD ou a janela Personagem/RP;
4. controlar o motorista com W/S, A/D e Shift;
5. usar Esc ou **Voltar ao ônibus** para restaurar o motorista.

## Multiplayer físico

A Test 2 preserva o teste de ônibus remoto físico da Test 1:

- spawn/update/despawn;
- posição e rotação nativas;
- velocidade;
- luzes e setas já suportadas;
- nome dos jogadores;
- projeção 3D fail-safe do nome sobre ônibus remotos;
- manifesto físico atualizado com a telemetria atual.

## Ainda em desenvolvimento

- câmera dedicada acompanhando o personagem;
- terreno inclinado;
- animações/gestos;
- personagem remoto físico completo;
- interação com ônibus/objetos;
- portas, matriz e articulação no multiplayer físico.

## Validação automática

A Alpha.14 Test 2 exige build bem-sucedido de:

- Shared;
- servidor;
- interop OMSI nativo x86;
- plugin Native AOT x86;
- cliente WPF x86;
- smoke test da ponte;
- pacote standalone e ZIPs.

Consulte `ALPHA14_TEST2_COMMUNITY.md` para o roteiro de teste.
