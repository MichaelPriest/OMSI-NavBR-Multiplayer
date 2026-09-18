# Plugin OMSI experimental — Alpha.14

O plugin de escrita física continua **experimental, opt-in e focado no OMSI 2.3.004**.

## Bridge v3

- named pipe: OMSI.NavBR.Multiplayer.Plugin.v3;
- ABI/state version 3;
- plugin Native AOT x86;
- capacidades atualizadas em runtime;
- cliente e plugin de gerações antigas são deliberadamente incompatíveis.

## Ônibus remoto físico

O cliente possui um coordenador único para spawn/update/despawn.

Antes de escrever no OMSI, valida:

- opt-in;
- bridge conectado;
- capabilities necessárias;
- jogador remoto em jogo;
- identidade real do veículo;
- mapa/protocolo compatíveis;
- limites de segurança.

## Personagem / RP

A Alpha.14 também usa o plugin v3 para o modo Personagem/RP:

- seleciona personagem real da lista Drivers;
- salva vínculo/IA/pose;
- destaca o motorista do ônibus;
- aplica transform durante o controle;
- restaura estado ao retornar.

Controles iniciais: W/S, A/D, Shift e Esc.

## Segurança

Nenhum ponteiro vindo da rede é usado diretamente. Ponteiros físicos são descobertos e validados localmente.

Caminhos de veículos devem resolver para arquivos válidos dentro da instalação OMSI.

## Ainda experimental

- portas e matriz por modelo;
- articulação;
- câmera RP dedicada;
- terreno inclinado;
- animações/gestos;
- personagem remoto físico completo;
- compatibilidade ampla com outras versões do OMSI.
