# Alpha.13 — escopo mestre

A **Alpha.13** continua diretamente a base da Alpha.12 e muda a prioridade para tornar o multiplayer físico do OMSI utilizável em testes reais.

Branch principal desta fase:

`feature/alpha13-online-vehicles`

Versão inicial:

`0.3.0-alpha.13-test.1`

## 1. Prioridade da versão

O primeiro marco da Alpha.13 é permitir que dois ou mais jogadores na mesma sessão vejam os ônibus uns dos outros fisicamente dentro do OMSI 2.

### Test 1

- ✅ telemetria online real via SignalR;
- ✅ spawn/update/despawn experimental;
- ✅ posição local nativa e quaternion do OMSI 2.3.004;
- ✅ coordenador físico único no serviço multiplayer;
- ✅ posição, rotação e velocidade;
- 🧪 luzes e setas no backend atual;
- 🔒 escrita física somente com opt-in;
- 🧪 teste real com 2+ PCs ainda obrigatório.
- ✅ jogadores remotos usam o mesmo formato de marcador do host/local no minimapa e mapa principal, com cor azul diferenciada;
- ✅ nome real do usuário aparece acima do marcador remoto;
- 🧪 nome 3D acima do ônibus físico usa câmera View/Projection somente leitura do OMSI 2.3.004 e só aparece quando o spawn físico daquele jogador está confirmado;
- 🔒 projeção 3D é fail-safe: câmera inválida, jogador fora da tela, atrás da câmera ou ônibus não spawnado = etiqueta oculta.
- ✅ Central Multiplayer usa o mesmo formato/tamanho de marcador para host e remotos, diferenciando somente a cor;
- ✅ manifesto físico local é atualizado a partir da telemetria real publicada, acompanhando mapa, ônibus e HOF sem exigir reconexão.

### Modo Personagem / RP experimental

- ✅ disponível também no modo normal/single-player; multiplayer não é requisito;
- ✅ entrada própria **Personagem / RP** em DIRIGIR e ação rápida na Home;
- ✅ após o mapa carregar, o NavBR lê somente a lista real `Drivers` exposta pelo mapa do OMSI;
- ✅ seletor de personagem é liberado apenas com mapa carregado e opções reais disponíveis;
- ✅ quando o RP já estiver habilitado, o seletor pode abrir automaticamente uma vez por mapa até o jogador escolher;
- ✅ a seleção é compartilhada entre modo normal e Central Multiplayer;
- ✅ ponteiro da definição do personagem é usado somente na sessão local e nunca é persistido nem enviado pela rede;
- ✅ backend experimental exige que o personagem escolhido corresponda ao motorista humano real vinculado ao ônibus do jogador;
- ✅ ao ativar o modo a pé, o motorista é destacado temporariamente do ônibus, com snapshot de vínculo/IA para restauração;
- ✅ controles iniciais: **W/S** frente/trás, **A/D** giro, **Shift** corrida e **Esc** retorno ao ônibus;
- ✅ escrita de personagem permanece opt-in e é executada somente no callback/thread seguro do plugin OMSI;
- ✅ em multiplayer, o estado RP usa canal separado da telemetria do ônibus e é publicado no máximo a 10 Hz;
- 🧪 primeira Alpha limita o deslocamento a uma área próxima ao ônibus e mantém a altura atual do motorista;
- 🚧 câmera dedicada acompanhando o personagem, ajuste de terreno inclinado, animações/gestos, entrar/sentar/interagir e criação física de personagens remotos ainda exigem desenvolvimento e validação;
- 🔒 se o personagem selecionado não for o motorista humano ativo do ônibus, o NavBR recusa a posse em vez de escolher outro NPC.

### Depois da validação básica

- 🚧 portas por perfil de compatibilidade do ônibus;
- 🚧 matriz, linha e destino usando HOF/estado real;
- 🚧 articulação;
- 🚧 perfis por modelo/add-on;
- 🚧 animações e estados adicionais;
- 🚧 LOD/culling por distância;
- 🚧 extrapolação curta e correção de jitter;
- 🚧 maior quantidade de jogadores simultâneos.

## 2. Multiplayer e conectividade

Continuam preservados:

- peer-host no PC de quem cria a sala;
- TCP 27730;
- servidor dedicado opcional;
- salas públicas e privadas;
- chat e voz PTT;
- diagnóstico NAT/firewall/UPnP;
- relay/fallback experimental;
- compatibilidade de mapa, veículo, HOF e protocolo quando disponível;
- sessão continua ativa ao fechar a Central Multiplayer.

## 3. Navegação, HUD e operação

Todo o trabalho consolidado da Alpha.12 permanece na Alpha.13:

- shell Figma Alpha.12;
- Home operacional;
- GPS e navegação 2D/3D;
- ETA adaptativa baseada em progresso real;
- HUD configurável;
- perfil e histórico real de viagens;
- empresa, equipe e Company Network;
- CCO/Dispatcher;
- Saúde da Sessão;
- Hardware Cockpit;
- Ghost/Replay experimental.

### Refinamento de interface em andamento

- ✅ Navegação mantém o shell Figma já aprovado;
- ✅ mapa 2D ocupa a área principal e deixa de depender da altura fixa legada de 480 px;
- ✅ painel lateral da rota permanece ao lado do mapa, com largura responsiva e rolagem própria;
- ✅ estado real da navegação aparece no cabeçalho da página;
- ✅ linha/destino, ETA, próxima parada, progresso e distâncias continuam alimentados somente por dados reais;
- ✅ detalhes técnicos de mapas instalados saem da superfície principal sem alterar o diagnóstico interno;
- ✅ Central Multiplayer com painéis principais responsivos, alturas alinhadas e rolagem interna;
- ✅ estados da Home para Multiplayer e Empresa/Company Network localizados em pt-BR, inglês, espanhol, alemão e francês;
- ✅ CI leve dedicado à interface da Alpha.13 compila o cliente x86 sem publicar release;
- ✅ CCO com mapa real mais dominante, proporções de operação refinadas e status do mapa/rota/remotos nos 5 idiomas;
- ✅ Home responsiva em largura/altura, saudação neutra e estados dinâmicos localizados;
- ✅ Configurações com badge da versão real e seleção lateral visualmente sincronizada;
- ✅ Hardware com preview técnico recolhível e acabamento do expander;
- ✅ Saúde da Sessão com grade 3×3 responsiva e resumo semântico baseado em estado real;
- ✅ Empresa com badge da versão real do NavBR;
- ✅ Rede da Empresa reorganizada em duas áreas operacionais lado a lado (hospedar / entrar);
- ✅ Perfil com indicadores de carreira distribuídos uniformemente;
- 🚧 próximos passes de fidelidade: Equipe, ferramentas avançadas e microinterações finais.

## 4. Licenciamento e distribuição futura

A Alpha.13 introduz somente a **arquitetura de entitlement**, separada do núcleo do aplicativo.

Objetivo futuro:

- Steam;
- licença própria/chave;
- outras lojas;
- Alpha/Beta abertas enquanto o produto está em validação.

Nenhum bloqueio comercial é ativado nesta Test 1.

## 5. Benchmark técnico

Busweave e BusDriverMP são usados somente como referências de comportamento observado no mercado: ônibus remotos, estados visuais, portas, matriz e operação multiplayer. A implementação do NavBR continua independente e deve respeitar licenças e propriedade intelectual de terceiros.

## 6. Regra de dados reais

Nenhuma tela ou recurso deve inventar telemetria, ping, posição, ETA, compatibilidade ou estado do veículo. Quando a origem real não fornecer um valor, o NavBR deve mostrar indisponível/`—` ou desativar a capacidade correspondente.

## 7. Critério para avançar a Alpha.13

Antes de ampliar portas/matriz/articulação, a Test 1 precisa responder com clareza:

- o ônibus remoto nasce de forma consistente?;
- a pose está correta?;
- o movimento é estável?;
- o OMSI continua estável?;
- reconexão e despawn funcionam?;
- dois sentidos da sessão funcionam, PC A vendo B e PC B vendo A?

Os resultados desses testes definem a próxima Test build.
