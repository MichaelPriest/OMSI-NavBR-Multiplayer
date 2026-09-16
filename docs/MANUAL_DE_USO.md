# Manual de Uso — OMSI NavBR Multiplayer

> Manual atualizado para **v0.3.0-alpha.11-test.2**.

Este manual foi escrito para quem quer usar o NavBR sem precisar entender programação, arquivos internos do OMSI ou conceitos de rede.

O aplicativo também possui um botão **Manual de uso** na barra lateral. Esse guia interno funciona offline.

## 1. Antes de começar

Recomendado:

- Windows;
- OMSI 2 **2.3.004**;
- Alpha.11 Test 2 do NavBR;
- conexão de internet ou rede local para multiplayer;
- microfone e saída de áudio se quiser usar voz.

Para baixar a Test 2:

https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.11-test.2

Para a maioria das pessoas, baixe o arquivo:

```text
OMSI-NavBR-Multiplayer-v0.3.0-alpha.11-test.2-win-x86.exe
```

## 2. Primeira abertura

1. Abra o NavBR.
2. Abra o OMSI 2.
3. Aguarde o NavBR detectar `Omsi.exe`.
4. Quando aparecer que a telemetria está conectada, carregue um mapa no OMSI.
5. Escolha o ônibus e inicie a partida normalmente.
6. O HUD/minimapa deve aparecer automaticamente durante o gameplay.

Ao abrir opções, timetable, menus ou janelas auxiliares do OMSI, o HUD deve desaparecer e voltar quando o gameplay ficar ativo novamente.

## 3. HUD e velocidade

O HUD foi reduzido e ficou mais transparente na Alpha.11.

Ele pode mostrar:

- velocidade;
- linha;
- destino;
- próxima parada;
- minimapa;
- rota;
- outros jogadores;
- chat;
- status de voz e conexão.

A velocidade é lida do OMSI usando `Groundspeed` e, quando necessário, a velocidade linear real como fallback.

Se a velocidade ficar em `0 km/h` com o ônibus andando:

1. confirme que está usando a Test 2 ou mais recente;
2. feche e abra o NavBR;
3. confirme que o OMSI foi detectado como 2.3.004;
4. reporte o modelo do ônibus usado se o problema continuar.

## 4. Paradas e rota ativa

Na Alpha.11 a regra é simples:

### Quando existe rota/viagem ativa

O minimapa deve mostrar **somente as paradas daquela rota**.

A próxima parada continua destacada.

### Quando não existe rota ativa

O NavBR pode mostrar todas as paradas válidas encontradas no mapa.

Se aparecerem paradas de outras linhas durante uma viagem ativa, informe:

- mapa;
- linha;
- rota/track;
- sentido;
- terminal ou trecho onde ocorreu.

## 5. GPS e mapas

O NavBR usa os arquivos instalados no próprio OMSI.

Ele pode ler:

- `global.cfg`;
- arquivos `.map`;
- `TTData`;
- `.ttp` e `.ttr`;
- splines `.sli`;
- objetos/crossings `.sco`;
- roadmaps como `whole.roadmap.bmp`.

Se um mapa não tiver roadmap global, ele ainda pode ser detectado, mas o fundo visual do GPS pode ficar limitado.

## 6. Criar uma sala multiplayer

1. Clique em **Multiplayer**.
2. Informe seu apelido.
3. Escolha **Criar sala neste PC**.
4. Informe ou confirme o nome da sala.
5. O seu computador passa a hospedar a sessão.

Porta padrão:

```text
TCP 27730
```

Na mesma rede local, os convidados normalmente usam o IPv4 do computador host.

Pela internet, pode ser necessário liberar a porta `27730` no Firewall do Windows e no roteador.

A meta inicial da Alpha.11 é **até 32 jogadores por sala**.

## 7. Entrar em uma sala

1. Abra **Multiplayer**.
2. Informe seu apelido.
3. Digite o endereço do host ou use o convite fornecido pelo criador.
4. Use o mesmo nome da sala.
5. Clique para conectar.

Se não conectar:

- confirme o endereço;
- confirme o nome da sala;
- confirme que o host ainda está com a sala aberta;
- confira Firewall/roteador;
- teste primeiro em rede local se possível.

## 8. Chat e voz

Atalhos padrão:

- `F9` — abrir chat de texto;
- `F10` — segurar para falar.

Para voz:

1. ative o chat por voz na janela Multiplayer;
2. mantenha `F10` pressionado enquanto fala;
3. confirme que o microfone correto está ativo no Windows.

Se a voz não funcionar, teste outro dispositivo de entrada/saída do Windows e confirme se o PTT está habilitado.

## 9. Instalar o plugin experimental

O plugin é necessário para os recursos de integração profunda com o OMSI, incluindo o teste do ônibus remoto 3D.

Com o OMSI fechado:

1. abra o NavBR;
2. vá ao painel de plugin/diagnóstico;
3. clique em **Instalar / atualizar plugin**;
4. confirme a pasta do OMSI se solicitado;
5. abra novamente o OMSI.

Arquivos principais:

```text
NavBR.OmsiPlugin.dll
NavBR.OmsiPlugin.opl
NavBR.OmsiInterop.dll
```

## 10. Ônibus remoto físico 3D — EXPERIMENTAL

Esse recurso ainda é de teste e vem **desligado por padrão**.

Para testar:

1. os jogadores devem usar OMSI 2.3.004;
2. o plugin precisa estar atualizado;
3. todos devem estar no mesmo mapa/build compatível;
4. cada computador precisa ter o ônibus usado pelo outro jogador instalado localmente;
5. o caminho relativo dentro de `Vehicles\` precisa ser compatível;
6. marque **Ônibus remoto 3D (EXPERIMENTAL)** na janela Multiplayer;
7. comece com poucos jogadores.

O NavBR não transfere ônibus pagos ou proprietários.

### O que observar

- ônibus aparece ou não;
- posição correta;
- rotação correta;
- movimento suave;
- faróis;
- luz de freio;
- setas;
- remoção ao desconectar;
- queda de FPS;
- congelamento ou crash.

Se ocorrer crash, desative o 3D e reinicie o OMSI.

## 11. Diagnósticos automáticos

A opção **Enviar diagnósticos automáticos do teste** é opcional e vem desligada por padrão.

Quando ativada, pode enviar informações técnicas como:

- versão do NavBR;
- versão/contexto do OMSI;
- mapa;
- identificação técnica do ônibus;
- estado do plugin/bridge;
- estado do 3D experimental;
- mensagens de erro.

Não envia:

- conteúdo do chat;
- áudio ou voz;
- senha;
- token;
- arquivos pessoais;
- nome do usuário do Windows.

Se estiver offline, os eventos podem ficar temporariamente em uma fila local limitada para envio posterior.

## 12. Se o HUD não aparecer

Confira:

1. OMSI está aberto;
2. o mapa já foi carregado;
3. você está realmente no gameplay e não em menu/opções;
4. o NavBR detectou o processo correto;
5. nenhuma versão antiga do NavBR está aberta ao mesmo tempo.

A Alpha.11 aprende a janela de gameplay real do OMSI usando a janela em primeiro plano do próprio `Omsi.exe`, em vez de depender rigidamente de `Process.MainWindowHandle`.

## 13. Se o mapa não aparecer

Confirme se o mapa possui:

```text
global.cfg
```

Para fundo de roadmap, procure por algo como:

```text
texture\map\whole.roadmap.bmp
```

Se não existir, consulte:

[GERAR_ROADMAP_MAPAS.md](GERAR_ROADMAP_MAPAS.md)

## 14. Como reportar um problema

Informe sempre que possível:

- versão do NavBR;
- versão do OMSI;
- mapa;
- linha/rota;
- ônibus;
- se o plugin estava instalado;
- se o 3D experimental estava ligado;
- o que você esperava;
- o que aconteceu;
- se houve crash ou queda de FPS.

Checklist específico da Test 2:

[ALPHA11_TEST2_COMMUNITY.md](ALPHA11_TEST2_COMMUNITY.md)

## 15. Créditos

**Desenvolvedor:** MichaelPriest  
**Com apoio da IA:** ChatGPT

## 16. Observação sobre estabilidade

A Alpha.11 Test 2 é uma prerelease comunitária. HUD, multiplayer e integração física ainda podem receber ajustes antes da Alpha.11 geral.
