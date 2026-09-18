# OMSI NavBR Multiplayer — Alpha.14 Test 3.2

Esta revisão corrige a sobreposição visual da Central Multiplayer e restaura os acessos que haviam ficado escondidos pelo redesign Figma.

## Central Multiplayer

- Removido o assistente legado **1 Sala / 2 Privacidade / 3 Rede** que era injetado por `ModuleInitializer`.
- A aba **Sala** usa somente o layout novo em XAML.
- **Criar sala** e **Entrar em sala** ficam lado a lado, sem segunda camada por cima.
- **Salas públicas** voltou como botão fixo no card de entrada.
- Opções/canais de voz voltaram a ser inicializados no fluxo atual da Central.
- Relay experimental foi preservado em **Avançado > Rede / NAT**.

## Funções restauradas no app

A sidebar Figma volta a oferecer acesso visível às funções reais existentes:

- Navegação e **Mapa 3D**.
- Multiplayer e **Personagem / RP**.
- CCO.
- Empresa / Frota.
- Rede da empresa.
- Equipe da empresa.
- Perfil do motorista e histórico de viagens.
- Hardware.
- **Mover HUD**.
- **Configurar HUD / Editor do HUD**.
- Configurações.
- Saúde da sessão.
- Diagnóstico técnico.
- Roadmap Studio.
- Instalações OMSI.
- Ghost 3D / replay.
- Conectividade Multiplayer.
- Diagnóstico NAT / UPnP.
- Teste externo TCP 27730.
- Manual.
- Feedback.

Somente controles internos/experimentais continuam atrás de **Mostrar modo avançado**.

## Correções visuais

- `ComboBox` / selects agora usam template NavBR escuro completo.
- Dropdown, itens, hover, seleção e item desabilitado não usam mais o fundo branco padrão do Windows.
- Sidebar compacta identifica também as funções restauradas com ícones/tooltip.
- Entradas legadas duplicadas de HUD e Ghost deixam de ser adicionadas quando a superfície Figma já existe.

## Simulador Multiplayer

- Conexão recusada em `127.0.0.1:27730` não gera mais stack trace bruto.
- O simulador verifica `/health` antes de conectar.
- Se localhost estiver vazio, tenta iniciar automaticamente o **NavBR.Server** empacotado.
- O ZIP do simulador inclui a pasta `server` e um launcher PowerShell funcional fora do repositório.
- Se o app já estiver hospedando a sala na porta 27730, o simulador reutiliza esse host e não inicia outro servidor.
- O console informa como entrar na sala `navbr-sim` pelo app quando o servidor for iniciado automaticamente.

## Mantido nesta build

- bridge/plugin protocol v3;
- interop state ABI v3;
- RP / Personagem;
- simulador de múltiplos players;
- teste automatizado de movimento via SignalR;
- cliente WPF x86, plugin OMSI x86 e servidor dedicado x64.
