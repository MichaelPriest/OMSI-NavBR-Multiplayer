# Alpha.12 — Figma Fidelity UI

Este documento registra a implementação visual usada pela Alpha.12 do OMSI NavBR Multiplayer na branch `feature/alpha12-full-expansion`.

## Direção visual

A interface segue o conceito de centro profissional de operação de transporte, evitando aparência de launcher gamer ou painel técnico genérico.

Paleta base:

- background: `#06101A`
- sidebar: `#07121B`
- card: `#0A131A`
- card elevado: `#0D1A24`
- borda: `#1C2A33`
- azul NavBR: `#3D89C4`
- azul interação: `#71C6FF`
- sucesso: `#38C98C`
- atenção: `#F2B84B`
- erro: `#EF5B64`
- texto principal: `#DAE6EE`
- texto secundário: `#97ABB9`

A fonte preferida é Inter quando disponível, com fallback para Segoe UI Variable/Segoe UI no Windows.

## Shell

- sidebar expandida: 252 px
- sidebar compacta: 72 px
- topbar: 64 px
- conteúdo fluido
- suporte mínimo previsto para 1366x768
- ícones vetoriais no menu lateral, sem emoji/Unicode como ícone final

### DIRIGIR

1. Início
2. Navegação
3. Multiplayer

### OPERAÇÃO

1. CCO
2. Empresa
3. Rede da Empresa
4. Equipe
5. Perfil

### SISTEMA

1. Hardware
2. HUD
3. Configurações
4. Saúde da Sessão

Ferramentas técnicas continuam atrás do modo avançado.

## Contrato de dados reais

A camada Figma é somente apresentação. Ela não pode inventar telemetria, ETA, ping, quantidade de membros, compatibilidade ou estado operacional.

Regras:

- valor ausente => `—`, estado indisponível ou mensagem de espera;
- nenhuma sala pública => estado vazio real, sem cards de demonstração;
- rota não resolvida => progresso/distâncias/manobras ficam indisponíveis;
- roadmap/layout ausente => mapa operacional mostra indisponibilidade;
- compatibilidade Multiplayer é calculada a partir dos manifests reais;
- CCO usa telemetria local/remota recebida, nunca posições artificiais;
- recursos planejados continuam explicitamente planejados/experimentais.

## Home

A Home usa somente informação real disponível no cliente:

- estado do OMSI
- veículo
- mapa
- linha/destino
- próxima parada
- rua atual
- velocidade
- atraso/adiantamento disponível na telemetria
- estado do Multiplayer
- empresa/Company Network

Os quatro cards superiores e o bloco de operação seguem as proporções do frame aprovado; ações rápidas apontam para os módulos reais do aplicativo.

## Navegação

O mapa 2D continua usando o roadmap real instalado no OMSI.

A página usa mapa dominante + rail lateral de rota. O painel usa `NavBRNavigationEngine` para calcular a partir da geometria instalada:

- progresso da rota
- distância restante
- distância até a próxima parada quando resolvível
- próxima manobra
- distância até a manobra
- saída da rota
- reaproximação da rota
- linha e destino reais da telemetria

O seletor `2D | 3D` é o ponto visual do Figma. O modo 3D reaproveita a visualização real existente com roadmap, rota ativa, ônibus local e veículos multiplayer compatíveis.

## Multiplayer

A página do shell não mantém salas fictícias em runtime.

O navegador consulta o diretório real e apresenta, quando informado pelo host:

- sala
- jogadores
- mapa obrigatório
- build/fingerprint do mapa
- versão OMSI
- versão NavBR
- protocolo
- ônibus
- HOF
- compatibilidade

A composição usa lista de salas à esquerda e detalhe/entrada da Central à direita, no mesmo ritmo visual do frame aprovado.

Fechar a Central Multiplayer não encerra host, conexão, voz ou telemetria. A sessão continua no mesmo objeto da aplicação até encerramento explícito ou fechamento completo do NavBR.

## CCO

A antiga grade/rota esquemática não é fonte de dados operacional.

A Alpha.12 substitui essa área, após o carregamento da janela, por:

- roadmap real do mapa ativo
- geometria real da rota instalada
- posição real do ônibus local
- posições reais dos jogadores remotos quando Grid/Tile e compatibilidade estão disponíveis
- lista operacional real de motoristas remotos

Quando roadmap, layout, posição ou rota não estão disponíveis, o CCO mostra o estado indisponível em vez de desenhar uma rota artificial.

## Empresa, Rede, Equipe e Perfil

As janelas de CCO, Empresa, Rede da Empresa, Equipe e Perfil compartilham o mesmo styler de fidelidade:

- fundo `#06101A`
- cards neutros `#0A131A`
- elevados `#0D1A24`
- bordas `#1C2A33`
- ações primárias em azul NavBR
- ações destrutivas em vermelho semântico
- dimensões adaptadas à área útil do Windows

A camada visual não altera papéis, permissões, assinatura das operações administrativas nem o armazenamento da Company Network.

## Sistema

O grupo SISTEMA mantém quatro entradas funcionais: Hardware, HUD, Configurações e Saúde da Sessão.

### Hardware

O Hardware Cockpit preserva o bridge Serial real e a telemetria enviada ao Arduino/ESP32. O passe Figma remove o cabeçalho interno duplicado, aplica os tokens do design e normaliza sucesso/atenção/erro sem alterar protocolo ou transporte.

### HUD

O atalho HUD abre a seção real de HUD nas Configurações. Temas/presets existentes continuam sendo a fonte de verdade do overlay; não há segundo estado de configuração criado apenas para o shell.

### Configurações

A janela centralizada preserva Geral, Aparência, HUD, Navegação, Multiplayer, Voz, Hardware e Avançado, agora dentro da mesma linguagem visual das telas operacionais.

### Saúde da Sessão

A tela usa métricas reais disponíveis: estado OMSI, Multiplayer, plugin, remotos, freshness, latência, jitter, perda e frequência adaptativa de telemetria. Métrica sem amostra suficiente permanece `—`.

## Estado da implementação dos frames salvos

A estrutura dos frames salvos no Figma já está refletida no código para:

- Design System
- Home Operacional
- Navegação GPS
- Multiplayer
- CCO
- Empresa
- Company Network
- Equipe da Empresa
- Perfil do Motorista
- HUD Studio / configuração de HUD
- Sistema

## Polimentos posteriores

A implementação atual já usa a estrutura e os tokens disponíveis no design salvo. Quando o acesso de edição/consulta do Figma estiver novamente disponível, os ajustes restantes são de acabamento visual, não de substituição de mock por funcionalidade:

- revisão pixel a pixel de espaçamentos e tipografia
- consolidação visual de hover/pressed/focus/disabled
- revisão em 1600x900, 1920x1080, 2560x1440 e ultrawide
- avaliação de incorporação do 3D dentro da própria página em vez de janela separada
- localização completa das strings novas do shell em pt-BR, inglês, espanhol, alemão e francês

Esses polimentos não autorizam inserir dados de demonstração no runtime.
