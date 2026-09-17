# Alpha.12 Test 3 — checklist de teste da comunidade

A **Alpha.12 Test 3** é a build de validação do novo sistema de HUDs modulares e redimensionáveis do NavBR. Ela mantém a correção do plugin introduzida na Test 2 e adiciona os novos presets, temas, widgets e editor visual do HUD.

## Antes de começar

1. Feche o OMSI completamente.
2. Abra o NavBR Alpha.12 Test 3.
3. Se ainda não instalou o plugin corrigido da Test 2, ative o modo avançado, abra **Diagnóstico técnico** e use **Instalar / atualizar plugin**.
4. Confirme que `NavBR.OmsiPlugin.dll`, `NavBR.OmsiInterop.dll` e `NavBR.OmsiPlugin.opl` estão em `OMSI 2\plugins`.
5. Abra o OMSI, carregue um mapa, ônibus, linha/rota e comece uma viagem.

## Teste prioritário — os seis HUDs

Teste cada preset durante gameplay real:

- **Compacto** — deve ocupar pouco espaço e manter linha/destino/velocidade/alertas essenciais legíveis;
- **Normal** — equilíbrio entre navegação e informações do veículo;
- **Completo** — deve comportar os módulos extras sem cortar conteúdo;
- **Cluster Digital** — mostrador de velocidade circular/digital e leitura rápida;
- **LCD / Âmbar** — visual monoespaçado/retrô, mantendo alta legibilidade;
- **Transparente Integrado** — fundo translúcido sem prejudicar a leitura da rua/cockpit.

Para trocar rapidamente, ative **Mover HUD** e use o menu do painel ou o botão **Editar HUD**.

## Teste de temas

Em cada preset, experimente:

- NavBR Modern;
- Painel de ônibus;
- LCD;
- Âmbar clássico;
- Claro.

Verifique se cores, textos, barras e bordas permanecem legíveis e se um tema não sobrescreve o outro após salvar/reabrir o aplicativo.

## Teste de redimensionamento

No editor visual teste:

- escala geral entre 60% e 180%;
- tamanhos rápidos Pequeno, Médio, Grande e XL;
- largura entre 280 e 960 px;
- altura automática e altura manual;
- opacidade entre 35% e 100%;
- ancoragem livre, superior/inferior e esquerda/centro/direita;
- escala automática pela resolução.

No modo **Mover HUD** teste também:

- roda do mouse = escala;
- `Shift + roda` = largura;
- `Ctrl + roda` = opacidade;
- arrastar o painel e salvar posição.

O HUD não deve sair permanentemente para fora da área visível nem impedir a interação normal com o OMSI quando o modo de edição estiver desligado.

## Teste dos módulos independentes

Ative/desative individualmente:

- combustível;
- acelerador/freio;
- indicadores do veículo;
- minimapa integrado;
- multiplayer no painel;
- alertas discretos;
- indicadores laterais.

Depois redimensione separadamente:

- minimapa;
- multiplayer;
- alertas;
- indicadores laterais.

Feche e abra o NavBR novamente e confirme que todas as preferências persistem.

## Minimapa integrado

Com roadmap disponível:

- confirme que o mapa exibido é o mapa real carregado;
- confirme que a rota continua funcionando;
- confirme que o marcador local permanece coerente com o sentido do ônibus;
- confirme que jogadores remotos só aparecem quando há telemetria compatível;
- confirme que mapas/builds incompatíveis não geram posições falsas.

A Test 3 não inventa coordenadas, ETA ou distância quando os dados necessários não estão disponíveis.

## Multiplayer no HUD

Com dois PCs, preferencialmente primeiro na mesma rede:

- crie/entre em uma sala;
- confira nome do motorista remoto;
- linha;
- velocidade;
- distância quando ambos estiverem no mesmo mapa compatível;
- entrada/saída de jogadores;
- reconexão;
- comportamento do widget quando não há jogadores remotos.

Chat, voz/PTT e a Central Multiplayer devem continuar funcionando como antes.

## Alertas e indicadores

Valide somente estados que o OMSI realmente fornecer durante o teste:

- combustível baixo;
- portas abertas/fechadas;
- parada solicitada;
- atraso/adiantamento;
- freio de estacionamento;
- setas/pisca-alerta;
- luzes;
- limpadores.

Os alertas devem ser discretos, sem ficar piscando a tela inteira ou encobrir o cockpit.

## Resoluções prioritárias

Se possível, teste pelo menos uma destas resoluções e informe qual usou:

- 1366×768;
- 1600×900;
- 1920×1080;
- 2560×1440;
- ultrawide 2560×1080 ou 3440×1440.

Observe especialmente:

- painel cortado;
- textos sobrepostos;
- minimapa deformado;
- escala exagerada/pequena;
- ancoragem incorreta após redimensionar a janela ou mudar resolução.

## Regressões que não podem acontecer

Confirme também:

- HUD aparece durante gameplay;
- HUD some nos menus/diálogos auxiliares do OMSI como previsto;
- navegação anterior continua funcionando;
- telemetria não para ao trocar de HUD;
- plugin continua conectando;
- Central Multiplayer continua abrindo;
- não reaparece o erro `Pacote interno do plugin incompleto: NavBR.OmsiInterop.dll`.

## Informações úteis em um relato de erro

Inclua, se possível:

- preset e tema do HUD;
- escala/largura/altura/opacidade usados;
- resolução do monitor e escala do Windows;
- versão exibida pelo NavBR;
- versão do OMSI;
- mapa e ônibus;
- captura da tela;
- se estava em tela cheia, janela ou borderless;
- conteúdo relevante de `%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-error.log` ou `navbr-plugin.log`;
- se estava usando o EXE standalone ou o ZIP.

## Status

A Test 3 continua sendo uma pré-release Alpha. O foco desta rodada é validar visualmente os novos HUDs no OMSI real e ajustar legibilidade, tamanhos e posições com base nos testes.
