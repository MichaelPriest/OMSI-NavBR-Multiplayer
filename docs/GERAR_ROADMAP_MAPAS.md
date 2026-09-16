# Como gerar Roadmaps para o NavBR

O NavBR usa uma imagem global de roadmap como fundo do GPS/minimapa. O arquivo preferido continua sendo:

```text
<PASTA_DO_OMSI>\maps\<NOME_DO_MAPA>\texture\map\whole.roadmap.bmp
```

A partir da **alpha.11**, o NavBR passa a desenvolver o **Roadmap Studio**, uma ferramenta integrada para criar esse arquivo sem depender obrigatoriamente do OMSI Editor.

> Estado atual: o Roadmap Studio está na branch de desenvolvimento da alpha.11 e ainda deve ser validado em mapas reais antes da publicação oficial.

## Roadmap Studio — Alpha.11

Abra o módulo:

```text
Roadmap Studio
```

na barra lateral da interface alpha.11.

O Studio oferece dois modos.

### Modo 1 — Montar pelas imagens de tile

Use quando o mapa já possuir arquivos semelhantes a:

```text
tile_-1_0.map.roadmap.bmp
tile_0_0.map.roadmap.bmp
tile_1_0.map.roadmap.bmp
```

mas não tiver conseguido concluir o `whole.roadmap.bmp`.

O NavBR:

1. lê o `global.cfg` como fonte oficial da grade do mapa;
2. identifica as coordenadas X/Y das imagens de roadmap pelo nome de cada tile;
3. verifica se as imagens têm dimensões compatíveis;
4. usa os limites min/max do `global.cfg`, inclusive quando uma tile de borda não possui imagem de roadmap;
5. distingue uma tile configurada sem imagem de uma célula vazia/ghost da grade retangular;
6. respeita a orientação dos eixos do OMSI;
7. monta o BMP final por streaming, linha a linha, para evitar consumir memória proporcional ao mapa inteiro;
8. preenche posições sem imagem com fundo escuro;
9. cria backup do `whole.roadmap.bmp` anterior, quando existir;
10. grava o novo arquivo em `texture\map\whole.roadmap.bmp`;
11. mostra preview e estatísticas no próprio Roadmap Studio.

Usar o `global.cfg` para os limites é importante: se o roadmap de uma tile da borda estiver ausente, o mosaico não deve encolher e deslocar todo o sistema de coordenadas do GPS.

Esse modo é o mais indicado quando o Editor conseguiu gerar roadmaps individuais, mas falhou ao montar a imagem final.

### Modo 2 — Gerar vetorial pelas splines

Use quando não existir nenhum roadmap por tile.

O NavBR lê diretamente:

```text
global.cfg
tile_*.map
[spline]
[spline_h]
```

e cria uma imagem de navegação a partir da geometria das splines.

O resultado é uma base **vetorial/navegável**, e não uma reprodução pixel a pixel do roadmap criado pelo OMSI Editor.

Na primeira implementação, o modo vetorial:

- lê a grade do mapa pelo `global.cfg`;
- suporta mapas normais e a escala tratada pelo leitor de `[worldcoordinates]` do NavBR;
- amostra splines retas e curvas;
- usa a mesma transformação de coordenadas do GPS;
- limita a dimensão máxima do bitmap para evitar consumo excessivo de memória;
- cria backup do roadmap anterior;
- grava metadados em `whole.roadmap.navbr.txt`;
- não precisa iniciar o OMSI Editor.

### Limitação inicial do modo vetorial

Alguns mapas constroem boa parte das ruas através de **crossings, scenery objects e paths internos de objetos**, e não apenas através de `[spline]` diretamente no tile.

Nesses mapas, a primeira versão do modo Vetorial pode produzir uma rede incompleta ou informar que não encontrou splines suficientes. O próximo estágio do gerador deve reutilizar os leitores de geometria de scenery/crossing já existentes no NavBR para completar essas vias.

## Segurança ao substituir roadmaps

O Roadmap Studio não substitui silenciosamente um arquivo existente sem manter uma cópia.

Quando já existir:

```text
whole.roadmap.bmp
```

o NavBR cria algo semelhante a:

```text
whole.roadmap.backup-20260915-193000.bmp
```

antes de publicar o novo arquivo.

Arquivos temporários incompletos são descartados se a operação falhar.

## Mapas muito grandes

O OMSI Editor pode falhar na geração de roadmaps grandes por memória/recursos. O modo por tiles do Roadmap Studio foi desenhado para reduzir esse problema porque escreve o BMP final por linhas, sem montar todas as tiles simultaneamente numa imagem gigante em RAM.

Mesmo assim existem limites de segurança:

- imagens finais absurdamente grandes são bloqueadas;
- BMPs estimados acima de aproximadamente 2 GB são recusados;
- o modo vetorial usa uma resolução global limitada para manter o consumo de memória previsível.

## Método clássico — OMSI Editor

O Editor continua sendo uma alternativa e pode ser usado para produzir o roadmap visual original do mapa.

### 1. Feche o OMSI normal

Evite manter uma sessão normal aberta enquanto trabalha no Editor.

### 2. Inicie o Editor

O método clássico é:

```text
Omsi.exe -editor
```

### 3. Abra o mapa

Carregue o mapa desejado.

### 4. Abra a aba `Tile`

### 5. Use `Create Roadmap`

O Editor percorre as tiles e normalmente gera os roadmaps individuais e o arquivo global.

Quando concluído corretamente, procure:

```text
<PASTA_DO_OMSI>\maps\<NOME_DO_MAPA>\texture\map\whole.roadmap.bmp
```

## Como confirmar que o NavBR encontrará o arquivo

Estrutura ideal:

```text
OMSI 2\
└─ maps\
   └─ MeuMapa\
      ├─ global.cfg
      ├─ tile_*.map
      └─ texture\
         └─ map\
            └─ whole.roadmap.bmp
```

O NavBR procura primeiro variantes globais como:

```text
texture\map\whole.roadmap.bmp
texture\map\roadmap.bmp
```

`texture\map\whole.roadmap.bmp` continua sendo o formato recomendado.

## Roadmap não é o traçado da linha

São camadas diferentes:

- **Roadmap:** fundo visual do GPS;
- **Traçado da linha:** calculado a partir de timetable e geometria (`TTData`, `.ttp`, `.ttr`, tiles `.map`, splines, crossings e paths);
- **Pontos de parada:** lidos dos objetos funcionais de parada nas tiles `.map` e desenhados como uma camada independente no HUD.

Portanto, criar `whole.roadmap.bmp` melhora o fundo visual, mas não corrige sozinho erros de timetable, rota ou objetos de parada do mapa.

## Quando regenerar

Vale regenerar quando:

- o mapa foi atualizado;
- novas tiles/ruas foram adicionadas;
- o roadmap ficou incompleto;
- o arquivo está corrompido;
- a imagem não corresponde mais à versão instalada do mapa.

## Checklist

Antes de testar um mapa no NavBR, confirme:

- `global.cfg` existe;
- tiles `.map` existem;
- `whole.roadmap.bmp` existe ou foi criado pelo Roadmap Studio, se um fundo visual for desejado;
- `TTData` ou `Chrono\*\TTData` existe para linhas/viagens;
- a viagem foi iniciada dentro do OMSI;
- todos os jogadores usam a mesma versão do mapa no multiplayer.

Para problemas de traçado, o log relevante continua sendo:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-route.log
```

## Referências técnicas

O desenvolvimento do Roadmap Studio segue a regra do projeto de consultar ferramentas/plugins OMSI existentes como referência antes de implementar recursos grandes. Foram estudados, entre outros, OMSI Launcher/OmsiHook e o projeto público OMSI RouteAdvisor para entender convenções de tiles, roadmaps e objetos de parada.

O NavBR usa essas referências para entender formatos e fluxos, mas mantém implementação própria e compatível com a licença do projeto.

As referências gerais usadas na alpha.11 ficam registradas em:

```text
docs/REFERENCIAS_OMSILAUNCH_OMSIHOOK.md
```
