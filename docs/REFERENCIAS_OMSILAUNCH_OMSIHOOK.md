# Referências técnicas: OMSI Launcher e OmsiHook

Este documento registra referências externas úteis ao desenvolvimento do OMSI NavBR Multiplayer. Elas são usadas como **referência de arquitetura e comportamento**, não como fonte para copiar código incompatível com a licença do NavBR.

## OMSI Launcher

Referência: `NyCodeGHG/omsi-launcher` (GPL-3.0, projeto arquivado).

Pontos úteis observados:

- Steam App ID do OMSI 2: `252530`.
- Manifesto Steam: `steamapps/appmanifest_252530.acf`.
- Steam pode manter a instalação do OMSI em bibliotecas/unidades diferentes.
- O OMSI também registra o caminho em `HKLM\SOFTWARE\WOW6432Node\aerosoft\OMSI 2`, valor `Product_Path`.
- Instalações podem usar symlinks/junctions, portanto o NavBR não deve presumir `C:\Program Files (x86)\Steam\...`.

Aplicação no NavBR:

- A detecção automática do OMSI consulta primeiro o caminho já conhecido/processo em execução.
- Em seguida consulta o registro Aerosoft (`Product_Path`).
- Depois percorre Steam e `libraryfolders.vdf`, encontra `appmanifest_252530.acf` e usa `installdir`.
- Se nada for encontrado, o seletor manual de pasta continua disponível.
- O NavBR permanece independente da Steam: Steam é apenas uma das fontes opcionais de descoberta da instalação.

## OmsiHook / Omsi-Extensions

Referência: `space928/Omsi-Extensions` / `OmsiHook` (LGPL-3.0).

O projeto declara suporte ao OMSI `2.3.004`, que também é o alvo inicial do NavBR.

Pontos arquiteturais relevantes:

- anexa a um processo OMSI em execução;
- expõe `Globals.PlayerVehicle`;
- expõe a coleção global de `RoadVehicles`;
- mapeia posição, rotação, velocidade e diversos estados de veículos;
- possui propriedades de leitura e escrita sobre objetos já existentes;
- possui uma camada de métodos nativos/RPC para operações que precisam executar dentro do processo do OMSI;
- usa um plugin interno + comunicação com processo externo, conceito próximo ao bridge do NavBR;
- a própria documentação informa que a biblioteca ainda não resolve operações que exigem alocação de memória, como simplesmente adicionar elementos a arrays.

### Relação com a arquitetura NavBR

A arquitetura atual permanece:

`SignalR -> NavBR.Client -> Named Pipe v1 -> NavBR.OmsiPluginExperimental`

OmsiHook reforça três decisões atuais:

1. **Plugin dentro do OMSI + app externo** é um caminho válido para atravessar o limite do processo.
2. **Named Pipe/RPC** é apropriado para manter rede/UI fora do OMSI e limitar o código in-process.
3. A futura representação 3D deve tratar criação e ciclo de vida de veículos como uma operação específica do OMSI, e não simplesmente escrever coordenadas em um endereço arbitrário.

## Próxima investigação para veículos remotos 3D

Depois da aceitação da alpha.10/testes de estabilidade:

1. Enumerar `RoadVehicles` com uma build de pesquisa somente leitura.
2. Identificar com segurança player bus versus veículos AI já existentes.
3. Validar coordenadas, rotação, velocidade, tile/path e estados de IA em OMSI 2.3.004.
4. Investigar métodos internos já mapeados pelo OmsiHook/OmsiHookInvoker que possam criar/remover uma entidade por um caminho equivalente ao mecanismo AI do OMSI.
5. Nunca reutilizar um veículo do jogador ou sobrescrever entidades de terceiros.
6. Só habilitar escrita/criação atrás de uma opção experimental explícita e depois de validação de versão/fingerprint.

A test.6 **não deve** prometer ônibus remotos físicos. Ela estabiliza plugin Native AOT, bridge, instalação, HUD/GPS, chat e diagnóstico. A investigação 3D continua como próxima camada experimental.

## Licenças

- `NyCodeGHG/omsi-launcher`: GPL-3.0. Usar como referência conceitual; não incorporar código GPL diretamente ao NavBR sem uma decisão explícita de licenciamento.
- `space928/Omsi-Extensions`: LGPL-3.0. Antes de adicionar uma dependência direta, revisar obrigações de redistribuição e compatibilidade. A pesquisa atual usa documentação/arquitetura e não incorpora seus binários ou código-fonte.
