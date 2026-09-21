# OMSI NavBR Multiplayer v0.3.0-alpha.19

## Português (Brasil)

A Alpha.19 amplia o teste público e inclui pela primeira vez o **NavBR Mobile Companion Alpha 2 para Android** no próprio GitHub Release.

### Mobile Companion Alpha 2

- APK Android incluído na release;
- descoberta automática do NavBR no PC pela mesma rede LAN/Wi-Fi (UDP 27732);
- conexão HTTP autenticada na LAN (TCP 27731), com IP/código manual como fallback;
- GPS, rota, retorno à rota, próximas paradas e telemetria real do ônibus;
- estado multiplayer, mapa/lista de jogadores, voz, mixer e PTT pelo celular;
- painel IBIS em formato de equipamento de cockpit, com visor de linha/curso/destino/HOF/próxima parada/atraso;
- perfis visuais para famílias IBIS/ATRON/ALMEX/EFAD/Matrix quando os eventos do veículo permitirem identificar o equipamento;
- teclas IBIS só ficam acionáveis quando existe correspondência com um `[mouseevent]` real detectado no ônibus;
- todo press/release é revalidado pelo desktop antes de chegar ao Plugin Bridge;
- controles locais do ônibus pelo celular exigem autorização experimental explícita e a capability `local-vehicle-trigger`.

### Segurança e limitações

Não são inventadas teclas, triggers, linha, rota, destino, posições ou telemetria. O OMSI/C# permanece como autoridade. A escrita direta de variáveis/stringvars de IBIS continua desativada até existir resolução segura por veículo.

Esta continua sendo uma **alpha pública de teste**. Multiplayer LAN/online, ônibus remotos físicos, RP físico e compatibilidade com diferentes IBIS/add-ons ainda precisam de validação real mais ampla.

## English

Alpha.19 expands public testing and includes the **NavBR Mobile Companion Alpha 2 Android APK** in the GitHub Release.

The mobile app provides automatic same-LAN discovery, authenticated local connectivity, real navigation and vehicle telemetry, multiplayer/player state, voice/PTT, and a cockpit-style IBIS panel. IBIS keys are enabled only when they map to real detected vehicle `[mouseevent]` entries and every press/release is revalidated by the desktop before reaching the Plugin Bridge.

Direct generic IBIS variable/stringvar writes remain disabled. Local vehicle controls remain experimental, opt-in, and require the explicit `local-vehicle-trigger` capability.

LAN/online multiplayer, remote physical buses/RP, and broad IBIS/add-on compatibility still require wider real-world validation.
