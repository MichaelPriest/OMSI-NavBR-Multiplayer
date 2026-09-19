# Alpha.14 — roteiro de teste comunitário

## 1. App e layout

- confira Home, Dirigir, Operação, Sistema, Ferramentas e Ajuda;
- teste **Executar OMSI**;
- teste **Mover HUD** pelo topo e por Sistema;
- abra selects de idioma, HUD, Hardware e Chat/Voz e confirme tema escuro;
- confirme acesso a Mapa 3D, CCO, Empresa/Frota, Rede, Equipe, Perfil, Roadmap, Instalações OMSI, Ghost e Diagnóstico.

## 2. Central Multiplayer

- confirme os três modos: **Servidor NavBR**, **LAN** e **Online através do Host**;
- crie uma sala em cada modo;
- confirme que não aparece o wizard antigo 1 Sala / 2 Privacidade / 3 Rede;
- teste sala pública e privada;
- teste convite;
- teste Salas públicas;
- teste chat e voz;
- teste Personagem/RP;
- no **Online através do Host**, confirme que o clique não congela a interface enquanto o UPnP é verificado;
- se o roteador não oferecer UPnP, confirme que a sala permanece funcionando em LAN e o estado muda sem travar o app.

## 3. Firewall

- em Conectividade/Avançado, permita TCP 27730;
- aceite o UAC;
- confirme a regra **OMSI NavBR Multiplayer - TCP 27730**;
- a regra deve valer para todos os perfis de rede;
- se cancelar o UAC, o NavBR deve informar o cancelamento.

## 4. Simulador no mesmo mapa

1. abra o OMSI e carregue um mapa;
2. inicie uma linha/rota;
3. entre/crie a sala no NavBR;
4. execute o simulador sem --map;
5. ele deve detectar o mapa real da sala;
6. deve aguardar a posição real do host;
7. os bots devem aparecer próximos ao host;
8. os bots devem publicar a mesma linha/rota/destino/próxima parada da operação ativa.

Para sala privada, informe a senha ao simulador.

## 5. RP

- selecione um personagem real de Drivers;
- ative o modo RP;
- teste W/S, A/D, Shift e Esc;
- volte ao ônibus e confirme restauração do motorista;
- reporte qualquer problema de câmera, terreno, pose ou restauração.

## 6. Multiplayer físico

Comece com dois PCs na mesma LAN. Valide spawn, movimento, orientação, despawn, reconexão e estabilidade antes de testar cenários de Internet/NAT mais complexos.
