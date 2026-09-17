# Alpha.12 — Figma Preview UI

Este documento registra a implementação visual usada pela Alpha.12 Figma Preview do OMSI NavBR Multiplayer.

## Direção visual

A interface segue o conceito de centro profissional de operação de transporte, evitando aparência de launcher gamer ou painel técnico.

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

## Shell

- sidebar expandida: 252 px
- sidebar compacta: 72 px
- topbar: 64 px
- conteúdo fluido
- suporte mínimo previsto para 1366x768

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

## Home

A Home usa somente informação real disponível no cliente:

- estado do OMSI
- veículo
- mapa
- linha/destino
- próxima parada
- rua atual
- velocidade
- atraso/adiantamento
- estado do Multiplayer
- empresa/Company Network

Quando uma fonte não fornece um valor, o componente exibe `—` ou um estado indisponível. Valores de demonstração do Figma nunca são usados como telemetria do aplicativo.

## Navegação

O mapa 2D continua usando o roadmap real instalado no OMSI.

O painel de rota usa `NavBRNavigationEngine` para calcular a partir da geometria instalada:

- progresso da rota
- distância restante
- distância até a próxima parada quando resolvível
- próxima manobra
- distância até a manobra
- saída da rota
- reaproximação da rota

O seletor `2D | 3D` é o ponto visual do Figma. O modo 3D reaproveita a visualização real existente com roadmap, rota ativa, ônibus local e veículos multiplayer compatíveis.

## Multiplayer

A página do shell não contém salas fictícias.

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

Fechar a Central Multiplayer não encerra host, conexão, voz ou telemetria. A sessão continua no mesmo objeto da aplicação até encerramento explícito ou fechamento completo do NavBR.

## CCO

A antiga grade/rota esquemática não é fonte de dados operacional.

A Figma Preview substitui essa área por:

- roadmap real do mapa ativo
- geometria real da rota instalada
- posição real do ônibus local
- posições reais dos jogadores remotos quando Grid/Tile e compatibilidade estão disponíveis
- lista operacional real de motoristas remotos

Quando roadmap, layout, posição ou rota não estão disponíveis, o CCO mostra o estado indisponível em vez de desenhar uma rota artificial.

## Empresa e Company Network

As janelas de CCO, Empresa, Rede da Empresa, Equipe e Perfil recebem a mesma linguagem visual do shell sem alterar sua lógica de negócio.

A Home mostra estado real da Company Network sem inventar número de membros online.

O Company Node continua independente da sala multiplayer e usa TCP 27740.

## Próximos polimentos

Quando a edição do Figma estiver novamente disponível:

- ajustar pixel a pixel espaçamentos e tipografia
- consolidar hover/pressed/focus/disabled
- revisar responsividade em 1600x900, 1920x1080, 2560x1440 e ultrawide
- avaliar incorporação do 3D dentro da própria página em vez de janela separada
- avançar a migração das telas operacionais de diálogos para páginas do workspace quando isso não comprometer a lógica existente
- concluir localização das novas strings do shell Figma em pt-BR, inglês, espanhol, alemão e francês
