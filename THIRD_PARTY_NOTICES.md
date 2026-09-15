# Third-party notices

OMSI NavBR Multiplayer usa bibliotecas de terceiros distribuídas sob licenças permissivas. Este arquivo acompanha o código-fonte e deve acompanhar os pacotes binários/release quando aplicável.

## Microsoft ASP.NET Core / SignalR

- Pacote principal usado pelo cliente: `Microsoft.AspNetCore.SignalR.Client`
- Framework usado pelo servidor/host: ASP.NET Core
- Licença: MIT
- Projeto: https://github.com/dotnet/aspnetcore
- Texto da licença: `licenses/ASP.NET-Core-LICENSE.txt`

## NAudio

- Uso no NavBR: captura do microfone e reprodução/mixagem do chat de voz no Windows
- Versão planejada nesta release: `2.4.0`
- Licença: MIT
- Projeto: https://github.com/naudio/NAudio
- Texto da licença: `licenses/NAudio-LICENSE.txt`

## Concentus / Opus

- Uso no NavBR: codificação e decodificação Opus do chat de voz
- Versão planejada nesta release: `2.2.2`
- Licença: licença permissiva do projeto/Opus, com obrigação de preservar o aviso e disclaimer em redistribuições binárias
- Projeto: https://github.com/lostromb/concentus
- Texto da licença: `licenses/Concentus-LICENSE.txt`

## DNNE

- Uso no NavBR: gerar a camada nativa x86/exportações C necessárias para o protótipo experimental de plugin do OMSI
- Versão usada no protótipo: `2.1.2`
- Licença: MIT
- Projeto: https://github.com/AaronRobinsonMSFT/DNNE
- Texto da licença: `licenses/DNNE-LICENSE.txt`
- O plugin continua experimental e não integra o pacote normal do cliente enquanto não houver validação real no OMSI.

## .NET / WPF

O aplicativo também é construído sobre .NET e WPF. As distribuições self-contained incluem componentes do runtime e seus próprios avisos de terceiros gerados pela cadeia oficial do .NET. O projeto NavBR não altera os termos desses componentes.

## OMSI e marcas de terceiros

OMSI, Aerosoft, M-R-Software, Steam, Valve, Grand Theft Auto, GTA, Rockstar Games e outras marcas citadas pertencem aos respectivos titulares. O OMSI NavBR Multiplayer é um projeto independente e não incorpora assets proprietários dessas marcas.

A interface HUD do NavBR pode se inspirar em padrões de HUD de jogos de mundo aberto, mas deve manter identidade visual, ícones e layout próprios, sem copiar assets ou telas proprietárias.
