# Alpha.15 — roteiro de validação / Alpha.15 — validation guide

## Português (pt-BR)

### 1. Instalação e plugin

- selecione a instalação pelo diretório, `Omsi.exe`, `.lnk` ou `.url`;
- feche o OMSI e execute Atualizar/Reinstalar plugin;
- confirme estado **INSTALLED/current**;
- abra o OMSI pelo NavBR e confirme Plugin Bridge conectado.

### 2. Ônibus físico — dois PCs

- use o mesmo mapa em ambos os PCs;
- os ônibus podem estar em rotas diferentes;
- fique inicialmente a menos de 750 m;
- confirme que o ônibus remoto aparece na Kachel correta;
- atravesse uma fronteira de Kachel e confirme continuidade;
- valide orientação, suavização, luzes/setas e despawn;
- se falhar, copie o código **e a mensagem detalhada** mostrados pelo NavBR.

### 3. Personagem/RP

- clique Sair do ônibus;
- teste W/S, A/D e Shift;
- retorne ao ônibus;
- confirme que o motorista volta fisicamente ao veículo;
- repita o ciclo mais de uma vez.

### 4. Navegação

- teste roadmap real;
- teste linha/rota ativa;
- saia da rota e confirme caminho de retorno pelas splines;
- registre `navbr-route.log` se a rota não resolver.

### 5. Multiplayer

- Servidor NavBR;
- LAN;
- Online através do Host;
- jogadores no mesmo mapa e próximos;
- chat, voz e reconexão.

## English (en)

### 1. Installation and plugin

Select the OMSI installation through folder, `Omsi.exe`, `.lnk` or `.url`, update the plugin while OMSI is closed, then launch OMSI through NavBR and confirm the Plugin Bridge connects.

### 2. Physical bus — two PCs

Use the same map, stay initially within 750 m, verify the remote bus appears on the correct Kachel, cross a Kachel boundary, and validate orientation, smoothing, lights/signals and despawn. If it fails, capture both the status code and the detailed backend message.

### 3. Character/RP

Exit the bus, move with W/S and A/D, use Shift, return to the bus and confirm the driver is physically restored. Repeat the cycle.

### 4. Navigation

Validate the real roadmap, active route, route-rejoin path and collect `navbr-route.log` when route resolution fails.
