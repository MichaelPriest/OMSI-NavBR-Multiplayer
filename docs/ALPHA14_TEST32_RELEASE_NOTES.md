# OMSI NavBR Multiplayer — Alpha.14 Test 3.2

Esta revisão corrige a sobreposição visual observada na Central Multiplayer.

## Correção principal

- Removido o assistente legado de criação de sala que era injetado por `ModuleInitializer`.
- A aba **Sala** agora usa somente o layout novo em XAML.
- **Criar sala** e **Entrar em sala** permanecem lado a lado, sem uma segunda camada por cima.
- Removido o polimento legado específico do wizard antigo.

## Relay preservado

O relay experimental não foi removido. Ele foi movido para:

**Avançado > Rede / NAT**

Assim, a correção visual não elimina a função de relay.

## Mantido nesta build

- funções restauradas no shell Figma;
- CCO, Empresa/Frota, Perfil, Rede da empresa e Equipe;
- Roadmap Studio, Instalações OMSI, Ghost 3D/replay;
- Diagnóstico, HUD e Hardware;
- RP / Personagem;
- bridge/plugin v3;
- simulador multiplayer e teste automatizado de movimento.
