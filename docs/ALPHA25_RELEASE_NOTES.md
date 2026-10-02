# OMSI NavBR Multiplayer v0.3.0-alpha.25

Esta Alpha consolida a interface oficial **React/WebView2** e leva adiante o multiplayer físico para OMSI 2 e a integração paralela com openOMSI.

## Principais mudanças

- interface desktop oficial em React/WebView2;
- backend/runtime OMSI mantido em x86 para compatibilidade real com OMSI 2;
- plugin Native AOT x86 e bridge físico preservados;
- gateway LAN v6 do openOMSI com HELLO/WELCOME/INFO/STATE/PLACE/NEAR/BYE/CLOCK;
- identidade real do veículo com caminho + SHA-256;
- atualização da presença sem churn a 20 Hz;
- telemetria física em movimento com alvo de **20 Hz (50 ms)**;
- recuperação de pose na troca de **Kachel** usando PhysicalGrid/NavigationGrid/TileXY coerentes;
- rejeição de recuperação quando PhysicalGrid e NavigationGrid pertencem a tiles diferentes;
- diferenças de HOF continuam como aviso e **não bloqueiam** o spawn físico;
- correções nos smokes do Plugin Bridge e novo smoke atravessando limite de Kachel;
- instalador Windows x86 validado com instalação e desinstalação silenciosas em CI.

## Validação

Os pipelines automatizados de build, desktop client, openOMSI plugin e Render server passaram no commit validado antes da promoção.

**Importante:** o teste ponta a ponta com dois jogadores reais, em dois PCs/duas sessões OMSI, ainda está pendente. Esta versão continua sendo uma **Alpha pública de teste**.

## Compatibilidade

- OMSI 2 2.3.004 / processo x86;
- openOMSI via plugin/gateway compatível;
- Windows desktop React/WebView2;
- Mobile Companion Android permanece incluído no fluxo público.
