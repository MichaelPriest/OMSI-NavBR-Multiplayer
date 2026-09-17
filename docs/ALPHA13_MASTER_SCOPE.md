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
