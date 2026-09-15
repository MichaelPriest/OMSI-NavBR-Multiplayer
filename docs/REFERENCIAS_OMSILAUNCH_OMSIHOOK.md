# Referências técnicas para o desenvolvimento do NavBR

Este documento registra referências externas úteis ao desenvolvimento do OMSI NavBR Multiplayer. Elas são usadas como **referência de arquitetura, formatos, comportamento e limites técnicos**, não como fonte para copiar código incompatível com a licença do NavBR.

## Regra de desenvolvimento da alpha.11 em diante

Antes de implementar um recurso grande que interaja com o OMSI, o desenvolvimento deve procurar projetos/plugins públicos que já tenham investigado a mesma área.

A pesquisa deve responder, quando aplicável:

- qual API/estrutura do OMSI já foi mapeada;
- quais formatos de arquivo são usados na prática;
- como o projeto detecta instalações e versões;
- quais offsets/estruturas são específicos da versão 2.3.004;
- quais operações precisam executar dentro do processo do OMSI;
- quais falhas/crashes são conhecidos;
- qual é a licença da referência;
- o que pode ser reaproveitado apenas como conhecimento e o que poderia ser uma dependência legítima.

O NavBR deve preferir **implementação própria**, testes defensivos e feature flags para funções de escrita no simulador.

## OMSI Launcher

Referência: `NyCodeGHG/omsi-launcher` (GPL-3.0, projeto arquivado).

Pontos úteis observados:

- Steam App ID do OMSI 2: `252530`;
- manifesto Steam: `steamapps/appmanifest_252530.acf`;
- Steam pode manter o OMSI em bibliotecas/unidades diferentes;
- o OMSI também registra o caminho em `HKLM\SOFTWARE\WOW6432Node\aerosoft\OMSI 2`, valor `Product_Path`;
- instalações podem usar symlinks/junctions;
- o conceito de múltiplas instalações/perfis é útil para separar conjuntos de mapas/ônibus sem presumir uma única pasta fixa.

Aplicação no NavBR:

- detecção automática consulta primeiro caminho conhecido/processo em execução;
- depois consulta registro Aerosoft (`Product_Path`);
- depois percorre Steam e `libraryfolders.vdf`, encontra `appmanifest_252530.acf` e usa `installdir`;
- seletor manual continua disponível;
- a alpha.11 possui base para perfis/instalações e launcher integrado;
- o NavBR permanece independente da Steam: Steam é apenas uma fonte opcional de descoberta.

## OmsiHook / Omsi-Extensions

Referência: `space928/Omsi-Extensions` / `OmsiHook` (LGPL-3.0).

O projeto declara suporte ao OMSI `2.3.004`, alvo inicial do NavBR.

Pontos arquiteturais relevantes:

- anexa a um processo OMSI em execução;
- expõe `Globals.PlayerVehicle`;
- expõe a coleção global de `RoadVehicles`;
- mapeia posição, rotação, velocidade e diversos estados de veículos;
- possui propriedades de leitura e escrita sobre objetos já existentes;
- possui camada de métodos nativos/RPC para operações que precisam executar dentro do processo do OMSI;
- usa um plugin interno + comunicação com processo externo, conceito próximo ao bridge do NavBR;
- contém referências experimentais a criação/colocação de veículos, incluindo métodos equivalentes a `MakeVehicle(...)` e `PlaceRandomBus(...)`;
- a própria arquitetura demonstra que operações complexas de ciclo de vida precisam ser tratadas como chamadas específicas, e não como escrita arbitrária de memória.

### Relação com a arquitetura NavBR

A alpha.11 evolui o fluxo para:

`SignalR -> NavBR.Client -> Named Pipe / bridge v2 -> plugin Native AOT x86 -> OMSI`

OmsiHook reforça decisões importantes:

1. **Plugin dentro do OMSI + app externo** é um caminho válido para atravessar o limite do processo.
2. **Named Pipe/RPC** permite manter rede/UI fora do OMSI e reduzir código in-process.
3. A futura representação 3D deve tratar criação e ciclo de vida de veículos como operações específicas do simulador.
4. Leitura e escrita devem ter perfis/fingerprints de versão e falhar fechado quando uma capacidade não estiver validada.

## Telemetria avançada

A alpha.11 usa estruturas públicas já investigadas pela comunidade como referência para ampliar a telemetria em modo defensivo.

Entre os estados pesquisados/implementados na camada de dados estão:

- acelerador;
- freio;
- combustível;
- atraso/horário;
- próxima parada;
- portas;
- luzes;
- setas;
- freio de estacionamento;
- ré;
- limpadores;
- posição/rotação/velocidade.

Quando um valor não passa pela validação de faixa/tipo, o NavBR deve tratá-lo como indisponível em vez de propagar dados corrompidos.

## Ghost 3D e veículo remoto

A alpha.11 separa a implementação em etapas:

1. registrar telemetria local em um arquivo Ghost;
2. reproduzir/interpolar os frames localmente;
3. enviar comandos `spawn/update/despawn` pelo bridge v2;
4. exigir capabilities declaradas pelo plugin;
5. fazer o plugin responder com `ack` ou erro explícito;
6. validar o backend de criação física em OMSI 2.3.004;
7. somente depois substituir a fonte Ghost pela telemetria SignalR de outro jogador.

Enquanto o backend físico não estiver validado, o plugin deve retornar algo equivalente a `writes-disabled` em vez de tentar operações arriscadas.

## Roadmap Studio

A pesquisa de roadmaps segue a mesma regra: entender primeiro os arquivos que o OMSI e ferramentas da comunidade já produzem.

A alpha.11 implementa dois modos próprios:

### Montagem por tiles

Reconhece roadmaps individuais no padrão usado em mapas OMSI, por exemplo:

```text
tile_-1_0.map.roadmap.bmp
tile_0_0.map.roadmap.bmp
```

O NavBR combina essas imagens usando a grade do mapa, respeita a inversão do eixo Y entre coordenadas do OMSI e bitmap e escreve o `whole.roadmap.bmp` por streaming.

### Geração vetorial

Quando não existe roadmap por tile, o NavBR lê `global.cfg`, tiles `.map`, `[spline]` e `[spline_h]` e gera uma base de navegação própria.

Essa imagem é deliberadamente uma representação NavBR e não pretende reproduzir pixel a pixel o render do OMSI Editor.

Próxima pesquisa para esse módulo:

- crossings;
- paths dentro de scenery objects;
- road objects que concentram grande parte da malha viária;
- convenções adicionais usadas por ferramentas públicas de mapa/route advisor.

## Interface e execução em segundo plano

A alpha.11 também reorganiza a interface em módulos, mantendo tarefas técnicas fora da tela principal.

A nova shell agrupa:

- visão geral;
- GPS/mapas;
- multiplayer;
- Roadmap Studio;
- instalações/perfis OMSI;
- Ghost 3D;
- plugin/diagnóstico;
- configurações e HUD.

O app também ganha modo de bandeja do Windows para continuar executando host, bridge/HUD e demais serviços quando a janela principal estiver oculta.

## Licenças

- `NyCodeGHG/omsi-launcher`: GPL-3.0. Usar como referência conceitual; não incorporar código GPL diretamente ao NavBR sem decisão explícita de licenciamento.
- `space928/Omsi-Extensions`: LGPL-3.0. Antes de adicionar uma dependência direta, revisar obrigações de redistribuição e compatibilidade. A pesquisa atual usa arquitetura/documentação/estruturas como referência e não incorpora seus binários ao NavBR.

## Regra de segurança

Nenhuma referência externa, por si só, é suficiente para habilitar escrita em memória ou criação de objetos no OMSI.

Para uma capability de escrita ser ativada, ela deve passar por:

1. validação da versão/fingerprint do OMSI;
2. teste automatizado do bridge/plugin;
3. teste local controlado (Ghost quando aplicável);
4. fallback seguro;
5. possibilidade de desativação pelo usuário;
6. teste real antes da promoção para uma alpha oficial.
