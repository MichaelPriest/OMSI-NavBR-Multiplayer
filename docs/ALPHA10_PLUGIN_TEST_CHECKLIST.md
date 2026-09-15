# Alpha.10 — checklist de teste do plugin OMSI experimental

> Este checklist cobre **somente o plugin experimental e o bridge local da v0.3.0-alpha.10 em desenvolvimento**. A última release publicada continua sendo a v0.3.0-alpha.9.

O plugin é **opcional**. GPS, HUD, criação/entrada em salas, chat e voz continuam funcionando sem instalar o plugin.

## Objetivo desta rodada

Validar com segurança que:

1. o OMSI 2.3.004 carrega a DLL x86;
2. o plugin executa dentro do processo correto do `Omsi.exe`;
3. os callbacks do OMSI continuam ativos;
4. o Named Pipe local conecta plugin e cliente NavBR;
5. o cliente recebe heartbeat/status de volta do plugin;
6. nenhuma variável ou trigger do OMSI é alterada;
7. não há crash ou queda perceptível de desempenho.

**Esta rodada ainda não cria nem movimenta ônibus remotos dentro do OMSI.**

## Antes do teste

- Feche o OMSI.
- Feche outras cópias do NavBR.
- Preserve um backup da pasta `OMSI\plugins` por precaução.
- O protótipo atual do plugin é framework-dependent e requer **.NET 10 Runtime x86** disponível para o processo 32-bit do OMSI.
- Use o artefato completo do plugin experimental; não copie somente a DLL.

## Instalação do plugin

O pacote experimental contém:

```text
Install-NavBROmsiPlugin.ps1
Remove-NavBROmsiPlugin.ps1
NavBR.OmsiPlugin.dll
NavBR.OmsiPlugin.opl
...arquivos .NET necessários ao protótipo
```

Com o OMSI fechado, execute:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Install-NavBROmsiPlugin.ps1 -OmsiRoot "G:\Games\OMSI 2 Steam Edition"
```

Ajuste o caminho para a instalação real.

O instalador:

- exige que `Omsi.exe` exista no diretório informado;
- recusa instalação com o OMSI aberto;
- mantém um manifesto dos arquivos copiados;
- não sobrescreve arquivos de mesmo nome que não pertençam a uma instalação NavBR rastreada.

## Ordem recomendada do teste

1. Instale o plugin com o OMSI fechado.
2. Abra o cliente NavBR alpha.10.
3. Abra o OMSI 2.3.004 normalmente.
4. Carregue um mapa e um ônibus.
5. Aguarde pelo menos 10 segundos.
6. Observe o painel técnico **`PLUGIN BRIDGE v1 • EXP`** na janela principal do NavBR.
7. Confira o arquivo de log do plugin.

## Valores esperados no painel

Quando o plugin estiver carregado corretamente:

```text
status=CONNECTED
process-match=YES
heartbeat=LIVE
```

Também devem aparecer:

- `plugin-pid=<PID>` — PID informado pela DLL;
- `version=<versão>` — versão do assembly experimental;
- `callbacks=<número>` — deve aumentar durante a sessão;
- `system-var=<índice>` — callback de system variable usado pelo `.opl`;
- `remote=<n>` — estados remotos recebidos;
- `compatible=<n>` — estados remotos compatíveis com o mapa local;
- `map=<mapa atual>`;
- `compatibility=<fingerprint resumido>`.

### Interpretação

`status=WAITING`
: o NavBR está aberto, mas nenhum plugin completou o handshake local.

`status=CONNECTED` + `heartbeat=NONE`
: o Named Pipe conectou, mas ainda não houve heartbeat de callback do OMSI.

`heartbeat=LIVE`
: o cliente recebeu recentemente um `plugin-status` originado dos callbacks do OMSI.

`heartbeat=STALE`
: o pipe ainda parece conectado, porém o último heartbeat está antigo. Registrar esse caso como falha para investigação.

`process-match=YES`
: o PID do plugin é o mesmo `Omsi.exe` detectado pelo NavBR. Este é o resultado esperado.

`process-match=NO`
: **não avançar o teste**. Registrar os PIDs e remover o plugin antes de nova investigação.

## Log esperado

Arquivo:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-plugin.log
```

Um teste básico saudável deve conter eventos equivalentes a:

```text
PluginStart ...
bridge conectado ...
heartbeat ... callbacks=... remoteCount=... compatibleRemoteCount=...
...
PluginFinalize ...
```

## Teste com dois computadores

Somente depois do teste local acima passar nos dois PCs:

1. use o mesmo mapa/versão nos dois computadores;
2. crie uma sala no PC A;
3. entre na sala pelo PC B;
4. confirme telemetria normal no HUD/mapa;
5. confira `remote` no painel do plugin;
6. com fingerprints iguais, confira `compatible`;
7. troque intencionalmente um dos PCs para mapa/versão incompatível e confirme que o estado deixa de contar como compatível;
8. saia da sala e confirme que os estados remotos desaparecem após remoção/timeout.

Mesmo neste teste, **nenhum ônibus remoto físico deverá aparecer dentro do OMSI ainda**.

## Roadmaps dos mapas

Na alpha.10, a área GPS mostra duas listas completas:

- `roadmap pronto` — mapa com roadmap global utilizável;
- `roadmap ausente` — precisa gerar/fornecer o roadmap global.

Roadmaps individuais de tiles não contam como mapa pronto.

Guia de geração:

`docs/GERAR_ROADMAP_MAPAS.md`

## Como remover o plugin

Feche o OMSI e execute, a partir do pacote experimental:

```powershell
.\Remove-NavBROmsiPlugin.ps1 -OmsiRoot "G:\Games\OMSI 2 Steam Edition"
```

O removedor usa o manifesto criado na instalação e remove somente os arquivos rastreados pelo NavBR.

## O que registrar se houver problema

Informe:

- versão do NavBR;
- versão do OMSI;
- mapa carregado;
- valores exibidos em `PLUGIN BRIDGE v1 • EXP`;
- conteúdo relevante de `navbr-plugin.log`;
- conteúdo relevante de `navbr-error.log`, se existir;
- se houve crash, travamento ou queda perceptível de FPS;
- se o problema ocorreu antes ou depois de entrar em uma sala multiplayer.

## Critério para avançar para o primeiro ônibus remoto

Só iniciar a etapa de entidade física quando houver confirmação real de:

- `status=CONNECTED`;
- `process-match=YES`;
- heartbeat contínuo e estável;
- instalação/remoção funcionando;
- bridge estável com dois PCs;
- filtro de compatibilidade de mapa funcionando;
- nenhum crash/regressão relevante no OMSI.
