# Alpha.13 Test 1 — teste comunitário

A **Alpha.13 Test 1** inicia a fase de validação do ônibus remoto físico dentro do OMSI 2.

## Objetivo principal

Validar uma sessão real com **2 ou mais PCs**, no mesmo mapa e sala, em que cada jogador consiga ver o ônibus dos demais dentro do OMSI.

Nesta primeira etapa o foco é deliberadamente limitado:

- spawn do ônibus remoto;
- posição, rotação e velocidade;
- movimento contínuo pela telemetria da sala;
- luzes e setas quando suportadas pelo backend atual;
- remoção segura ao sair da sala/desconectar;
- sessão física continua ativa mesmo fechando a Central Multiplayer.
- marcador remoto no minimapa/mapa principal deve ter o mesmo formato do marcador local, em cor diferente;
- nome do usuário deve aparecer acima do marcador remoto;
- com ônibus físico realmente spawnado, o nome do usuário deve acompanhar a posição projetada acima do ônibus 3D quando ele estiver visível na câmera.

Ainda não fazem parte deste teste:

- portas;
- matriz/linha/destino física;
- articulação de ônibus articulado;
- animações específicas por modelo;
- sincronização completa de tráfego IA.

## Requisitos do teste físico

1. OMSI **2.3.004** em ambos os PCs.
2. NavBR Alpha.13 Test 1.
3. Plugin experimental NavBR instalado no OMSI.
4. Mesmo mapa/versão compatível.
5. O ônibus usado pelo jogador remoto precisa existir localmente no PC que irá renderizá-lo.
6. Em Multiplayer, ativar **Ônibus dos jogadores no OMSI (TESTE ALPHA)**.

A escrita física continua **opt-in e experimental**. Se houver instabilidade, desative o recurso e envie o diagnóstico técnico.

## Roteiro recomendado — 2 PCs

### PC A

1. Abra NavBR e OMSI.
2. Carregue mapa e ônibus.
3. Crie a sala ou inicie o peer-host.
4. Ative o teste de ônibus remoto.
5. Comece a dirigir lentamente.

### PC B

1. Abra NavBR e OMSI.
2. Carregue o mesmo mapa compatível.
3. Entre na sala do PC A.
4. Ative o teste de ônibus remoto.
5. Confirme se o ônibus do PC A aparece no mundo 3D.

Depois inverta: movimente o PC B e confirme no PC A.

## O que observar

Registre principalmente:

- se o ônibus nasce ou não;
- posição correta ou deslocada;
- rotação/orientação correta;
- movimento suave ou pulando;
- desaparecimento inesperado;
- duplicação de ônibus;
- queda ou travamento do OMSI;
- comportamento ao jogador sair/reconectar;
- luzes/setas visíveis;
- modelo do ônibus, mapa e HOF usados.

## Mudanças desta build

- coordenador físico único no serviço multiplayer, evitando spawn/update duplicados;
- fluxo online já encaminha telemetria remota diretamente ao backend físico;
- telemetria 2.3.004 envia pose nativa real (`LocalX/Y/Z` + quaternion);
- limite de segurança de veículos remotos mantido;
- limpeza em desconexão/reconexão preservada;
- fechamento da janela Multiplayer não encerra mais os ônibus enquanto a sessão continua ativa;
- base de entitlement/licenciamento adicionada separadamente do núcleo multiplayer; Alpha/Beta permanecem abertas.

## Próximas etapas após o teste

A ordem prevista é:

1. corrigir spawn/movimento/estabilidade encontrados no teste real;
2. portas por perfis compatíveis de ônibus;
3. matriz/HOF/linha/destino;
4. articulação;
5. LOD/culling e otimizações;
6. ampliar estados visuais e compatibilidade por modelo.

## Privacidade e segurança

A telemetria normal permanece read-only. A escrita no OMSI só acontece no plugin experimental e exige ativação explícita. O NavBR não redistribui ônibus, mapas, HOFs ou outros conteúdos proprietários.
