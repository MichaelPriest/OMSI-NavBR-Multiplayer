# Manual de Uso — OMSI NavBR Multiplayer

> Manual atualizado para **v0.3.0-alpha.21**.

## 1. Primeira abertura

1. Abra o NavBR.
2. Na Home, use **Executar OMSI** ou abra o OMSI manualmente.
3. Se o NavBR não encontrar uma instalação válida, abra **Instalações OMSI** e selecione a pasta que contém Omsi.exe.
4. Carregue mapa e ônibus.
5. Aguarde a telemetria ficar disponível.

## 2. HUD

O HUD pode mostrar velocidade, linha, destino, próxima parada, minimapa, rota, jogadores, chat e estado de voz/conexão.

### Mover HUD

Use **Mover HUD**:

- no topo do app;
- em Sistema;
- nas ações rápidas da Home.

O modo permite mover/redimensionar o HUD. Clique novamente para sair da edição.

## 3. Navegação e Mapa 3D

O NavBR usa os assets instalados localmente no OMSI. Não redistribui mapas, ônibus ou HOFs.

Mapa 3D e navegação dependem dos dados realmente disponíveis no mapa carregado.

## 4. Criar uma sala

1. Abra **Central Multiplayer > Sala**.
2. Informe apelido e nome da sala.
3. Escolha pública ou privada.
4. Para sala privada, defina senha.
5. Clique em Criar sala.

Porta padrão: **TCP 27730**.

## 5. Firewall

Em Conectividade/Avançado, use a ação para liberar TCP 27730.

- o Windows solicita UAC;
- a regra vale para perfis Privado, Público e Domínio;
- o NavBR verifica a regra após criar;
- se você cancelar o UAC, o app informa que nada foi alterado.

## 6. Entrar em sala

Informe o servidor, por exemplo:

    http://192.168.1.50:27730

Depois informe a sala. Em sala privada, informe a senha.

Também é possível usar Salas públicas ou convite quando disponível.

## 7. Chat e voz

- F9: chat por padrão;
- F10: push-to-talk por padrão;
- Chat e PTT podem ser remapeados para as combinações suportadas pelo NavBR;
- opções de voz ficam em Chat & Voz;
- o NavBR verifica conflitos contra `Inputs\keyboard.cfg`.

Manual completo: [KEYBOARD_SHORTCUTS.md](KEYBOARD_SHORTCUTS.md).

## 8. Simulador Multiplayer

O simulador é uma ferramenta de teste separada.

Quando usado com uma sala real, ele deve:

- entrar no mesmo mapa;
- aguardar posição real do host;
- criar bots próximos, por padrão em raio de 18 m;
- herdar linha, rota, destino e próxima parada da operação ativa.

Para sala privada, informe a senha ao simulador.

## 9. Plugin experimental

O plugin é necessário para ônibus remoto físico e Personagem/RP.

Arquivos principais:

    NavBR.OmsiPlugin.dll
    NavBR.OmsiPlugin.opl
    NavBR.OmsiInterop.dll

A Alpha.21 usa Plugin Bridge v3 e state interop ABI v20.

## 10. Personagem / RP

1. Carregue um mapa.
2. Abra Personagem/RP.
3. Selecione um personagem real da lista Drivers.
4. Ative o modo.
5. Use W/S, A/D, Shift e Esc.
6. Volte ao ônibus e confira a restauração.

RP continua experimental; câmera dedicada, terreno inclinado e animações ainda exigem validação.

## 11. Ônibus remoto físico

Recurso experimental e opt-in. Para testar, os PCs devem ter OMSI compatível, plugin atualizado, mapa compatível e o veículo remoto disponível localmente.

**Estado atual:** o cenário online com bots/AI do simulador seguindo o host foi validado com sucesso em OMSI real. Ainda falta concluir a mesma validação com players reais em dois PCs/duas sessões OMSI independentes.

Veja também [MULTIPLAYER_STATUS.md](MULTIPLAYER_STATUS.md).

## 12. Se algo não funcionar

Reporte:

- versão do NavBR;
- versão do OMSI;
- mapa;
- ônibus;
- linha/rota;
- se o plugin estava instalado;
- se o recurso físico/RP estava ativo;
- mensagem exibida pelo app;
- se houve crash ou queda de FPS.

Checklist público: [ALPHA14_COMMUNITY.md](ALPHA14_COMMUNITY.md).


## NavBR TP/TS

O **NavBR TP/TS** é o painel operacional integrado ao HUD. Ele substitui a nomenclatura de desenvolvimento usada anteriormente e pertence ao próprio NavBR.

Recursos disponíveis:

- tecla **K** abre a configuração de operação;
- linha em modo automático ou manual;
- seleção manual de **TP (Terminal Primário)** ou **TS (Terminal Secundário)**;
- leitura de linha/curso, terminal e atraso quando o ônibus publica os dados IBIS;
- hora/data da simulação, temperatura interna, passageiros, velocidade e estado do horário quando essas variáveis existem no veículo;
- **lista real de rotas carregada do arquivo HOF do ônibus atual**;
- a lista HOF mostra linha, código de rota e destino/descrição;
- `Ctrl+Alt+F6`: mostrar/ocultar o NavBR TP/TS;
- `Ctrl+Alt+F7`: alternar o tema;
- `Ctrl+Alt+F8`: alternar o tamanho;
- `Ctrl+Alt+H`: mostrar/ocultar o HUD completo.

O NavBR TP/TS não inventa rotas. Se o ônibus não tiver um HOF compatível carregado ou se o HOF não contiver blocos `[infosystem_trip]`, a lista permanece vazia e o painel informa que está aguardando dados.

### Ônibus simulados e permanência na pista

O teste físico não usa mais uma órbita artificial. Os ônibus simulados seguem a trilha real do ônibus local com espaçamento por distância percorrida.

Para mantê-los na malha viária, o NavBR agora prioriza:

1. **paths de veículos (`[path]` tipo 0) definidos nas splines `.sli`**, incluindo o offset lateral real da faixa;
2. paths dos objetos de cruzamento/interseção;
3. somente quando um asset não possui path dirigível, o centro geométrico da spline pode ser usado como fallback.

O ônibus simulado só é materializado quando existe uma âncora de via confiável próxima. Se o path ficar temporariamente indisponível, ele mantém a última pose física válida em vez de circular, teleportar ou ser recriado.

Para validar o comportamento no teste, consulte `%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr.log` e procure por `physical-road-target`. O campo `source` deve indicar preferencialmente `vehicle-path` nas vias normais e `scenery-vehicle-path` dentro de cruzamentos. `spline-center-fallback` significa que o asset não expôs um path dirigível e deve ser tratado como fallback de compatibilidade.
