# Checklist de teste — v0.3.0-alpha.2

## Build / inicialização

- [ ] cliente Windows x86 compila e publica;
- [ ] EXE standalone mantém o ícone NavBR;
- [ ] cliente inicia sem OMSI e exibe estado correto;
- [ ] servidor dedicado Windows x64 inicia normalmente.

## OMSI / HUD

- [ ] OMSI 2.3.004 é detectado;
- [ ] HUD acompanha a posição/tamanho da janela do OMSI;
- [ ] minimapa aparece no canto inferior esquerdo;
- [ ] ônibus local fica centralizado no minimapa;
- [ ] `T` abre o campo de chat e Enter envia;
- [ ] Esc fecha a entrada de chat;
- [ ] `N` ativa voz somente enquanto pressionado;
- [ ] overlay não bloqueia mouse/teclas do OMSI fora do modo de chat.

## Sala peer-host

- [ ] Criar sala neste PC inicia porta TCP 27730;
- [ ] host conecta automaticamente à própria sala;
- [ ] endereço LAN é exibido;
- [ ] segundo PC na LAN consegue entrar;
- [ ] presença/saída são atualizadas;
- [ ] telemetria remota aparece no GPS/HUD;
- [ ] versões diferentes do mesmo mapa são sinalizadas por fingerprint.

## Chat / voz

- [ ] chat de texto aparece nos dois clientes;
- [ ] voz do host chega ao convidado;
- [ ] voz do convidado chega ao host;
- [ ] indicador de voz mostra o jogador que está falando;
- [ ] desabilitar chat por voz impede captura/transmissão;
- [ ] desconectar remove buffers/decoders do jogador remoto.

## Internet

- [ ] regra do Windows Firewall documentada/testada;
- [ ] port forwarding TCP 27730 documentado/testado;
- [ ] conexão por endereço público validada em redes diferentes.

Itens de runtime não são considerados concluídos apenas pelo GitHub Actions; precisam de teste real no Windows/OMSI.
