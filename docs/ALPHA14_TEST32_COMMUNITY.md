# Alpha.14 Test 3.2 — roteiro rápido

## Layout e funções

1. Abra o app e confira as seções **Dirigir**, **Operação**, **Sistema**, **Ferramentas** e **Ajuda**.
2. Confirme acesso a Mapa 3D, CCO, Empresa/Frota, Rede da empresa, Equipe e Perfil.
3. Em Sistema, teste **Mover HUD**, **Configurar HUD**, Configurações, Saúde da sessão e Diagnóstico.
4. Em Ferramentas, confira Roadmap Studio, Instalações OMSI, Ghost, Conectividade, NAT/UPnP e teste TCP 27730.
5. Abra Manual e Feedback.
6. Redimensione a janela e confirme que a sidebar compacta mantém as funções acessíveis por ícone/tooltip.
7. Abra qualquer select (idioma, HUD, hardware, atalhos) e confirme que campo e dropdown permanecem escuros.

## Central Multiplayer

1. Abra **Central Multiplayer > Sala**.
2. Confirme que não aparece mais o assistente **1 Sala / 2 Privacidade / 3 Rede**.
3. Confirme que existe apenas uma interface: Criar sala à esquerda e Entrar em sala à direita.
4. Teste **Salas públicas**, convite, sala pública e sala privada.
5. Abra Chat & Voz e teste as opções/canais de voz.
6. Abra **Avançado > Rede / NAT** e confirme UPnP e Relay.

## Simulador

### Testando uma sala criada pelo app

1. No app, crie a sala na porta 27730.
2. Execute `run-multiplayer-simulator.ps1` ou `NavBR.MultiplayerSimulator.exe`.
3. Os players devem entrar no host já existente sem iniciar outro servidor.

### Testando sem host aberto

1. Execute o simulador com o servidor em `http://127.0.0.1:27730`.
2. Ele deve iniciar automaticamente o servidor incluído na pasta `server`.
3. No app, entre no servidor `http://127.0.0.1:27730` e na sala `navbr-sim`.
4. Confirme os players simulados e a movimentação no mapa.
