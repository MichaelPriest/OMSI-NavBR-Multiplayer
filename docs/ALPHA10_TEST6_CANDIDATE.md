# v0.3.0-alpha.10-test.6 — candidato de publicação

Este arquivo registra o gate do candidato permanente `v0.3.0-alpha.10-test.6`.

Antes do disparo da prerelease, o workflow `build` concluiu com sucesso os seguintes gates:

- ícone compacto NavBR gerado em 10 resoluções;
- plugin OMSI Native AOT `win-x86` publicado;
- PE/I386 e seis exports OMSI validados;
- instalação e remoção do plugin testadas sem preparar .NET Runtime x86;
- payload Native AOT embutido no cliente;
- cliente WPF x86 compilado;
- smoke test do Named Pipe/bridge concluído;
- cliente self-contained e EXE standalone publicados pelo CI;
- recurso de ícone confirmado dentro do EXE standalone;
- bundle de integração montado.

A publicação continua sendo uma **prerelease experimental**. Validação real dentro do OMSI 2.3.004, HUD/GPS, chat/input, PTT e dois computadores permanece responsabilidade do teste de aceitação em ambiente real.

Não há representação física de ônibus remotos no OMSI 3D nesta build.
