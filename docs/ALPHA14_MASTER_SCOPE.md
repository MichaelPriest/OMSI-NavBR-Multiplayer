# Alpha.14 — escopo mestre

A **Alpha.14 pública** consolida o shell Figma, o multiplayer físico experimental, o modo Personagem/RP e a expansão de ferramentas operacionais.

Versão pública: **v0.3.0-alpha.14**.

## 1. Interface

- Home com Executar OMSI, Navegação e ações operacionais;
- Mover HUD disponível no topo, Sistema e Home;
- selects/ComboBox com tema escuro consistente;
- Central Multiplayer sem wizard legado sobreposto;
- funções restauradas: Mapa 3D, CCO, Empresa/Frota, Rede, Equipe, Perfil, Saúde da sessão, Roadmap Studio, Instalações OMSI, Ghost, conectividade/NAT, teste TCP, Manual e Feedback;
- interface em pt-BR, inglês, espanhol, alemão e francês.

## 2. Multiplayer

- peer-host TCP 27730 como padrão;
- servidor dedicado opcional;
- salas públicas e privadas;
- chat e voz;
- UPnP opcional;
- relay experimental;
- Firewall do Windows configurável para TCP 27730 em todos os perfis;
- presença, telemetria, mapa e estado operacional sincronizados por SignalR.

## 3. Simulador

O simulador é somente de desenvolvimento/teste.

- reutiliza host existente ou inicia servidor local empacotado;
- suporta senha de sala privada;
- herda mapa e compatibilidade da sala real;
- aguarda telemetria real para posicionar os bots perto do host;
- raio padrão de 18 m;
- herda linha, rota, destino e próxima parada da autoridade da sala;
- --verify exige movimento mensurável e mapa consistente.

## 4. Personagem / RP

- usa personagens reais da lista Drivers;
- funciona também sem multiplayer;
- bridge/plugin v3;
- entrada/saída e restauração de vínculo/IA;
- controles W/S, A/D, Shift e Esc;
- sincronização RP separada da telemetria do ônibus.

Ainda em validação física: câmera dedicada, terreno inclinado, animações/gestos, interação com objetos/veículos e personagem remoto físico completo.

## 5. Multiplayer físico

- spawn/update/despawn experimental;
- pose local/quaternion nativos;
- velocidade, luzes e setas quando suportadas;
- compatibilidade de mapa/veículo validada antes da escrita;
- opt-in obrigatório.

## 6. Critério da Alpha pública

1. cliente, servidor, plugin e bridge compilando;
2. layout sem superfícies sobrepostas;
3. funções principais acessíveis;
4. peer-host e servidor dedicado funcionando;
5. simulador entrando no mesmo mapa e operação ativa;
6. regressões zero no HUD/telemetria;
7. RP e ônibus físico continuando fail-safe e experimentais.
