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
- Home passa a mostrar:
  - estado do plugin OMSI 2;
  - Empresa/Crachá;
  - Mobile Companion;
  - atualizações.

## CCO / Empresa / Crachá

Quando existe crachá verificado:

- nome do motorista passa a seguir o crachá;
- empresa/sigla passam a seguir o crachá;
- campos conflitantes ficam bloqueados na UI;
- Perfil do Motorista, Empresa/Frota e Rede da Empresa permanecem coerentes.

## HUD e minimapa

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
- PWA e APK continuam no pipeline oficial.

## Voz

- canal/raio não são mais sobrescritos pelo polling durante edição;
- mixer por jogador usa estado local enquanto o slider se move;
- alteração é enviada ao backend ao soltar/confirmar;
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
