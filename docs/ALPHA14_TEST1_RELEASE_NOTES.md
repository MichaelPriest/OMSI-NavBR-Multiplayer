# Alpha.14 Test 1 — release notes

## Novo marco: Personagem / RP

A **Alpha.14 Test 1** introduz o primeiro sistema experimental de personagem do NavBR.

O recurso funciona também no **modo normal**, sem exigir sala multiplayer. Quando o mapa termina de carregar, o NavBR pode ler os personagens reais da lista `Drivers` do OMSI, permitir a seleção do motorista e preparar o controle a pé do motorista humano do próprio ônibus.

## Incluído

- menu **DIRIGIR → Personagem / RP**;
- ação rápida de Personagem/RP na Home;
- leitura do catálogo real `Drivers` do mapa;
- seletor de personagem após o mapa carregar;
- seleção compartilhada entre modo normal e multiplayer;
- vínculo ao motorista humano real do ônibus do jogador;
- posse/desvinculação experimental do motorista;
- restauração do vínculo e IA ao voltar ao ônibus;
- controles iniciais W/S, A/D, Shift e Esc;
- canal multiplayer próprio para estado RP;
- publicação RP limitada a 10 Hz;
- opt-in independente do teste de ônibus físico;
- validação nativa fail-closed para humanos/driver.

## Multiplayer físico preservado

A Alpha.14 mantém as melhorias da Alpha.13:

- ônibus remoto físico experimental;
- coordenador físico único;
- manifesto vivo por telemetria;
- marcadores uniformes e nomes dos jogadores;
- nome 3D fail-safe sobre ônibus remotos spawnados;
- luzes e setas básicas;
- limpeza em saída, reconexão e encerramento.

## Interface

Também estão incluídos os passes recentes de interface:

- Home responsiva;
- Navegação e Central Multiplayer refinadas;
- CCO com mapa dominante;
- Configurações, Hardware e Saúde da Sessão refinados;
- Empresa, Rede e Perfil refinados;
- estados dinâmicos nos 5 idiomas.

## Limitações do Personagem/RP nesta Test 1

Ainda não são considerados concluídos:

- câmera dedicada seguindo o personagem;
- terreno inclinado;
- animações e gestos;
- interações com objetos;
- entrar/sentar em veículos;
- personagem remoto físico completo;
- criação de personagem quando não houver motorista compatível.

## Teste recomendado

Primeiro teste em modo normal:

1. carregue mapa e ônibus;
2. abra **Personagem / RP**;
3. selecione o motorista;
4. saia do ônibus;
5. teste W/S, A/D, Shift e Esc;
6. confirme que o retorno restaura o motorista.

Depois repita em dois PCs na mesma sala para validar a sincronização RP.

Consulte `ALPHA14_TEST1_COMMUNITY.md` para o checklist detalhado.
