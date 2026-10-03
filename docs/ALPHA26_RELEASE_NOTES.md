# OMSI NavBR Multiplayer — Alpha.26

## Foco desta versão

A Alpha.26 concentra melhorias do aplicativo, HUD, CCO/Empresa, Mobile Companion,
Hardware Cockpit, Voz, diagnóstico e atualização automática.

O multiplayer físico real entre dois jogadores foi propositalmente deixado para a
etapa final de validação, porque ainda depende de teste real com dois PCs/sessões OMSI.

## Atualizador automático

- verificação automática de releases oficiais ao abrir o launcher;
- suporte correto a versões prerelease como Alpha.25 -> Alpha.26;
- download do instalador oficial;
- validação obrigatória contra `SHA256SUMS.txt`;
- nova aba **Configurações > Atualizações**;
- progresso de download/verificação;
- ação **Atualizar e reiniciar**;
- instalação silenciosa via instalador Inno Setup validado;
- retomada de download interrompido via HTTP Range;
- modo offline não bloqueante: falha de rede não impede o NavBR de abrir;
- confirmação pós-update ao reabrir, mostrando versão anterior → nova;
- limpeza automática de instaladores antigos, mantendo no máximo duas versões preparadas;
- o plugin OMSI 2 continua gerenciado pelo NavBR;
- o plugin openOMSI **não** participa do atualizador do App.

## openOMSI separado do NavBR App

O plugin NavBR para openOMSI agora é tratado como pacote independente:

- não é mais embutido no executável do NavBR App;
- o App não instala, atualiza, remove ou inicia openOMSI;
- o App mantém somente o gateway local de comunicação;
- detecção de runtime/content roots é somente leitura;
- o plugin openOMSI possui workflow dedicado e artefato x64 próprio.

## Interface React

- correções de campos que eram sobrescritos pelo polling;
- Hardware Cockpit não perde seleção enquanto o usuário edita;
- Perfis OMSI não reescrevem nome/argumentos durante edição;
- controles de Voz não disparam dezenas de comandos durante o movimento do slider;
- erros de comandos permanecem visíveis em toast por tempo suficiente;
- loading inicial estável;
- servidor personalizado e atalhos de chat/PTT preservam o draft local durante polling;
- manual React da Alpha.26 reorganizado, sem seções duplicadas e com fluxos atuais de Empresa/Crachá, updater, openOMSI, Mobile, Hardware e diagnóstico;
- Home passa a mostrar:
  - estado do plugin OMSI 2;
  - Empresa/Crachá;
  - Mobile Companion;
  - atualizações;
- criação/entrada em salas agora valida sala, apelido e senha privada mínima antes de enviar comandos;
- navegador de salas públicas mostra estado real de atualização e evita refresh duplicado;
- URL do Company Node usa draft/ack e não fica presa em vínculo antigo;
- preferências do updater ficam bloqueadas durante check/download/verificação/instalação para evitar comandos concorrentes.

## CCO / Empresa / Crachá

Quando existe crachá verificado:

- nome do motorista passa a seguir o crachá;
- empresa/sigla passam a seguir o crachá;
- campos conflitantes ficam bloqueados na UI;
- Perfil do Motorista, Empresa/Frota e Rede da Empresa permanecem coerentes;
- importação de perfil pode trazer histórico/estatísticas, mas não substitui nome/empresa de um crachá verificado.

## HUD e minimapa

- layout padrão limpo para evitar sobreposição entre o HUD principal e o painel modular;
- minimapa integrado agora espelha a viewport GPS completa: zoom por velocidade, rotação pelo heading, rota/rejoin e marcador local;
- Configurações > HUD expõe o zoom-base do GPS; o ajuste automático por velocidade continua ativo sobre esse valor;
- minimapa circular dos HUDs modulares e compostos usa máscara elíptica real;
- no modo circular, o cartão inteiro do GPS vira circular (não apenas a imagem dentro de um painel retangular);
- o recorte circular é aplicado após o sizing do preset, evitando voltar para formato quadrado/retangular;
- roda do mouse sobre o minimapa modular, em modo de edição, ajusta o zoom real do GPS;
- prévia React aplica imediatamente o zoom/forma aos HUDs modulares;
- zoom do GPS varia continuamente com a velocidade e o heading do mapa é suavizado;
- manobras e reentrada de rota aparecem dentro da própria viewport do GPS, inclusive nos HUDs novos;
- veículos IA locais do OMSI passam a aparecer no GPS como setas orientadas, lidas diretamente da memória do simulador;
- marcadores de IA que deixam o snapshot são removidos do Canvas para evitar fantasmas/acúmulo;
- minimapa integrado oculta visualmente o minimapa legado, mantendo-o apenas como fonte invisível do VisualBrush;
- Alertas modulares ficam totalmente ocultos durante operação normal e só surgem quando existe alerta real;
- Resetar HUD volta para o layout limpo v4 da Alpha.26;
- painel modular padrão migra para o canto inferior direito quando ainda usa a posição antiga intocada;
- minimapa, multiplayer e indicadores laterais duplicados deixam de vir ativados dentro do painel modular padrão;
- layouts personalizados pelo usuário são preservados;
- dicas/legendas de atalhos de teclado foram removidas do overlay do OMSI;
- chips fixos CHAT/PTT foram removidos da barra superior;
- erros de voz no HUD agora são temporários e somem automaticamente;
- TeleMatrix passa a ser opt-in no layout padrão; personalizações existentes são preservadas;
- TeleMatrix agora pode ser ligada/desligada e ter tema/tamanho ajustados em Configurações > HUD > Módulos no React;
- prévia do HUD também pré-visualiza TeleMatrix e restaura o estado salvo ao ser fechada;
- painel de viagem vazio fica oculto até existir linha/rota/destino/parada;
- minimapa sem roadmap/layout real fica oculto até os dados estarem disponíveis;
- textos fixos de instrução do minimapa foram removidos e o zoom só aparece durante a edição do HUD;
- HUD principal agora reduz escala automaticamente em resoluções menores para evitar sobreposição;
- chat in-game usa largura máxima responsiva e margens adaptativas;
- minimapa **Retangular**;
- minimapa **Circular · GTA**;
- escolha persistida no perfil do HUD;
- formato aplicado ao overlay real dentro do OMSI;
- formato também aplicado aos HUDs compostos;
- rota, heading, posição e paradas continuam usando dados reais do OMSI.

## Mobile Companion

- roadmap real do OMSI ligado ao GPS mobile;
- rota, paradas e ônibus renderizados sobre o roadmap;
- retomada imediata ao voltar de bloqueio/suspensão;
- polling suspenso quando a página não está visível;
- PTT é liberado ao suspender o aplicativo;
- `voice-ptt=false` é enviado com `fetch keepalive` em ocultação/pagehide para reduzir risco de transmissão presa;
- linha manual TP/TS não é mais sobrescrita pelo polling enquanto o usuário edita;
- PWA e APK continuam no pipeline oficial.

## Chat e Voz

- chat in-game fica oculto por padrão e não abre automaticamente ao receber mensagens;
- tela React de Chat & Voz ficou mais compacta, com estado de conexão/canal e auto-scroll controlado;
- mensagens continuam armazenadas e aparecem quando o motorista ativa o chat;
- ao fechar com Enter/Esc, painel e campo de digitação somem imediatamente;
- chat foi destacado do `HudDock` para não deslocar/reorganizar os demais HUDs;
- preset Immersive Operation também esconde mensagens enquanto o chat não estiver ativo;
- canal/raio não são mais sobrescritos pelo polling durante edição;
- mixer por jogador usa estado local enquanto o slider se move;
- alteração é enviada ao backend ao soltar/confirmar;
- raio de proximidade também confirma alterações feitas pelo teclado;
- redução de churn visual e de comandos.

## RP

- encerramento seguro se o mapa/sessão OMSI terminar;
- encerramento seguro se o Plugin Bridge desconectar;
- personagem é restaurado em vez de permanecer preso fora do ônibus;
- mensagens de estado mais claras na React.

## Hardware Cockpit

- estado de reconexão automática exposto na UI;
- mostra se a COM configurada está disponível;
- mostra tentativa atual de backoff;
- mostra horário da próxima tentativa;
- NavBR não troca silenciosamente para outra porta COM.

## Diagnóstico e desempenho

Novo pacote de diagnóstico sanitizado:

- resumo de saúde da sessão;
- versão do NavBR;
- estado do plugin OMSI;
- estado do updater;
- estado do gateway openOMSI;
- Hardware Cockpit;
- log sanitizado;
- sem senhas;
- sem tokens;
- sem IDs de sala/jogador;
- sem IPs;
- sem e-mails;
- sem caminhos locais do usuário.

O painel de desempenho mostra:

- poll efetivo de telemetria;
- duração da última leitura;
- média de leitura;
- refresh do HUD;
- pressão do plugin;
- perfil de desempenho ativo.

## Validação

A versão deve ser promovida somente após:

- React desktop build;
- Mobile PWA build;
- Android APK build;
- OMSI x86 native interop;
- Plugin Bridge smoke;
- openOMSI LAN/gateway smokes;
- plugin openOMSI x64 standalone;
- instalador Windows;
- smoke install/uninstall;
- atualização automática em instalação real entre duas versões.

## Multiplayer físico

Permanece experimental e será a última etapa de validação real:

- dois PCs;
- duas sessões OMSI;
- ônibus remoto físico visível em movimento;
- Kachel/grid/tile;
- 20 Hz;
- pause/unpause;
- mudança de tile;
- HOF mismatch não bloqueante;
- diagnóstico completo quando não desenhar.
