# Manual de Uso — OMSI NavBR Multiplayer

> Manual atualizado para **v0.3.0-alpha.14**.

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

- F9: chat;
- F10: push-to-talk;
- opções de voz ficam em Chat & Voz.

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

A Alpha.14 usa bridge/protocolo v3.

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
