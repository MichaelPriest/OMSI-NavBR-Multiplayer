# Alpha.14 — notas da versão pública

Versão: v0.3.0-alpha.14

A Alpha.14 consolida o trabalho das builds Test 1–3.x e passa a ser a prerelease pública principal do OMSI NavBR Multiplayer.

## Interface

- shell Figma consolidado;
- funções de uso normal novamente visíveis;
- **Mover HUD** no topo, Sistema e Home;
- **Executar OMSI** diretamente pela Home;
- ComboBox/selects com tema escuro completo;
- Mapa 3D, CCO, Empresa/Frota, Rede da empresa, Equipe, Perfil, Saúde da sessão, Roadmap Studio, Instalações OMSI, Ghost, diagnóstico, NAT e teste de porta acessíveis.

## Central Multiplayer

- removido definitivamente o wizard legado que sobrepunha a aba Sala;
- Criar sala e Entrar em sala em superfícies independentes;
- salas públicas;
- salas privadas com senha;
- chat e voz;
- Personagem/RP;
- opções avançadas de rede;
- relay experimental preservado.

## Rede

- peer-host padrão em TCP 27730;
- servidor dedicado opcional;
- UPnP opcional;
- regra do Windows Firewall criada para TCP 27730 em **todos os perfis de rede**;
- elevação UAC e verificação posterior;
- mensagens claras quando UAC é cancelado ou a regra não é confirmada.

## Plugin / RP

- Named Pipe OMSI.NavBR.Multiplayer.Plugin.v3;
- ABI/state v3;
- capabilities atualizadas em runtime;
- motorista RP usa personagem real do mapa;
- restauração de pose/vínculo/IA ao retornar;
- ônibus remoto físico continua experimental.

## Simulador

- pode iniciar automaticamente o servidor local empacotado;
- reutiliza o host existente se o app já estiver hospedando TCP 27730;
- suporta sala privada via senha;
- herda o **mesmo mapa** da sala real;
- usa a **posição real do host** como centro;
- bots ficam próximos, por padrão em raio de 18 m;
- herda **linha, rota, destino e próxima parada** da operação ativa;
- o modo --verify valida movimento e consistência de mapa.

## Observação

A Alpha.14 é pública, mas continua sendo uma prerelease. Escrita física no OMSI e Personagem/RP permanecem experimentais e devem ser testados com cautela.
