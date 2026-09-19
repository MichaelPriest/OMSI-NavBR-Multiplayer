# Salas peer-host

O peer-host continua disponível na série 0.3.0-alpha, mas é apenas uma das três opções de multiplayer. O **Servidor NavBR oficial**, **LAN** e **Online através do Host** são modos distintos.

## Criar sala

1. Abra a Central Multiplayer.
2. Informe apelido e sala.
3. Clique em Criar sala.
4. O NavBR sobe o host local em TCP 27730.
5. Compartilhe endereço alcançável e nome da sala.

## Entrar

Informe o servidor, por exemplo:

    http://192.168.1.50:27730

Depois use a mesma sala e, se privada, informe a senha.

## Firewall

Use a ação de Firewall do NavBR para criar a regra TCP 27730 em todos os perfis de rede. Aceite o UAC e confira o estado confirmado no app.

## Internet

No modo **Online através do Host**, dependendo da rede podem ser necessários UPnP ou port forwarding. CGNAT pode impedir host direto. Nessa situação, o jogador pode optar pelo Servidor NavBR oficial, que não exige conexão de entrada no PC.

Ao criar uma sala, a Central Multiplayer mostra o alcance real conhecido pelo cliente:
- **Somente LAN**: o host está ativo e acessível na rede local, sem endereço externo confirmado;
- **Internet via UPnP**: o roteador aceitou o mapeamento e forneceu endereço externo;
- **UPnP ativo · não verificado externamente**: o mapeamento foi criado, mas não há confirmação por um serviço externo.

O teste externo é opcional. Se a instalação informar que o serviço não está configurado, isso **não significa que a sala falhou**; apenas significa que o NavBR não consegue testar a porta a partir de fora da rede.

## Estado físico

A Alpha.14 inclui multiplayer físico experimental de ônibus via plugin v3. O recurso é opt-in, exige compatibilidade local e ainda precisa de validação em modelos/mapas reais.

## Simulador

O simulador pode entrar na mesma sala do host, herdar mapa, posição e operação ativa e criar bots próximos para testar a Central e o mapa sem múltiplas instâncias do OMSI.


## Modos de criação de sala

A Central Multiplayer mantém **três modos**:

1. **Servidor NavBR oficial (gratuito/limitado)** — usa por padrão `https://omsi-navbr-multiplayer-server.onrender.com`. O servidor roda no Render e o PC do jogador é cliente. A infraestrutura gratuita atual é voltada à Alpha/testes e pode atingir limites de capacidade.
2. **LAN** — o PC do usuário executa o `NavBR.Server` apenas para a rede local.
3. **Online através do Host** — o PC do usuário executa o `NavBR.Server` e tenta disponibilizar TCP 27730 pela Internet, usando UPnP quando possível.

No futuro o projeto poderá oferecer assinatura oficial para ampliar capacidade/estabilidade do Servidor NavBR, mas ainda não há plano, preço ou data definidos.

## Verificação do plugin ao iniciar

Ao abrir o NavBR, o cliente verifica a instalação real do plugin OMSI. Ele confere os três arquivos esperados (`NavBR.OmsiPlugin.dll`, `NavBR.OmsiInterop.dll` e `NavBR.OmsiPlugin.opl`) e o manifesto de versão. Quando os arquivos estão ausentes/desatualizados e a instalação é segura, o bootstrap tenta instalar/atualizar automaticamente. Se o OMSI estiver aberto, houver conflito ou arquivo não rastreado, nenhuma sobrescrita insegura é feita e a ação manual continua disponível.
