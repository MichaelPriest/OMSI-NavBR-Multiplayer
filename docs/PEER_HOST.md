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

## Estado físico

A Alpha.14 inclui multiplayer físico experimental de ônibus via plugin v3. O recurso é opt-in, exige compatibilidade local e ainda precisa de validação em modelos/mapas reais.

## Simulador

O simulador pode entrar na mesma sala do host, herdar mapa, posição e operação ativa e criar bots próximos para testar a Central e o mapa sem múltiplas instâncias do OMSI.
