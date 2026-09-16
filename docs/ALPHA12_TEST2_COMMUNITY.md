# Alpha.12 Test 2 — checklist de teste da comunidade

A **Alpha.12 Test 2** corrige o pacote interno do plugin OMSI da Test 1 e traz a primeira rodada ampla da nova interface operacional do NavBR.

## Correção prioritária desta build

Na Test 1, o instalador interno esperava três arquivos do plugin, mas o empacotamento embutido continha apenas dois. Isso causava a mensagem:

`Pacote interno do plugin incompleto: NavBR.OmsiInterop.dll`

Na Test 2, o bundle do plugin contém obrigatoriamente:

- `NavBR.OmsiPlugin.dll`;
- `NavBR.OmsiInterop.dll`;
- `NavBR.OmsiPlugin.opl`.

O script de build agora abre o ZIP gerado e falha automaticamente caso qualquer um desses arquivos esteja ausente.

## O que mudou visualmente

- shell Alpha.12 em formato de central operacional;
- barra lateral reorganizada para uso diário;
- Home com resumo da operação atual;
- Navegação com painel lateral de linha, rota, destino, próxima parada, rua, velocidade e atraso;
- Central Multiplayer remodelada, com sessão, jogadores, chat e configurações técnicas em segundo plano;
- CCO / Operação com visão de serviço e frota multiplayer;
- Perfil do motorista em formato de carreira/estatísticas;
- Empresa e frota em formato de painel operacional;
- Configurações organizadas por categorias;
- ferramentas avançadas preservadas, mas fora do fluxo principal.

## Teste 1 — instalação/atualização do plugin

1. Feche o OMSI completamente.
2. Abra o NavBR Alpha.12 Test 2.
3. Ative o modo avançado e abra **Diagnóstico técnico**.
4. Clique em **Instalar / atualizar plugin**.
5. Confirme que não aparece o erro de pacote incompleto.
6. Verifique na pasta `OMSI 2\plugins` a presença dos três arquivos:
   - `NavBR.OmsiPlugin.dll`;
   - `NavBR.OmsiInterop.dll`;
   - `NavBR.OmsiPlugin.opl`.
7. Abra o OMSI e confirme se o bridge/telemetria volta a conectar normalmente.

## Teste 2 — interface principal

- confirme que a barra lateral possui rolagem quando necessário;
- teste Início, Navegação, Multiplayer, CCO, Perfil, Empresa/Frota, Hardware, Configurações e Diagnóstico;
- redimensione a janela e confirme que não há controles cortados;
- altere o idioma e confirme que a navegação continua funcional;
- ative/desative o modo avançado e confirme que as ferramentas técnicas continuam acessíveis.

## Teste 3 — navegação e HUD

Com um mapa e ônibus carregados no OMSI, confira:

- mapa/rota existente continuam funcionando;
- linha e rota;
- destino;
- próxima parada;
- rua atual;
- velocidade;
- atraso;
- HUD aparece durante o gameplay e se comporta corretamente ao abrir menus/diálogos do OMSI.

A Alpha.12 não inventa ETA, distância ou coordenadas quando esses dados não estão disponíveis na telemetria.

## Teste 4 — Multiplayer Central

Comece preferencialmente com dois PCs na mesma rede:

- criar sala;
- entrar por convite/endereço;
- presença de jogadores;
- chat;
- voz/PTT;
- canais de voz;
- ping/jitter/perda;
- sala privada com senha;
- UPnP somente se você optar por ativá-lo.

Depois, teste acesso pela Internet se sua rede permitir. NAT traversal/relay avançado ainda está em desenvolvimento.

## Teste 5 — CCO, Perfil e Empresa

- CCO mostra a operação local ativa;
- motoristas remotos aparecem quando há sessão multiplayer e telemetria;
- painel de CCO usa visão esquemática enquanto não houver coordenadas suficientes para mapa remoto real;
- Perfil acumula horas, distância e viagens;
- Empresa mantém nome, sigla, mapa-base e frota;
- cadastro do ônibus atual continua funcionando.

## Informações úteis em um relato de erro

Inclua, se possível:

- versão exibida pelo NavBR;
- versão do OMSI;
- mapa e ônibus usados;
- se o problema ocorre com OMSI aberto ou fechado;
- captura da tela;
- conteúdo relevante de `%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-error.log` ou `navbr-plugin.log`;
- se estava usando cliente standalone EXE ou ZIP.

## Status

Esta é uma pré-release de teste. Recursos marcados como **Experimental** ou **Em desenvolvimento** continuam sujeitos a mudanças durante a Alpha.12.
