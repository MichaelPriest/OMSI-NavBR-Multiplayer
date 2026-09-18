# OMSI NavBR Multiplayer — Alpha.14 Test 3.3

Esta revisão corrige os problemas encontrados na Test 3.2 e adiciona um atalho direto para iniciar o OMSI pela Home.

## Correções de interface

- **Selects / ComboBox**:
  - template escuro nomeado e explícito;
  - dropdown, itens, hover e seleção permanecem no tema NavBR;
  - aplicação do estilo também em runtime para ComboBoxes criados ou movidos por código;
  - evita retorno ao fundo branco padrão do Windows.

- **Mover HUD**:
  - continua em **Sistema**;
  - continua em **Ações rápidas** da Home;
  - agora também fica fixo no **topo do app**, sempre visível;
  - abre o HUD e alterna o modo de mover/redimensionar.

## Executar OMSI

- Novo atalho **Executar OMSI** na Home.
- Também fica destacado no topo da Home ao lado de **Abrir navegação**.
- Usa a instalação/perfil real cadastrado no NavBR.
- Se houver perfil preferido, ele tem prioridade.
- Se o OMSI já estiver aberto nessa instalação, não abre uma segunda cópia.
- Se nenhuma instalação válida existir, abre **Instalações OMSI** para selecionar a pasta que contém `Omsi.exe`.
- O lançamento é direto pelo executável detectado/configurado e não depende de iniciar pela Steam.

## Firewall TCP 27730

- Regra de entrada recriada para **todos os perfis de rede do Windows**: Privado, Público e Domínio.
- A regra é baseada na porta TCP 27730, não somente no executável atual.
- Isso cobre tanto a sala hospedada pelo cliente quanto o servidor dedicado.
- Solicita elevação UAC.
- Verifica a regra depois da criação.
- Se o UAC for cancelado ou o Windows recusar a alteração, o app mostra o resultado em vez de indicar sucesso incorretamente.

## Mantido

- Central Multiplayer sem wizard antigo sobreposto.
- Salas públicas.
- Voz/canais.
- Personagem/RP.
- Mapa 3D.
- CCO, Empresa/Frota, Rede, Equipe e Perfil.
- Roadmap Studio, Instalações OMSI, Ghost e diagnóstico.
- Simulador com servidor local automático.
- plugin/bridge v3 e x86.
