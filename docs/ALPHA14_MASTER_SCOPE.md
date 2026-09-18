# Alpha.14 — escopo mestre

A **Alpha.14** consolida a evolução visual/física da Alpha.13 e inaugura o **Modo Personagem / RP** como um fluxo experimental próprio, disponível tanto no modo normal quanto no multiplayer.

Versão inicial:

`0.3.0-alpha.14-test.1`

## Test 2

A **Alpha.14 Test 2** congela o primeiro passe de usabilidade do RP:

- Central Multiplayer com abas reais;
- aba própria **Personagem / RP**;
- aba **Jogadores** mostrando No ônibus / RP a pé / RP correndo / RP parado;
- mapa da sessão com marcador distinto para personagem RP;
- botão **Personagem** diretamente no HUD;
- estados do botão: configurar, ativar e voltar ao ônibus;
- interação do botão preservando o overlay click-through;
- portal da Test 2 sem comunicação de cobrança, licença comercial futura, Steam/chaves ou assinatura.

Os commits posteriores ao corte da Test 2 continuam na branch Alpha.14 e entram na próxima Test.

## 1. Personagem / RP

A Alpha.14 Test 1 introduz o primeiro fluxo real de personagem controlável no OMSI:

- entrada **Personagem / RP** em DIRIGIR e ação rápida na Home;
- funciona também sem sala multiplayer;
- após o mapa carregar, o NavBR lê somente personagens reais da lista `Drivers` do mapa;
- seletor liberado apenas com mapa carregado e catálogo real disponível;
- seletor pode abrir automaticamente uma vez por mapa quando o recurso estiver habilitado;
- seleção compartilhada entre modo normal e multiplayer;
- ponteiros nativos da definição do personagem permanecem somente na sessão local;
- backend exige que o personagem escolhido corresponda ao motorista humano real do ônibus do jogador;
- posse/desvinculação do motorista é opt-in, experimental e processada no thread seguro do plugin;
- snapshot do vínculo/IA é restaurado ao voltar ao ônibus;
- controles iniciais: **W/S**, **A/D**, **Shift** e **Esc**;
- deslocamento inicial limitado à área próxima ao ônibus;
- multiplayer usa canal RP separado da telemetria do ônibus, com publicação limitada a 10 Hz.

Ainda não concluído nesta Test 1:

- câmera dedicada seguindo o personagem;
- adaptação ao terreno inclinado;
- animações e gestos RP;
- entrar/sentar/interagir com veículos e objetos;
- personagem físico remoto completo em todos os clientes;
- criação de personagem quando não existir uma instância de motorista compatível.

## 2. Multiplayer físico

Tudo que foi consolidado na Alpha.13 permanece:

- ônibus remoto físico experimental;
- coordenador físico único;
- spawn/update/despawn;
- pose nativa do OMSI 2.3.004;
- manifesto físico atualizado pela telemetria viva;
- luzes e setas básicas;
- nomes dos jogadores em HUD/minimapa/mapa;
- projeção de nome 3D fail-safe sobre ônibus físicos spawnados;
- marcadores uniformes entre host e remotos;
- sessão persistente ao fechar a Central Multiplayer.

## 3. Interface

A Alpha.14 parte do shell Figma já refinado:

- Home responsiva;
- Navegação com mapa dominante e rail responsivo;
- Central Multiplayer responsiva;
- CCO com mapa operacional dominante;
- Configurações com versão real e seleção lateral;
- Hardware com preview técnico recolhível;
- Saúde da Sessão em grade responsiva;
- Empresa/Rede/Perfil refinados;
- estados dinâmicos localizados em pt-BR, inglês, espanhol, alemão e francês.

## 4. Compatibilidade e segurança

- alvo principal: **OMSI 2.3.004**;
- cliente Windows x86;
- plugin Native AOT x86;
- escritas experimentais somente com opt-in explícito;
- caminhos de leitura continuam separados dos caminhos de escrita;
- personagem/ônibus físicos falham fechados: sem capacidade válida, sem escrita;
- restauração de NPC/driver ocorre em saída normal e finalização do plugin quando possível.

## 5. Critério da Test 1

A Test 1 deve validar primeiro:

1. cliente, servidor, plugin e interop compilando;
2. catálogo `Drivers` real aparecendo após carregar mapa;
3. seleção correta do motorista;
4. entrada/saída do modo personagem sem quebrar o ônibus;
5. restauração do vínculo/IA ao retornar;
6. controles básicos em mapa plano;
7. modo normal funcionando sem servidor;
8. sincronização RP básica quando uma sala online estiver ativa;
9. regressões zero no multiplayer físico de ônibus.

Os resultados definem a Alpha.14 Test 2.
