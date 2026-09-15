# Como gerar o Roadmap de um mapa para o NavBR

O NavBR usa o **Roadmap gerado pelo próprio OMSI Editor** como imagem de fundo do GPS/minimapa. O arquivo preferido é:

```text
<PASTA_DO_OMSI>\maps\<NOME_DO_MAPA>\texture\map\whole.roadmap.bmp
```

Sem esse arquivo o mapa ainda pode ser detectado pelo `global.cfg` e o traçado da viagem ainda pode ser processado, mas o GPS pode ficar sem a imagem de fundo do mapa.

## Método recomendado — OMSI Editor

### 1. Feche o OMSI normal

Evite manter uma sessão normal do jogo aberta enquanto for trabalhar no Editor.

### 2. Inicie o OMSI em modo Editor

Abra o OMSI Map Editor da forma usada na sua instalação. O método clássico é iniciar o executável com o parâmetro:

```text
Omsi.exe -editor
```

Em instalações com atalho próprio para o Editor, pode usar esse atalho.

### 3. Abra o mapa

Carregue o mapa para o qual deseja criar o roadmap.

Exemplo:

```text
<PASTA_DO_OMSI>\maps\SP_Projeto_Metra_Ficticio\
```

### 4. Abra a aba `Tile`

No painel do Editor, selecione:

```text
Tile
```

### 5. Clique em `Create Roadmap`

Na parte inferior da aba `Tile`, use:

```text
Create Roadmap
```

O OMSI percorre as tiles e gera as imagens do roadmap. Dependendo do tamanho do mapa, esse processo pode demorar bastante e o Editor pode parecer parado durante parte do processamento.

### 6. Aguarde o processo terminar

Não feche o Editor enquanto ele estiver gerando os arquivos.

Quando concluído corretamente, procure principalmente por:

```text
<PASTA_DO_OMSI>\maps\<NOME_DO_MAPA>\texture\map\whole.roadmap.bmp
```

O Editor também pode gerar arquivos de roadmap correspondentes às tiles dentro da mesma pasta.

## Como confirmar que o NavBR encontrará o arquivo

A estrutura ideal é:

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

O NavBR procura primeiro:

```text
texture\map\whole.roadmap.bmp
texture\map\roadmap.bmp
```

Também existem fallbacks para variantes de nome/localização, mas `texture\map\whole.roadmap.bmp` é o formato recomendado.

## Importante: Roadmap não é o traçado da linha

São coisas diferentes:

- **Roadmap**: imagem de fundo do mapa, gerada pelo OMSI Editor;
- **Traçado da linha**: calculado pelo NavBR a partir de `TTData`, `.ttp`, `.ttr`, tiles `.map`, splines `.sli` e paths/crossings `.sco`.

Portanto, gerar `whole.roadmap.bmp` melhora/viabiliza o fundo do GPS, mas não corrige sozinho um problema de rota ou timetable.

## Se `whole.roadmap.bmp` já existir

Não é necessário gerar novamente para usar o NavBR.

Só vale regenerar quando:

- o mapa foi atualizado;
- novas tiles/ruas foram adicionadas;
- a imagem ficou incompleta;
- o arquivo está corrompido;
- o roadmap não corresponde mais à versão instalada do mapa.

Antes de substituir um arquivo existente, faça backup.

## Mapas muito grandes

O gerador do OMSI Editor pode falhar em mapas com muitas tiles por limitações do próprio OMSI/Editor, especialmente por memória/recursos.

Sintomas comuns:

- `Create Roadmap` demora muito e fecha com erro;
- erro de recursos/memória;
- erro de faixa/range check;
- somente parte das tiles recebe arquivos `*.roadmap.bmp`;
- `whole.roadmap.bmp` não é concluído.

### O que tentar

1. faça backup da pasta do mapa;
2. verifique o `logfile.txt` do OMSI após a falha;
3. valide se existem tiles/objetos/splines quebrados;
4. tente gerar novamente com outros programas fechados;
5. em mapas enormes, pode ser necessário gerar roadmaps parciais/tile a tile e montar uma imagem final manualmente.

A montagem manual deve respeitar exatamente a grade e a orientação das tiles. Um BMP montado incorretamente fará o marcador do NavBR aparecer deslocado mesmo quando a telemetria estiver correta.

## Se o Editor gerar apenas arquivos por tile

Em alguns mapas grandes, o Editor pode deixar diversos arquivos `*.roadmap.bmp` na pasta:

```text
<MAPA>\texture\map\
```

mas falhar antes de produzir o `whole.roadmap.bmp` final.

Esses arquivos podem servir como matéria-prima para reconstrução manual, mas o NavBR funciona melhor com uma imagem global completa. Não renomeie simplesmente uma tile individual para `whole.roadmap.bmp`.

## Checklist para enviar um mapa para teste do NavBR

Antes de testar um mapa no NavBR, confirme:

- `global.cfg` existe;
- tiles `.map` existem;
- `texture\map\whole.roadmap.bmp` existe, quando o fundo visual for desejado;
- `TTData` ou `Chrono\*\TTData` existe para linhas/viagens;
- a viagem foi iniciada dentro do OMSI;
- o mapa é a mesma versão usada pelos jogadores no multiplayer.

Se a rota não for desenhada corretamente, envie também:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-route.log
```

Esse log é mais importante para problemas de traçado do que o BMP do roadmap.

## Referências técnicas consultadas

- OMSI Wiki — função `Tile > Create Roadmap` do Map Editor;
- OMSI WebDisk/Community — relatos de geração em `maps\<mapa>\texture\map\whole.roadmap.bmp` e limitações em mapas muito grandes;
- OMSI RouteAdvisor — também orienta gerar o roadmap pelo Editor antes do primeiro uso em mapas sem roadmap.
