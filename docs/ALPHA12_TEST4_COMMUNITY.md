# Alpha.12 Test 4 — teste comunitário

Esta pré-release congela o estado atual da `feature/alpha12-full-expansion` antes do início da NavBR Network distribuída.

## O que testar

### HUDs
- Compacto, Normal, Completo, Cluster Digital, LCD/Âmbar e Transparente Integrado.
- Temas NavBR Modern, Painel de ônibus, LCD, Âmbar clássico e Claro.
- Editor visual de HUD.
- Escala geral, largura, altura, opacidade e ancoragem.
- Redimensionamento individual de minimapa, multiplayer, alertas e indicadores.
- Autoescala em 1366x768, 1600x900, 1920x1080, 2560x1440 e ultrawide.

### Central Multiplayer
- Assistente Sala → Privacidade → Rede.
- Host direto TCP 27730 continua como padrão.
- Relay experimental opcional para CGNAT/double NAT.
- Cards reais de jogadores, voz rápida e acesso às salas públicas.
- Mapa da sessão usando apenas telemetria real; sem posições ilustrativas falsas.
- Resumo de compatibilidade de mapa/protocolo/ônibus/HOF.
- Indicador de HOST DIRETO / RELAY EXPERIMENTAL / CONECTADO AO HOST.
- Dono da sala e autoridade de tráfego, incluindo migração do dono quando ele sair.

### Session Sync
- Preview somente leitura do estado operacional da sessão.
- Linha, rota, destino, próxima parada e mapa compartilhados pela autoridade da sessão.
- Não altera hora, clima ou data do OMSI nesta versão.

### CCO / Operação
- Pedido de apoio, incidente e normalização pelo motorista.
- Fila de ocorrências no CCO.
- Reconhecer/resolver chamado apenas pela autoridade da sessão.
- Cards com estados NORMAL, ATRASADO, SEM TELEMETRIA, SOLICITA APOIO e INCIDENTE.

### Plugin e bridge
- Validar instalação do plugin embutido.
- `NavBR.OmsiPlugin.dll`, `NavBR.OmsiInterop.dll` e `NavBR.OmsiPlugin.opl` devem estar presentes.
- Testar abrir/fechar OMSI, HUD, multiplayer e bridge sem regressão.

## Testes de mapa

Para esta Test 4, confirme se o NavBR detecta corretamente o mapa carregado e se a compatibilidade entre jogadores aparece na Central Multiplayer.

Na próxima etapa da NavBR Network, toda sala pública deverá anunciar explicitamente o **mapa necessário para jogar**, junto de compatibilidade e, quando disponível, ônibus/HOF necessários, antes de permitir a entrada.

## Como reportar

Ao encontrar um problema, informe:
- resolução da tela;
- preset/tema do HUD;
- mapa carregado;
- modo de rede (direto ou relay);
- quantidade de jogadores;
- mensagem de erro ou comportamento observado.

Esta é uma pré-release Alpha e não substitui a última versão estável do projeto.
