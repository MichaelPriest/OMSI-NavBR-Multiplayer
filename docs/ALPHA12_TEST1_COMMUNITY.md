# Alpha.12 Test 1 — checklist comunitário

Versão: `v0.3.0-alpha.12-test.1`

Esta é a primeira pré-release pública da Alpha.12. O objetivo é permitir testes reais enquanto os módulos restantes continuam aparecendo como **Em desenvolvimento** ou **Experimental**.

## O que já está incluído

- novo shell Alpha.12 e HUD redesenhado;
- GPS/HUD preservando a lógica de janela de gameplay estável;
- perfil local do motorista e estatísticas;
- empresa virtual e frota local;
- CCO/Dispatcher local com monitoramento remoto básico;
- multiplayer peer-host TCP 27730;
- diagnóstico de conectividade e saúde da sessão;
- UPnP opt-in e métricas de rede;
- salas privadas com senha somente em memória;
- navegador de salas públicas do servidor configurado;
- chat e voz PTT;
- canais de voz Geral, Empresa/Equipe, CCO e Proximidade;
- mute, deafen, ganho individual e seleção de microfone/saída;
- plugin Native AOT x86, bridge v2 e ônibus remoto físico experimental;
- Hardware Cockpit Serial já existente.

## Recursos ainda em desenvolvimento

A Alpha.12 ainda não está completa. Permanecem em desenvolvimento ou experimentais: presença global de salas, NAT traversal/fallback avançado, multiplayer 3D completo, tráfego IA compartilhado, Hardware Cockpit Wi-Fi/displays, sincronização de sessão, replay/Ghost, mapa web, eventos, moderação/permissões, SDK, workshop e companion.

## Testes prioritários

1. Abrir o NavBR e confirmar que a versão exibida é `v0.3.0-alpha.12-test.1`.
2. Confirmar HUD/GPS no gameplay e ocultação em menus/opções do OMSI.
3. Criar sala peer-host e conectar pelo menos outro PC.
4. Testar sala pública e sala privada com senha correta/incorreta.
5. Testar chat e PTT nos quatro canais de voz.
6. Testar proximidade com dois jogadores no mesmo mapa, aproximando e afastando os ônibus.
7. Testar mute/deafen, volume individual e troca de dispositivo de áudio.
8. Testar CCO com motorista remoto.
9. Testar plugin/bridge experimental somente com o opt-in ativado.
10. Relatar mapa, ônibus, versão do OMSI e passos exatos em caso de erro.

## Pacotes esperados

- `OMSI-NavBR-Multiplayer-v0.3.0-alpha.12-test.1-win-x86.exe`
- `OMSI-NavBR-Multiplayer-v0.3.0-alpha.12-test.1-win-x86.zip`
- `OMSI-NavBR-Server-v0.3.0-alpha.12-test.1-win-x64.zip`
- `OMSI-NavBR-Plugin-v0.3.0-alpha.12-test.1-win-x86.zip`
- `ALPHA12_TEST1_COMMUNITY.md`
- `HARDWARE_COCKPIT.md`
- `SHA256SUMS.txt`

Para a maioria dos usuários, use o **EXE standalone x86**.

## Observação

Esta versão é uma **pré-release de teste**. Recursos marcados como Em desenvolvimento/Experimental podem mudar nas próximas `alpha.12-test.N`.
