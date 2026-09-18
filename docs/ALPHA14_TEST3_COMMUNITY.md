# Alpha.14 Test 3 — roteiro de teste

## 1. Atualize o plugin

A Test 3 usa **Plugin Bridge v3 + Interop v3**.

Instale/atualize o plugin entregue junto da Test 3 antes de validar Personagem/RP. Plugin antigo deve ser tratado como incompatível, não como funcional.

## 2. Central Multiplayer

Confirme:

- existe somente uma experiência principal de Multiplayer;
- o item Multiplayer do menu abre diretamente a Central;
- Visão geral mostra o mapa em destaque;
- Sala separa criação e entrada;
- Jogadores não aparece como formulário;
- Chat & Voz mantém chat separado das opções PTT;
- Personagem/RP tem aba própria;
- Avançado concentra firewall, UPnP/NAT, bridge e ônibus físico.

## 3. Personagem / RP sem multiplayer

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

## 4. Movimento no mapa

Durante RP:

- parado: posição deve permanecer estável;
- a pé: posição deve mudar continuamente;
- correndo: deslocamento deve ser maior;
- giro A/D deve atualizar heading;
- o marcador da Central deve acompanhar o estado.

## 5. Simulação de sala

Desenvolvedores podem usar `docs/multiplayer-simulator.md` para gerar vários players sem vários PCs.

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

## 6. Multiplayer físico real

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
- se o personagem apareceu fora do ônibus;
- se W/S/A/D/Shift funcionaram;
- se Esc restaurou o motorista;
- se Central/HUD acompanharam movimento.
