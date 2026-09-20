# Verificador e atualizador do plugin OMSI

O cliente NavBR trata o plugin OMSI empacotado na própria build como a fonte de verdade.

## O que é verificado

Em cada abertura do NavBR e antes de iniciar o OMSI pelo cliente:

1. localiza a instalação real do OMSI;
2. confere a presença de `NavBR.OmsiPlugin.dll`, `NavBR.OmsiInterop.dll` e `NavBR.OmsiPlugin.opl`;
3. calcula SHA-256 de cada arquivo instalado;
4. calcula SHA-256 das mesmas entradas dentro do bundle de plugin embutido;
5. compara versão e hash do manifesto de instalação;
6. considera o plugin atualizado somente quando os três arquivos e o manifesto correspondem à build atual.

## Atualização automática

Quando o plugin estiver ausente, incompleto ou diferente do bundle atual, o NavBR chama o mesmo instalador transacional já usado pelo cliente. Os arquivos existentes rastreados são copiados para staging/backup antes da substituição e a instalação é verificada novamente depois da gravação.

O NavBR nunca substitui binários enquanto `Omsi.exe` estiver em execução. Nesse caso, a atualização é agendada e aplicada automaticamente quando o OMSI fechar.

Arquivos com nomes reservados do NavBR que não tenham um manifesto confiável continuam protegidos: o cliente não os sobrescreve silenciosamente.

## Interface

Configurações > Instalações mostra:

- quantidade de arquivos encontrados;
- quantidade de arquivos com SHA-256 correto;
- versão instalada e esperada;
- estado do manifesto;
- necessidade de atualização;
- estado individual de cada DLL/OPL;
- ação **Verificar e atualizar agora**.
