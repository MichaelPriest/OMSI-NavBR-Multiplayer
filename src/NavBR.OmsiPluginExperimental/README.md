# NavBR OMSI Plugin — experimental

Este projeto é um protótipo isolado para validar a interface oficial de plugins do OMSI 2 antes de qualquer tentativa de representar veículos remotos dentro do simulador.

## Estado atual

A fase atual é **somente diagnóstico**:

- compila como plugin Windows x86;
- exporta os callbacks esperados pelo OMSI;
- usa um arquivo `.opl` mínimo;
- recebe a system variable `Time` apenas para confirmar callbacks;
- grava `PluginStart`, heartbeat e `PluginFinalize` em log;
- **não escreve variáveis do veículo**;
- **não aciona triggers**;
- **não cria nem altera veículos no OMSI**.

O log é salvo em:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-plugin.log
```

## Por que começar assim

O multiplayer atual do NavBR funciona externamente ao OMSI e continua sendo o caminho estável. O plugin será opcional e só avançará se os testes mostrarem que pode operar sem comprometer o simulador.

A sequência planejada é:

1. validar carregamento e callbacks do plugin;
2. criar um bridge local entre plugin e cliente NavBR;
3. transmitir para o plugin apenas um estado remoto de teste;
4. investigar criação/controle seguro de uma entidade remota;
5. adicionar interpolação;
6. depois avaliar portas, luzes, setas, matriz, articulação e outros estados.

## Arquivos esperados no teste

O build via DNNE gera uma DLL nativa de entrada chamada:

```text
NavBR.OmsiPlugin.dll
```

O arquivo de configuração é:

```text
NavBR.OmsiPlugin.opl
```

Como o protótipo usa hospedagem .NET, o pacote de teste deve preservar também os arquivos de runtime/assemblies gerados pelo build. Não copie somente a DLL nativa até o empacotamento self-contained do plugin ser definido.

### Requisito temporário do protótipo

Nesta primeira fase o plugin é **framework-dependent**. Portanto, o computador de teste precisa ter o **.NET 10 Runtime x86** disponível para o processo 32-bit do OMSI. O EXE standalone do NavBR ser self-contained não instala esse runtime globalmente.

Antes de distribuir o plugin para usuários finais, o empacotamento será alterado para evitar esse requisito manual sempre que tecnicamente possível.

## Instalação para teste

Quando houver um artefato de CI aprovado:

1. feche o OMSI;
2. faça backup da pasta `plugins`;
3. confirme que o .NET 10 Runtime x86 está instalado no PC de teste;
4. extraia o pacote experimental;
5. copie **todo o conteúdo do pacote do plugin** para:

```text
<PASTA_DO_OMSI>\plugins\
```

6. inicie o OMSI normalmente;
7. carregue um mapa e um ônibus;
8. aguarde alguns segundos;
9. confira se existe:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-plugin.log
```

Um teste de carga bem-sucedido deve registrar `PluginStart` e heartbeats periódicos.

## Segurança

Este protótipo não deve ser incluído na release normal enquanto não houver validação real. Se o OMSI apresentar instabilidade, remova os arquivos `NavBR.OmsiPlugin*` da pasta `plugins` antes de continuar os testes.
