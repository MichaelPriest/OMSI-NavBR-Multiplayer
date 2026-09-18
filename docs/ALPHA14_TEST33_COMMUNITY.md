# Alpha.14 Test 3.3 — roteiro de teste

## Selects

1. Abra o select de idioma na sidebar.
2. Abra os selects de Chat/Voz e Hardware.
3. Confirme que o campo fechado e a lista aberta permanecem escuros.
4. Passe o mouse e selecione itens; nenhum item deve ficar com fundo branco.

## Mover HUD

1. No topo do app, clique **Mover HUD**.
2. Confirme que o HUD aparece e entra em modo de edição.
3. Mova/redimensione o HUD.
4. Clique novamente para sair do modo de edição.
5. Confirme também os atalhos em **Sistema** e **Home > Ações rápidas**.

## Executar OMSI

1. Na Home, clique **Executar OMSI**.
2. Se existir instalação preferida, confirme que o NavBR inicia o `Omsi.exe` dessa instalação.
3. Se o OMSI já estiver aberto, confirme que não abre uma segunda cópia.
4. Sem instalação cadastrada, confirme que abre **Instalações OMSI**.

## Firewall

1. Abra **Central Multiplayer > Avançado** ou **Conectividade Multiplayer**.
2. Clique para permitir TCP 27730.
3. Aceite o UAC.
4. Confirme que o app mostra a regra como pronta.
5. No Windows Firewall, confirme a regra **OMSI NavBR Multiplayer - TCP 27730**.
6. A regra deve permitir TCP 27730 em todos os perfis de rede.
7. Se cancelar o UAC, o app deve informar que a operação foi cancelada.
