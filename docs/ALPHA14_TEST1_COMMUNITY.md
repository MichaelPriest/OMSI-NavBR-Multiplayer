# Alpha.14 Test 1 — teste comunitário

A **Alpha.14 Test 1** inicia a validação do **Modo Personagem / RP** no OMSI, preservando o multiplayer físico introduzido anteriormente.

## Requisitos

- OMSI 2.3.004;
- NavBR Alpha.14 Test 1;
- plugin NavBR experimental instalado para controle físico do personagem;
- um mapa que exponha personagens reais em `Drivers`;
- um ônibus com motorista humano compatível.

## Teste no modo normal

1. Abra o OMSI e carregue totalmente o mapa/ônibus.
2. Abra o NavBR em **DIRIGIR → Personagem / RP**.
3. Ative **Personagem / RP (EXPERIMENTAL)**.
4. Confirme que o seletor mostra somente personagens reais do mapa.
5. Selecione o personagem correspondente ao motorista atual.
6. Clique em **Sair do ônibus / controlar personagem**.
7. Teste:
   - W / S: frente e trás;
   - A / D: girar;
   - Shift: correr;
   - Esc: voltar ao ônibus.
8. Confirme que, ao voltar, o motorista/IA é restaurado sem corromper o ônibus.

## Teste em multiplayer

1. Repita o teste acima nos dois PCs.
2. Entre na mesma sala.
3. Use mapa compatível.
4. Selecione o personagem local em cada PC.
5. Ative o modo personagem.
6. Observe se o estado RP é transmitido sem interferir na telemetria do ônibus.
7. Saia do modo RP e confirme a limpeza remota.

## Resultado esperado

- sem mapa carregado: seletor indisponível;
- sem personagens `Drivers`: nada é inventado;
- personagem que não corresponde ao motorista ativo: posse recusada;
- sem plugin/capacidade: controle a pé indisponível;
- modo normal funciona sem multiplayer;
- multiplayer apenas adiciona sincronização;
- Esc/Voltar ao ônibus restaura o estado;
- nenhuma escrita ocorre sem opt-in.

## Limitações conhecidas da Test 1

- terreno inclinado ainda não é seguido dinamicamente;
- a câmera dedicada do personagem ainda não está pronta;
- animações/gestos ainda não estão sincronizados;
- personagens remotos físicos ainda não são considerados concluídos;
- interações como sentar, entrar em ônibus e objetos RP ainda não estão implementadas.

Envie junto do relato:
- mapa;
- ônibus;
- personagem selecionado;
- se o modo foi normal ou multiplayer;
- trecho relevante do diagnóstico técnico;
- se o retorno ao ônibus restaurou o motorista corretamente.
