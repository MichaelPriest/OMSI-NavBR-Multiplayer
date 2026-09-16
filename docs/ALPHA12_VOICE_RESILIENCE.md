# Alpha.12 — resiliência de voz

Este documento registra a evolução do áudio multiplayer da Alpha.12 após a publicação da `v0.3.0-alpha.12-test.1`.

## Objetivo

Reduzir cortes e áudio irregular quando os pacotes de voz chegam com atraso, fora de ordem ou com perda isolada, sem alterar o protocolo público `VoiceFrame` e sem aumentar a dependência de serviços externos.

## Implementação atual

- `VoiceFrame.Sequence` passa a ser usado para ordenar o playout remoto;
- cada jogador remoto possui um `AdaptiveVoiceJitterBuffer` independente;
- o áudio recebido é reproduzido em cadência de 20 ms, em vez de ser decodificado imediatamente no callback de rede;
- o alvo do jitter buffer varia entre 40 ms e 120 ms conforme a variação observada entre timestamp do frame e horário local de chegada;
- pacotes duplicados e atrasados são descartados sem reinjetar áudio antigo;
- uma perda isolada pode usar o FEC in-band do Opus a partir do pacote seguinte;
- gaps maiores são contabilizados e o buffer ressincroniza em vez de tentar reproduzir uma sequência longa de áudio inválido;
- uma nova fala após pausa prolongada reinicia o estado de sequência sem carregar atraso do talkspurt anterior;
- o buffer PCM final continua limitado e com descarte em overflow para impedir crescimento ilimitado.

## Indicadores de qualidade

O `VoiceChatService` publica um `VoiceQualitySnapshot` agregado contendo:

- streams de voz remotos ativos;
- pacotes recebidos;
- pacotes reproduzidos;
- pacotes recuperados por FEC;
- perdas estimadas;
- pacotes atrasados;
- duplicados;
- jitter médio observado;
- alvo atual do jitter buffer.

A Central Multiplayer mostra no botão de voz um indicador compacto com jitter e perda estimada. O tooltip detalha FEC e tamanho do buffer. Quando o jitter chega a 45 ms ou a perda estimada chega a 5%, a interface passa a exibir alerta de degradação.

## Segurança e compatibilidade

- não houve mudança no contrato `VoiceFrame`;
- não houve nova porta de rede;
- não houve escrita adicional no OMSI;
- mute, deafen, ganho individual, seleção de dispositivos e canais continuam preservados;
- a implementação continua usando Opus/Concentus e NAudio já presentes no projeto.

## Validação necessária

Além do CI, ainda é necessário validar em sessão real entre dois ou mais PCs:

1. voz estável em LAN sem perda artificial;
2. reordenação de frames sem estalos perceptíveis;
3. recuperação FEC de perda isolada;
4. comportamento com jitter crescente;
5. troca entre Geral, Empresa/Equipe, CCO e Proximidade;
6. mute/deafen durante áudio ativo;
7. troca de dispositivo durante sessão;
8. reconexão e remoção de streams sem timers residuais.

A validação real de rede continua sendo requisito antes de considerar a voz estável para uma release não experimental.
