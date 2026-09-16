# v0.3.0-alpha.11 — desenvolvimento

A alpha.11 continua em desenvolvimento. A rodada atual de interface e hardware está na branch:

```text
feature/alpha11-ui-refresh-hardware
```

Ela parte diretamente da `test/alpha11-test3`, preservando as correções já validadas da Alpha.11 Test 3. A `main` não deve receber esta fase antes da validação real.

## Objetivos desta rodada

- simplificar a interface principal e reduzir a sensação de excesso de controles;
- separar uso cotidiano de ferramentas técnicas/experimentais;
- aprofundar a integração com dados reais do OMSI;
- melhorar HUD/GPS sem regredir o comportamento de janela aprendido nas versões anteriores;
- adicionar informações úteis do veículo sem inventar valores;
- melhorar ferramentas de mapa/roadmap;
- preparar a base para Ghost Bus e representação física de veículos remotos;
- iniciar o Hardware Cockpit Bridge para Arduino/ESP32.

## Nova organização da interface

O shell da Alpha.11 deixa de apresentar todas as funções no mesmo nível.

### Navegação principal

A lateral passa a destacar apenas as áreas de uso mais frequente:

- **Visão geral** — estado do OMSI e telemetria essencial;
- **Navegação** — GPS, mapa e rota;
- **Multiplayer** — salas, chat, voz e sincronização;
- **Hardware cockpit** — telemetria destinada a painéis físicos.

### Ferramentas avançadas

Recursos de desenvolvimento/manutenção ficam recolhidos em **Ferramentas avançadas**, reduzindo ruído visual:

- diagnóstico técnico;
- Roadmap Studio;
- instalações/perfis OMSI;
- Ghost 3D;
- outras ferramentas experimentais futuras.

A barra lateral mantém rolagem vertical automática e o rodapé de idioma/minimização continua fixo.

## Hardware Cockpit Bridge

A Alpha.11 passa a reservar uma área própria para integração de hardware físico.

O primeiro contrato é `NAVBR_HW_V1`. A tela de Hardware Cockpit mostra em tempo real o pacote que será enviado aos dispositivos e já reserva os seguintes dados:

- mapa;
- linha e rota;
- destino;
- rua atual;
- próxima parada;
- parada solicitada;
- velocidade;
- atraso;
- portas;
- seta/pisca.

Os campos `CurrentStreetName` e `StopRequested` foram adicionados à telemetria como campos opcionais/seguros. Nesta etapa eles ainda dependem da implementação do resolvedor de rua e da leitura específica da solicitação de parada do ônibus.

Transportes planejados:

1. **USB / Serial** para Arduino Uno, Mega, Nano e ESP32;
2. **Wi-Fi** para ESP32, inicialmente via UDP ou WebSocket;
3. protocolo versionado para que projetos físicos antigos continuem compatíveis com versões futuras do NavBR.

A arquitetura permanece prioritariamente **NavBR → hardware**, sem exigir que o microcontrolador leia memória do OMSI. Escrita de hardware de volta ao simulador será uma fase separada e opcional.

## HUD e GPS

### Pontos de parada

O HUD lê objetos funcionais de parada diretamente das tiles `.map` do mapa ativo.

A implementação atual:

- reconhece o objeto padrão `Sceneryobjects\Generic\bus_stop.sco` e nomes compatíveis;
- usa GridX/GridY + TileX/TileY para posicionar a parada no mesmo sistema do roadmap;
- desenha apenas as paradas que entram no viewport do GPS;
- mantém os símbolos legíveis no modo heading-up;
- identifica a próxima parada pelo nome da telemetria e escolhe a ocorrência compatível mais próxima;
- destaca a próxima parada com tamanho/glow maior.

### Ícones de parada

Há três modos:

1. **Padrão OMSI** — símbolo clássico `H`;
2. **Minimalista** — marcador simples;
3. **Personalizado** — imagem PNG, JPG/JPEG ou BMP escolhida pelo usuário.

A preferência fica em:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\multiplayer.json
```

Se a imagem personalizada ficar indisponível, o HUD usa fallback seguro para o padrão OMSI.

### Visão geral da rota

O mapa principal recebe o botão **Rota completa**.

Quando existe rota ativa resolvida:

- a geometria da rota é desenhada sobre o roadmap;
- o NavBR calcula min/max X/Y dos pontos reais da rota;
- o zoom é ajustado para encaixar todo o percurso com margem;
- o viewport é centralizado no centro geométrico da rota;
- o modo `Seguir ônibus` é desativado enquanto a visão geral é ativada;
- zoom e pan continuam disponíveis;
- ao sair da visão geral, o mapa volta a seguir o ônibus.

O texto do botão possui versões em pt-BR, inglês, espanhol, alemão e francês.

## Roadmap Studio

O modo de montagem por tiles usa o `global.cfg` como fonte oficial dos limites da grade.

Isso corrige o caso em que uma imagem `.roadmap.bmp` de uma tile de borda está ausente: o gerador não deve reduzir os limites do mapa e deslocar as coordenadas do GPS.

Agora a análise diferencia:

- tile realmente configurada no `global.cfg`, mas sem imagem de roadmap;
- célula vazia da grade retangular que não representa uma tile do mapa.

O gerador mantém:

- escrita do BMP por streaming;
- backup automático do `whole.roadmap.bmp` anterior;
- arquivo temporário seguro;
- bloqueio para dimensões/arquivos impraticáveis;
- modo vetorial por splines como alternativa.

## Referências técnicas

Para entender formatos públicos do OMSI, foram consultados projetos abertos já registrados na documentação, incluindo OMSI Launcher/OmsiHook e OMSI RouteAdvisor. O NavBR mantém implementação própria e não redistribui assets proprietários do simulador.

## Validação obrigatória antes da alpha.11 oficial

- build/CI verde;
- menu lateral permanece utilizável em janelas pequenas e escalas diferentes do Windows;
- navegação por páginas não perde controles nem quebra a atualização de telemetria;
- Hardware Cockpit mostra o `NAVBR_HW_V1` sem interferir no loop de telemetria;
- OMSI 2.3.004 real: HUD aparece durante gameplay e some em menus/diálogos;
- paradas aparecem nas posições corretas em mapas diferentes;
- próxima parada é destacada corretamente;
- troca de ícone persiste entre sessões;
- visão geral enquadra a rota completa sem deslocamento;
- Roadmap Studio mantém a grade correta quando faltam roadmaps de borda;
- nenhum crash do OMSI ou do cliente;
- multiplayer e plugin bridge continuam sem regressão.
