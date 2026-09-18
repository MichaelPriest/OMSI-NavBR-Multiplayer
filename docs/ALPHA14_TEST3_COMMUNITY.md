# Alpha.14 Test 3 — roteiro de teste

A Test 3 é a prerelease pública de validação da Alpha.14. O objetivo é confirmar o novo shell React/WebView2 e o ciclo funcional multiplayer/RP antes da promoção final.

## 1. Atualize o plugin

A Test 3 usa **Plugin Bridge v3 + Interop v3**.

Instale/atualize o plugin entregue junto da Test 3 antes de validar Personagem/RP. Plugin antigo deve ser tratado como incompatível, não como funcional.

## 2. Interface React

Confirme:

- o app abre diretamente no shell React/WebView2;
- nenhuma tela antiga WPF aparece durante navegação normal;
- primeiro acesso, ajuda, configurações, perfil, empresa, Ghost, instalações, roadmap e diagnóstico permanecem dentro do React;
- seletores de arquivo/pasta do Windows podem abrir como diálogos nativos;
- o HUD OMSI continua nativo quando necessário.

## 3. Central Multiplayer

Confirme:

- existe somente uma experiência principal de Multiplayer;
- o item Multiplayer do menu abre diretamente a Central;
- Visão geral mostra o mapa em destaque;
- Sala separa criação e entrada;
- diretório de salas públicas, busca/favoritos e entrada funcionam no React;
- Jogadores não aparece como formulário;
- Chat & Voz mantém chat separado das opções PTT;
- Personagem/RP tem aba própria;
- Avançado concentra firewall, UPnP/NAT, bridge e ônibus físico.

## 4. Personagem / RP sem multiplayer

1. abra OMSI 2.3.004;
2. carregue completamente mapa e ônibus;
3. abra Personagem/RP;
4. selecione um personagem real da lista Drivers;
5. ative o personagem;
6. confirme que ele deixa o banco/ônibus e aparece fora da carroceria;
7. use W/S para andar;
8. use A/D para girar;
9. segure Shift para correr;
10. use Esc ou **Voltar ao ônibus**;
11. confirme que o motorista volta ao estado anterior.

Registre qualquer mensagem exibida pela aba RP.

## 5. Movimento no mapa

Durante RP:

- parado: posição deve permanecer estável;
- a pé: posição deve mudar continuamente;
- em rua inclinada com spline válida: o personagem deve acompanhar a elevação sem saltos bruscos;
- fora de uma spline confiável: a interface deve indicar altura preservada, sem inventar terreno;
- correndo: deslocamento deve ser maior;
- giro A/D deve atualizar heading;
- o marcador React/HUD deve acompanhar o estado;
- nenhum marcador paralelo deve aparecer em layout WPF antigo.

## 6. Simulação de sala

Desenvolvedores podem usar `docs/MULTIPLAYER_SIMULATOR.md` para gerar vários players sem vários PCs.

Teste sugerido:

```powershell
dotnet run --project src/NavBR.MultiplayerSimulator -- `
  --server http://127.0.0.1:27730 `
  --room navbr-sim `
  --players 8 `
  --mode mixed `
  --duration 15 `
  --verify
```

Na Central, observe entradas/saídas, movimento, rotação, nomes e alternância entre ônibus/RP.

## 7. Multiplayer físico real

O simulador não substitui este teste. Para ônibus físico e RP dentro do OMSI, repita com dois PCs reais:

- A vê B;
- B vê A;
- movimento;
- setas/luzes;
- despawn/reconexão;
- retorno do personagem ao ônibus;
- sem duplicação.

## Informe no relato

- mapa;
- ônibus;
- personagem Drivers selecionado;
- versão do plugin exibida;
- mensagem de erro, se houver;
- se alguma tela WPF antiga apareceu;
- se o personagem apareceu fora do ônibus;
- se W/S/A/D/Shift funcionaram;
- se Esc restaurou o motorista;
- se Central/HUD acompanharam movimento.
