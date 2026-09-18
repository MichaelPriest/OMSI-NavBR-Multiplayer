# OMSI NavBR Multiplayer — Alpha.14 Test 3.1 Corrigida

Esta revisão corrige a Test 3 publicada anteriormente sem sobrescrever os artefatos antigos.

## Correções de interface

- Paleta global WPF alinhada ao layout Figma aprovado.
- Sidebar, topbar, espaçamentos e cards harmonizados com a Central Multiplayer.
- Aba **Sala** reorganizada: identidade compacta no topo, **Criar sala** e **Entrar em sala** em áreas independentes.
- O bloco de criação de sala não cobre nem empurra as demais funções.

## Funções restauradas no app

As funções continuam usando os módulos reais existentes; nenhuma tela mock foi reintroduzida.

- CCO.
- Empresa / Frota.
- Perfil do motorista.
- Rede da empresa.
- Equipe da empresa.
- Hardware.
- HUD.
- Configurações.
- Diagnóstico técnico.
- Roadmap Studio.
- Instalações OMSI.
- Ghost 3D / replay.
- Personagem / RP.
- Central Multiplayer.

Funções de uso normal voltaram a ficar visíveis. Apenas controles técnicos/experimentais permanecem no modo avançado.

## Multiplayer e RP

Mantém as correções da Test 3:

- bridge protocol v3;
- interop state ABI v3;
- capabilities atualizadas em runtime;
- motorista RP destacado ao lado do ônibus;
- restauração de pose/vínculo/IA ao retornar;
- simulador de múltiplos jogadores;
- validação automática de movimento no mapa via SignalR.

## Artefatos

- EXE standalone Windows x86;
- ZIP cliente Windows x86;
- ZIP servidor Windows x64;
- ZIP plugin OMSI x86;
- simulador multiplayer dev x64;
- SHA256SUMS e documentação.
