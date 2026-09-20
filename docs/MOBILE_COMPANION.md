# NavBR Mobile Companion — Alpha 1

## Estado

**Em desenvolvimento ativo na branch `feature/mobile-companion-alpha1`.**

Arquitetura:

`Smartphone/PWA -> NavBR Client no PC -> C# authority -> Plugin Bridge/OMSI`

O celular nunca acessa memória do OMSI diretamente.

## Alpha 1

A primeira versão funcional inclui:

- PWA responsiva servida pelo próprio NavBR no PC;
- acesso pela LAN na porta TCP **27731**;
- código de pareamento novo a cada abertura do NavBR;
- bloqueio de clientes fora da rede local/loopback;
- atualização periódica de estado real;
- aba **GPS** com posição, rota real, retorno à rota e próximas paradas quando disponíveis;
- aba **IBIS** separada do GPS;
- IBIS exibindo linha, rota/curso, destino, HOF, próxima parada e atraso reais;
- aba **Status** com estado do OMSI, Plugin Bridge e telemetria;
- card no desktop com endereços LAN e código de pareamento.

## Segurança

A API mobile exige o código de pareamento gerado nesta execução. O host rejeita endereços remotos que não sejam loopback ou rede privada local.

## IBIS

O IBIS permanece separado do GPS.

Nesta Alpha 1 ele é **somente leitura real** porque o Plugin Bridge ainda não possui uma capacidade nativa segura para escrever linha/rota/destino/HOF no ônibus do jogador.

Nenhum comando fake é enviado. A UI indica leitura até existir capacidade explícita no bridge.

Próxima etapa do IBIS:

1. definir comandos do Plugin Bridge para operação local;
2. validar suporte por veículo/mapa/HOF;
3. listar somente linhas/rotas/destinos reais;
4. aplicar no thread correto do OMSI;
5. confirmar resultado antes de refletir a alteração no celular.

## Como testar

1. abra o NavBR no PC;
2. em **Configurações > Instalações**, localize **Mobile Companion**;
3. conecte o celular à mesma rede Wi-Fi/LAN;
4. abra um dos endereços mostrados, por exemplo `http://192.168.0.10:27731`;
5. informe o código de pareamento;
6. com o OMSI aberto e o ônibus carregado, teste GPS, IBIS e Status.

## Portas

- multiplayer local: 27730;
- Mobile Companion: **27731**;
- Company Node: 27740.

## Sem mocks em produção

Quando OMSI, rota, HOF ou Plugin Bridge não fornecerem um dado real, a PWA mostra indisponível/aguardando. Não são geradas linhas, rotas, destinos, paradas ou posições artificiais.
