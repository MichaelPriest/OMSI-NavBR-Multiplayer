# Releases

O OMSI NavBR Multiplayer usa versionamento semântico (SemVer) e GitHub Actions para publicar builds de teste e releases gerais.

## Convenção

- `v0.x.y-alpha.n` — prerelease geral da série alpha;
- `v0.x.y-alpha.n-test.m` — prerelease pública de integração/teste comunitário;
- `v0.x.y-beta.n` — fase beta;
- `v0.x.y` — release estável.

## Estado atual

### Teste público atual

```text
v0.3.0-alpha.11-test.2
```

Release:

https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.11-test.2

A Test 2 é destinada à comunidade para validar a Alpha.11 em OMSI real. Ela inclui o cliente standalone, cliente ZIP, servidor dedicado e pacote técnico do plugin experimental.

### Release oficial anterior

```text
v0.3.0-alpha.10
```

A Alpha.10 continua disponível como referência anterior enquanto a Alpha.11 passa por testes comunitários.

## Destaques da Alpha.11 Test 2

- correção da velocidade no HUD usando `Groundspeed` com fallback para a velocidade linear real do OMSI;
- HUD/minimapa mais compacto e transparente;
- lógica de visibilidade baseada na janela real de gameplay do OMSI, escondendo o HUD em menus, opções e timetable;
- paradas filtradas pela rota/viagem ativa; todas as paradas só aparecem quando não há rota ativa;
- manual para iniciantes dentro do aplicativo, funcionando offline;
- créditos no app: **Desenvolvedor: MichaelPriest • Com apoio da IA ChatGPT**;
- multiplayer peer-host TCP `27730`, chat e voz;
- meta inicial de até 32 jogadores por sala;
- snapshots de tráfego com até 48 veículos relevantes, separados da contagem de jogadores;
- plugin Native AOT x86 e interop nativo para OMSI 2.3.004;
- **ônibus remoto físico 3D experimental**, desligado por padrão e ativado por opt-in;
- diagnósticos automáticos opcionais, desligados por padrão, com fila offline e sanitização.

## Pacotes publicados

A Test 2 publica:

```text
OMSI-NavBR-Multiplayer-v0.3.0-alpha.11-test.2-win-x86.exe
OMSI-NavBR-Multiplayer-v0.3.0-alpha.11-test.2-win-x86.zip
OMSI-NavBR-Server-v0.3.0-alpha.11-test.2-win-x64.zip
OMSI-NavBR-Plugin-v0.3.0-alpha.11-test.2-win-x86.zip
ALPHA11_TEST2_COMMUNITY.md
SHA256SUMS.txt
LICENSE
THIRD_PARTY_NOTICES.md
```

O **EXE standalone x86** é a opção recomendada para a maioria dos usuários.

## Ônibus remoto 3D experimental

A Test 2 é a primeira rodada pública da infraestrutura física remota. O recurso não deve ser tratado como estável.

O teste exige:

- OMSI 2.3.004;
- plugin atualizado;
- mesmo mapa/build compatível;
- modelo do ônibus remoto instalado localmente no mesmo caminho relativo dentro de `Vehicles\`;
- ativação explícita de **Ônibus remoto 3D (EXPERIMENTAL)**.

O NavBR não transfere nem redistribui ônibus pagos/proprietários.

Ainda precisam de validação ampla:

- posicionamento entre tiles;
- orientação/quaternion em mapas diferentes;
- luzes, freio e setas;
- compatibilidade com add-ons;
- remoção segura ao desconectar;
- estabilidade e impacto em FPS;
- comportamento com vários jogadores.

## Paradas na Alpha.11

A regra de exibição é:

- **rota ativa:** somente as paradas daquela rota/viagem;
- **sem rota ativa:** todas as paradas válidas do mapa podem aparecer.

O NavBR tenta resolver as estações do `.ttp`. Quando o TTData personalizado não permite resolver a sequência com segurança, usa o traçado ativo como fallback sem voltar a exibir todas as paradas do mapa.

## Diagnósticos comunitários

O envio automático de diagnóstico é opcional e desligado por padrão.

Quando autorizado, o cliente pode enviar somente informações técnicas necessárias para reproduzir problemas, como versão, contexto do OMSI, mapa, identificação técnica do veículo, estado do bridge/plugin/3D e mensagens de erro. Não envia chat, voz, senha, token ou arquivos pessoais.

## Gates do CI

A publicação da Test 2 só é concluída quando o workflow valida:

1. versão/tag esperada;
2. XAML e JavaScript do site;
3. interop nativo MSVC x86;
4. Shared e servidor;
5. plugin Native AOT x86;
6. arquitetura PE x86 das DLLs do plugin;
7. bundle do plugin embutido no cliente;
8. build do cliente WPF x86;
9. smoke test do bridge v2;
10. EXE standalone self-contained;
11. servidor dedicado win-x64;
12. geração de ZIPs e `SHA256SUMS.txt`;
13. upload dos assets para a release;
14. atualização do catálogo do GitHub Pages.

Esses gates reduzem regressões de build, mas não substituem o teste real dentro do OMSI.

## Política de builds `-test`

- são marcadas como **Prerelease**;
- cada número de teste representa uma rodada pública específica;
- correções relevantes recebem uma nova build ou atualização controlada antes da publicação;
- checksums SHA-256 acompanham os assets;
- limitações experimentais devem permanecer descritas publicamente.

## Histórico recente

- `v0.3.0-alpha.11-test.1` — primeira build pública da Alpha.11;
- `v0.3.0-alpha.11-test.2` — velocidade corrigida, HUD compacto, filtro de paradas, manual interno, diagnósticos e primeira rodada pública do 3D experimental;
- `v0.3.0-alpha.10` — release oficial anterior, baseada na linha de integração Alpha.10.

## Site e catálogo

O portal oficial é:

https://michaelpriest.github.io/OMSI-NavBR-Multiplayer/

O GitHub Pages lê `site/releases.json` e apresenta os downloads publicados. O workflow de release atualiza esse catálogo após a publicação.

## Créditos

**Desenvolvedor:** MichaelPriest  
**Apoio ao desenvolvimento:** IA ChatGPT
