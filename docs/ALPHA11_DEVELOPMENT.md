# v0.3.0-alpha.11 — desenvolvimento

A alpha.11 permanece em desenvolvimento na branch:

```text
feature/alpha11-deep-omsi-integration
```

A `main` e a release oficial `v0.3.0-alpha.10` não devem ser alteradas até a validação real desta fase.

## Objetivos desta rodada

- aprofundar a integração com dados reais do OMSI;
- melhorar HUD/GPS sem regredir o comportamento de janela aprendido na alpha.8/alpha.10;
- adicionar informações úteis do veículo sem inventar valores;
- melhorar ferramentas de mapa/roadmap;
- preparar a base para Ghost Bus e, depois, representação física de veículos remotos.

## HUD e GPS

### Pontos de parada

O HUD passa a ler objetos funcionais de parada diretamente das tiles `.map` do mapa ativo.

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

O modo de montagem por tiles passa a usar o `global.cfg` como fonte oficial dos limites da grade.

Isso corrige um caso importante: se uma imagem `.roadmap.bmp` de uma tile de borda estiver ausente, o gerador não deve reduzir os limites do mapa e deslocar todas as coordenadas do GPS.

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

- CI `alpha11-build` verde;
- OMSI 2.3.004 real: HUD aparece durante gameplay e some em menus/diálogos;
- paradas aparecem nas posições corretas em mapas diferentes;
- próxima parada é destacada corretamente;
- troca de ícone persiste entre sessões;
- visão geral enquadra a rota completa sem deslocamento;
- Roadmap Studio mantém a grade correta quando faltam roadmaps de borda;
- nenhum crash do OMSI ou do cliente;
- multiplayer e plugin bridge continuam sem regressão.
