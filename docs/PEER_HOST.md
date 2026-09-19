# Salas peer-host

Na série 0.3.0-alpha, o modo padrão usa o **PC de quem cria a sala como servidor**.

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

Dependendo da rede podem ser necessários UPnP, port forwarding ou relay. CGNAT pode impedir host direto.

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

A Central Multiplayer mantém os dois modos:

1. **Hospedar no meu PC (LAN + Internet)** — o próprio computador do usuário executa o `NavBR.Server`, publica os endereços LAN e tenta mapear TCP 27730 via UPnP para gerar um endereço externo. Se o roteador não permitir, a sala continua disponível em LAN.
2. **Servidor Online NavBR** — usa por padrão `https://omsi-navbr-multiplayer-server.onrender.com` para criar/entrar em salas sem transformar o PC do usuário em servidor público.

O modo online não remove nem substitui o peer-host local.

## Verificação do plugin ao iniciar

Ao abrir o NavBR, a interface React verifica a instalação real do plugin OMSI. Ela confere os três arquivos esperados (`NavBR.OmsiPlugin.dll`, `NavBR.OmsiInterop.dll` e `NavBR.OmsiPlugin.opl`) e o manifesto de versão. Estados ausente, parcial ou desatualizado geram um aviso com ação explícita **Instalar / atualizar plugin**. A instalação nunca é silenciosa e exige que o OMSI esteja fechado.
