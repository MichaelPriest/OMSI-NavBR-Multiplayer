# Alpha.15 — escopo mestre / Alpha.15 — master scope

## Português (pt-BR)

### Objetivo

A Alpha.15 prioriza **confiabilidade do runtime já implementado** antes de ampliar o produto.

### Em escopo

1. validar ônibus físico remoto em OMSI real;
2. validar troca de Kachel por GridX/GridY;
3. validar RP: sair, movimentar e retornar ao ônibus;
4. validar roadmap, TTData e retorno à rota em mapas reais;
5. validar instalação/atualização do Plugin Bridge em instalações Steam e não-Steam;
6. validar salas Servidor NavBR, LAN e Host Internet;
7. reduzir respawns, falsos positivos e mensagens genéricas;
8. preservar React como UI principal e C# como autoridade;
9. manter Portal V2 e catálogo público atualizados.

### Fora do escopo imediato

- tornar plugin obrigatório;
- redistribuir ônibus/mapas/HOFs;
- implementar articulados sem ownership/consist seguro;
- iniciar Mobile Companion antes da validação do núcleo;
- mascarar ausência de dados com mocks.

### Critério de promoção

Um recurso físico só deixa de ser experimental depois de repetir testes reais sem corrupção de estado, spawn invisível recorrente ou perda do motorista/RP.

## English (en)

### Goal

Alpha.15 prioritizes **reliability of the already implemented runtime** before expanding the product.

### In scope

1. validate remote physical buses in real OMSI sessions;
2. validate Kachel transitions using GridX/GridY;
3. validate Character/RP exit, movement and return-to-bus flow;
4. validate roadmap, TTData and route rejoin on real maps;
5. validate Plugin Bridge install/update across Steam and non-Steam installations;
6. validate NavBR Server, LAN and Internet Host rooms;
7. reduce respawns, false positives and generic errors;
8. keep React as the main UI and C# as the authority;
9. keep Portal V2 and the public catalog current.

### Not immediate scope

Articulated physical consists, mandatory plugin usage, redistributed OMSI assets and Mobile Companion implementation remain outside the immediate Alpha.15 runtime-validation focus.
