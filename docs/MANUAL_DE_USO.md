# Manual de Uso — OMSI NavBR Multiplayer

> Manual da série **0.3.0-alpha.9**. O projeto ainda está em fase alpha; alguns recursos dependem de validação no OMSI real e podem mudar nas próximas versões.

## 1. O que é o NavBR

O **OMSI NavBR Multiplayer** é um aplicativo externo para Windows que acompanha o OMSI 2. Ele funciona de forma independente da Steam e se conecta ao processo `Omsi.exe` já em execução para ler telemetria em modo somente leitura.

Principais recursos atuais:

- GPS e minimapa/HUD sobre o OMSI;
- leitura de posição, direção, velocidade, mapa, linha, destino e próxima parada quando disponíveis;
- traçado da viagem ativa a partir dos arquivos de timetable do mapa;
- criação e entrada em salas multiplayer;
- outros jogadores no mapa/minimapa;
- chat de texto;
- voz push-to-talk;
- interface em português, inglês, espanhol, alemão e francês;
- servidor dedicado opcional.

O NavBR **não injeta código e não grava na memória do OMSI**.

---

## 2. Requisitos gerais

- Windows;
- OMSI 2, com foco inicial na versão **2.3.004**;
- OMSI em execução para usar telemetria, GPS e HUD;
- conexão de rede para multiplayer;
- microfone e saída de áudio para chat por voz.

O cliente é compilado em **x86**, compatível com a arquitetura do OMSI 2.

---

## 3. O que um mapa precisa ter para funcionar no NavBR

O NavBR não usa um banco de mapas próprio. Ele lê diretamente a estrutura instalada do mapa no OMSI. Por isso, a qualidade do GPS e do traçado depende dos arquivos existentes e da forma como o mapa foi construído.

### Obrigatório para o mapa ser detectado

A pasta do mapa precisa existir dentro de:

```text
<PASTA_DO_OMSI>\maps\<NOME_DO_MAPA>\
```

E precisa possuir:

```text
global.cfg
```

O `global.cfg` é usado para identificar a estrutura geral do mapa e a ordem dos blocos `[map]`, que é importante para converter os índices de tile usados nos timetables em coordenadas reais `GridX/GridY`.

### Tiles do mapa

Os blocos `[map]` do `global.cfg` apontam para os arquivos de tile `.map`.

Exemplo simplificado:

```text
[map]
4
-2
tile_4_-2.map
```

O NavBR usa a ordem desses blocos para resolver corretamente os índices presentes nos arquivos `.ttr`.

Para o traçado detalhado, os arquivos `.map` precisam estar presentes e referenciar corretamente as splines, objetos e cruzamentos usados naquele tile.

### Roadmap para mapa de fundo

Para exibir uma imagem de fundo do mapa, o NavBR procura arquivos como:

```text
whole.roadmap.bmp
roadmap.bmp
```

ou variantes equivalentes existentes no mapa.

Se não houver roadmap, o mapa ainda pode ser reconhecido, mas o fundo visual pode ficar indisponível ou limitado.

### Timetable / TTData para traçar a rota ativa

Para desenhar a viagem selecionada no OMSI, o mapa precisa ter os dados de timetable esperados pelo jogo, normalmente em:

```text
TTData\
```

ou, quando aplicável:

```text
Chrono\<PACOTE>\TTData\
```

O NavBR trabalha com os arquivos de viagem/track usados pelo OMSI, especialmente:

```text
.ttp
.ttr
```

O `.ttp` identifica a viagem e pode levar ao `.ttr` correspondente. O `.ttr` contém a sequência de entradas do trajeto.

Na alpha.9, cada `[track_entry]` é tratado como:

```text
ObjectId
PathId
TileIndex
```

O terceiro campo é um **índice de tile**, não `GridX` ou `GridY`. O NavBR resolve esse índice pela ordem dos blocos `[map]` do `global.cfg`.

### Splines e caminhos para geometria detalhada

Para reconstruir a rota com mais precisão, o mapa precisa manter referências válidas entre:

- tiles `.map`;
- splines `.sli`;
- objetos/crossings `.sco`;
- `[path]` e `[path_2]`;
- `ObjectId` e `PathId` presentes no timetable.

O NavBR tenta usar essa geometria para aproximar o caminho real da faixa/rua, incluindo orientação, comprimento, curvas e deslocamento lateral quando disponível.

Se algum desses vínculos não puder ser resolvido com segurança, o programa usa um **fallback por tiles** em vez de inventar um traçado.

### Para linha, destino e próxima parada

Essas informações dependem do timetable ativo e também da telemetria disponível na versão do OMSI detectada.

Para funcionar corretamente, o jogador deve:

1. carregar o mapa no OMSI;
2. selecionar uma linha/viagem válida;
3. iniciar a viagem no timetable;
4. entrar em um ônibus controlado pelo jogador;
5. manter o `Omsi.exe` em execução enquanto o NavBR lê a sessão.

### Resumo do mínimo necessário

Para **detectar o mapa**:

- `global.cfg` válido;
- tiles `.map` referenciados corretamente.

Para **mostrar um fundo visual do mapa**:

- `whole.roadmap.bmp`, `roadmap.bmp` ou equivalente.

Para **traçar a viagem ativa**:

- `TTData` ou `Chrono/*/TTData`;
- `.ttp` e `.ttr` válidos;
- índices de tile coerentes com a ordem de `[map]` no `global.cfg`.

Para **traçado detalhado pelas ruas/faixas**:

- splines `.sli`;
- objetos/crossings `.sco`;
- paths válidos;
- `ObjectId` e `PathId` resolvíveis.

### Quando o mapa não funciona corretamente

Os casos mais comuns são:

- mapa sem roadmap;
- timetable incompleto ou personalizado de forma não convencional;
- `.ttr` referenciando índices/objetos inexistentes;
- tiles ausentes;
- splines ou objetos não instalados;
- Chrono alterando `TTData` em relação ao mapa base;
- viagem não iniciada no OMSI;
- mapa construído com estruturas que ainda não foram cobertas pelo parser da alpha atual.

Nessas situações, consulte:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-route.log
```

O log registra mapa, linha, rota/track, arquivo `.ttr`, modo de resolução e quantidade de entradas/pontos encontrados.

---

## 4. Download

Na página de Releases do GitHub são publicados, conforme a versão:

- `OMSI-NavBR-Multiplayer-vX.X.X-win-x86.exe` — cliente standalone;
- `OMSI-NavBR-Multiplayer-vX.X.X-win-x86.zip` — pacote completo do cliente;
- `OMSI-NavBR-Server-vX.X.X-win-x64.zip` — servidor dedicado opcional.

Para a maioria dos jogadores, o arquivo **standalone `.exe`** é o caminho mais simples.

---

## 5. Primeira execução

1. Inicie o **OMSI 2** normalmente.
2. Carregue um mapa e entre em um ônibus.
3. Abra o **OMSI NavBR Multiplayer**.
4. O NavBR procura automaticamente um processo `Omsi.exe` em execução.
5. Quando o processo é reconhecido, o aplicativo identifica a pasta real da instalação e passa a ler os dados suportados.

O NavBR não depende do caminho padrão da Steam. A instalação é descoberta a partir do próprio processo do OMSI.

### Idioma

Na primeira execução, o aplicativo tenta usar o idioma do Windows. Se o idioma não for suportado, usa inglês.

Idiomas disponíveis:

- Português (Brasil);
- English;
- Español;
- Deutsch;
- Français.

A preferência é salva em:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\language.txt
```

---

## 6. GPS e mapa

Depois que o OMSI é detectado, o NavBR procura os mapas instalados na pasta `maps` e lê os arquivos necessários diretamente da instalação.

O GPS pode mostrar:

- posição do ônibus;
- direção do veículo;
- velocidade;
- roadmap do mapa, quando disponível;
- outros jogadores compatíveis da mesma sala;
- traçado da viagem ativa quando o timetable pode ser resolvido.

### Controles principais

- **Zoom** — aproxima ou afasta o mapa;
- **Seguir ônibus** — mantém o mapa acompanhando o veículo;
- **Ajustar** — enquadra novamente o conteúdo disponível;
- **Pan** — permite deslocar manualmente o mapa.

O zoom do minimapa/HUD pode chegar a **10×**.

### Traçado da rota

Na alpha.9 o NavBR tenta montar o caminho real da viagem usando os arquivos de timetable e a geometria do mapa.

Quando a geometria detalhada não pode ser resolvida com segurança, o programa usa um fallback por tiles. Essa parte ainda deve ser considerada **experimental** até a validação visual nos mapas reais utilizados pelos jogadores.

---

## 7. HUD / minimapa sobre o OMSI

O HUD acompanha a janela de gameplay do OMSI e pode apresentar:

- minimapa;
- linha;
- destino;
- próxima parada;
- jogadores próximos/compatíveis;
- mensagens de chat;
- quantidade de jogadores;
- estado do chat de voz;
- atalhos de multiplayer.

A alpha.9 aprende a janela real de gameplay do OMSI quando ela está em primeiro plano e tenta ocultar a sobreposição em janelas auxiliares, como menus e diálogos.

O comportamento do HUD em todas as combinações de janela, menus e fullscreen ainda requer validação no OMSI real. O uso inicial recomendado é em **modo janela ou janela sem bordas**.

---

## 8. Atalhos do HUD

Atalhos padrão:

- `F9` — abrir o chat de texto;
- `F10` — segurar para falar no chat por voz.

Os atalhos podem ser alterados na área multiplayer.

O NavBR evita oferecer `F5`, `F6`, `F7` e `F8`, por serem teclas conhecidas do OMSI. As combinações disponíveis usam `F9` e `F10`, com ou sem `Shift` e/ou `Ctrl`.

Chat e push-to-talk devem usar combinações diferentes.

### Verificação de conflito

O NavBR lê:

```text
Inputs\keyboard.cfg
```

da instalação detectada do OMSI e compara scan code e modificadores.

Se a combinação escolhida já estiver atribuída no OMSI, o atalho é bloqueado e o HUD mostra um aviso. Se o arquivo não puder ser verificado, os atalhos permanecem desativados por segurança.

---

## 9. Criar uma sala multiplayer

Na série 0.3 alpha, o **computador de quem cria a sala funciona como servidor da própria sala**.

1. Abra a área **Multiplayer**.
2. Clique em **Criar sala neste PC**.
3. O NavBR inicia o host local.
4. A porta padrão é **TCP 27730**.
5. O criador entra automaticamente na própria sala.
6. Use o botão de copiar convite.
7. Envie o convite aos outros jogadores por um canal de sua preferência.

O convite usa o formato versionado `NAVBR_INVITE_V1`.

### Jogadores na mesma rede

Em rede local, o NavBR apresenta os endereços IPv4 disponíveis do host. Normalmente os convidados devem conseguir usar o endereço da rede local do computador que criou a sala.

### Jogadores pela Internet

Para jogadores fora da mesma rede, pode ser necessário:

- permitir o NavBR no **Windows Firewall**;
- encaminhar a porta **TCP 27730** no roteador para o computador do host;
- usar o endereço de conexão apropriado da rede do host.

UPnP/NAT traversal ainda é um recurso planejado e não deve ser considerado disponível nesta alpha.

---

## 10. Entrar em uma sala

1. Abra a área **Multiplayer**.
2. Copie o convite recebido do host.
3. Cole o convite no campo correspondente do NavBR.
4. O aplicativo preenche os dados de servidor/sala suportados pelo convite.
5. Confirme a entrada.

Quando a conexão estiver ativa, a sessão pode transportar:

- presença dos jogadores;
- telemetria compartilhada;
- chat de texto;
- chat por voz.

A compatibilidade visual entre jogadores depende também do mapa/ambiente utilizado por cada participante.

---

## 11. Chat de texto

O chat é compartilhado por sala.

- limite atual de **280 caracteres** por mensagem;
- mensagens aparecem na janela multiplayer;
- mensagens também podem aparecer no HUD;
- ao fechar o campo de chat, o NavBR tenta devolver o foco ao OMSI.

Evite enviar dados pessoais, senhas, tokens ou outras informações privadas pelo chat.

---

## 12. Chat por voz

O chat por voz funciona em modo **push-to-talk**.

1. Entre em uma sala.
2. Verifique o atalho de PTT configurado.
3. Segure a tecla enquanto estiver falando.
4. Solte a tecla para interromper a transmissão.

A implementação atual usa áudio Opus e transporta a voz pelo mesmo canal SignalR/WebSocket da sessão multiplayer.

Voz, latência e comportamento entre computadores ainda precisam de validação real mais ampla antes de serem considerados estáveis.

---

## 13. Servidor dedicado

O peer-host é o modo principal da série 0.3 alpha. O pacote de servidor dedicado continua disponível como alternativa.

Use o servidor dedicado quando quiser manter a sala em outra máquina ou separar o host do computador que executa o OMSI.

O pacote publicado é:

```text
OMSI-NavBR-Server-vX.X.X-win-x64.zip
```

O servidor dedicado não é obrigatório para criar uma sala comum pelo cliente.

---

## 14. Arquivos e configurações locais

O NavBR mantém preferências e diagnósticos em:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\
```

Entre os dados locais podem estar:

- idioma;
- posição do HUD;
- zoom;
- atalhos escolhidos;
- outras configurações do cliente;
- log de diagnóstico da rota.

### Log de rota

Para investigar problemas de traçado, consulte:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-route.log
```

Esse arquivo é especialmente útil para identificar:

- mapa detectado;
- linha;
- rota/track;
- `.ttr` usado;
- modo de resolução;
- quantidade de entradas e pontos gerados.

Ao reportar um problema de rota, inclua esse log sempre que possível, revisando antes se não há informação que você não queira publicar.

---

## 15. Solução de problemas

### O OMSI não foi detectado

- confirme que `Omsi.exe` está realmente em execução;
- carregue um mapa e tente novamente;
- confirme que a versão do OMSI está dentro dos perfis atualmente suportados.

### O HUD não aparece

- deixe a janela de gameplay do OMSI em primeiro plano;
- teste em modo janela ou janela sem bordas;
- feche menus e diálogos do OMSI e retorne à condução;
- reinicie o NavBR com o OMSI já aberto, se necessário.

### O HUD aparece sobre menu ou diálogo

Esse comportamento deve ser reportado, pois a alpha.9 inclui uma nova lógica para diferenciar a janela de gameplay das janelas auxiliares do OMSI.

### A rota está errada ou só aparece por tiles

- confirme se a viagem/timetable está realmente ativa no OMSI;
- reproduza o problema;
- envie o `navbr-route.log` junto com o nome do mapa, linha e rota utilizada.

### Não consigo entrar em uma sala pela Internet

No computador do host:

- confirme que a sala continua aberta;
- verifique o Windows Firewall;
- confirme o encaminhamento da porta TCP `27730`, se necessário;
- confirme o endereço usado pelo convidado.

### O atalho de chat ou voz não funciona

- abra as configurações multiplayer;
- verifique se há conflito informado;
- escolha uma combinação diferente;
- confirme que `Inputs\keyboard.cfg` pode ser lido pelo NavBR.

---

## 16. Como reportar bugs e feedback

O projeto disponibiliza formulários de GitHub Issues para:

- bugs;
- sugestões;
- feedback geral.

Ao reportar um problema, informe sempre que possível:

- versão do NavBR;
- versão do OMSI;
- nome do mapa;
- linha/rota usada;
- passos para reproduzir;
- mensagem de erro;
- `navbr-route.log`, se o problema envolver navegação.

Não publique senhas, tokens ou dados privados.

---

## 17. Estado de validação da alpha.9

O CI valida compilação e publicação dos binários. Isso não substitui o teste dentro do OMSI real.

Na alpha.9, ainda devem ser considerados **pendentes de validação real**:

- comportamento do HUD em gameplay, menus e diálogos;
- geometria detalhada da rota;
- leitura real de próxima parada e destino em diferentes mapas/ônibus;
- atalhos em diferentes configurações de `keyboard.cfg`;
- voz entre computadores;
- multiplayer peer-host em diferentes redes.

Esses itens só devem ser considerados estáveis depois de testes reais suficientes no OMSI.
