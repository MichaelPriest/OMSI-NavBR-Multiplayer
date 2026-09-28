from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.platypus import (
    PageBreak,
    Paragraph,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "site" / "public" / "OMSI-NavBR-Multiplayer-Manual-Oficial-Alpha22.pdf"
OUT.parent.mkdir(parents=True, exist_ok=True)

styles = getSampleStyleSheet()
styles.add(ParagraphStyle(
    name="NavTitle",
    parent=styles["Title"],
    fontName="Helvetica-Bold",
    fontSize=25,
    leading=29,
    textColor=colors.HexColor("#0A1D2C"),
    spaceAfter=8,
))
styles.add(ParagraphStyle(
    name="NavSub",
    parent=styles["Heading1"],
    fontName="Helvetica-Bold",
    fontSize=18,
    leading=22,
    textColor=colors.HexColor("#149EE8"),
    spaceAfter=9,
))
styles.add(ParagraphStyle(
    name="NavH1",
    parent=styles["Heading1"],
    fontName="Helvetica-Bold",
    fontSize=17,
    leading=21,
    textColor=colors.HexColor("#0A1D2C"),
    spaceAfter=8,
    spaceBefore=4,
))
styles.add(ParagraphStyle(
    name="NavH2",
    parent=styles["Heading2"],
    fontName="Helvetica-Bold",
    fontSize=12,
    leading=15,
    textColor=colors.HexColor("#137AAE"),
    spaceAfter=5,
    spaceBefore=7,
))
styles.add(ParagraphStyle(
    name="NavBody",
    parent=styles["BodyText"],
    fontName="Helvetica",
    fontSize=9.4,
    leading=13.6,
    textColor=colors.HexColor("#263A49"),
    spaceAfter=5,
))
styles.add(ParagraphStyle(
    name="NavSmall",
    parent=styles["BodyText"],
    fontName="Helvetica",
    fontSize=8.2,
    leading=11.2,
    textColor=colors.HexColor("#607786"),
    spaceAfter=4,
))
styles.add(ParagraphStyle(
    name="NavCallout",
    parent=styles["BodyText"],
    fontName="Helvetica-Bold",
    fontSize=9,
    leading=13,
    textColor=colors.HexColor("#075A84"),
    backColor=colors.HexColor("#E9F7FE"),
    borderColor=colors.HexColor("#99DDF7"),
    borderWidth=.6,
    borderPadding=8,
    spaceBefore=6,
    spaceAfter=7,
))

def footer(canvas, doc):
    canvas.saveState()
    canvas.setFillColor(colors.HexColor("#607786"))
    canvas.setFont("Helvetica", 7.5)
    canvas.drawString(16 * mm, 10 * mm, "OMSI NavBR Multiplayer - Manual Oficial Alpha.22")
    canvas.drawRightString(194 * mm, 10 * mm, f"Pagina {doc.page}")
    canvas.restoreState()

def info_table(rows, widths=(52*mm, 118*mm)):
    table = Table(rows, colWidths=list(widths), repeatRows=1)
    table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#0A1D2C")),
        ("TEXTCOLOR", (0, 0), (-1, 0), colors.white),
        ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold"),
        ("FONTNAME", (0, 1), (0, -1), "Helvetica-Bold"),
        ("FONTSIZE", (0, 0), (-1, -1), 8.7),
        ("GRID", (0, 0), (-1, -1), .35, colors.HexColor("#B9CAD5")),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor("#F5F9FB")]),
        ("TOPPADDING", (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
        ("LEFTPADDING", (0, 0), (-1, -1), 6),
        ("RIGHTPADDING", (0, 0), (-1, -1), 6),
    ]))
    return table

doc = SimpleDocTemplate(
    str(OUT),
    pagesize=A4,
    leftMargin=16*mm,
    rightMargin=16*mm,
    topMargin=15*mm,
    bottomMargin=17*mm,
    title="OMSI NavBR Multiplayer - Manual Oficial Alpha.22",
    author="OMSI NavBR Multiplayer",
)

story = [
    Spacer(1, 14*mm),
    Paragraph("OMSI NavBR Multiplayer", styles["NavTitle"]),
    Paragraph("Manual Oficial - Alpha.22", styles["NavSub"]),
    Paragraph(
        "Guia de instalacao, operacao, navegacao, multiplayer, HUD, voz, personagem/RP, empresa, CCO, hardware e atalhos.",
        styles["NavBody"],
    ),
    Spacer(1, 8*mm),
    Paragraph(
        "<b>Estado da Alpha.22:</b> o cenario online com bots/AI do simulador seguindo o host foi validado no OMSI real. "
        "A validacao ponta a ponta com players reais em dois PCs/duas sessoes OMSI ainda e a proxima etapa.",
        styles["NavCallout"],
    ),
    Spacer(1, 10*mm),
    info_table([
        ["Modulo", "Resumo"],
        ["Inicio", "Estado do OMSI, mapa, multiplayer e atalhos rapidos."],
        ["Navegacao", "Roadmap, rota, paradas, manobras e retorno a rota."],
        ["Multiplayer", "Salas, jogadores, chat, voz, RP e onibus fisicos experimentais."],
        ["Rede da empresa", "Identidade NavBR, equipe, convites e Company Node."],
        ["CCO", "Operacao, motoristas, ocorrencias, telemetria e frota."],
        ["Hardware Cockpit", "Bridge serial para Arduino, ESP32, letreiros e LEDs."],
        ["HUD", "Presets, aparencia, modulos, posicao, mapa e multiplayer."],
    ]),
    PageBreak(),
]

sections = [
    ("1. Primeiros passos",
     "Abra o NavBR e use <b>Executar OMSI</b> ou inicie o simulador manualmente. "
     "Se a instalacao nao for detectada, configure a pasta que contem Omsi.exe. "
     "Carregue mapa e onibus e aguarde a telemetria ficar disponivel."),
    ("2. Navegacao e Roadmap",
     "O NavBR usa a geometria e os arquivos instalados localmente no OMSI. "
     "Mapa 3D, rota completa, paradas, proxima manobra, ETA e retorno a rota so aparecem quando os dados reais podem ser resolvidos com seguranca."),
    ("3. Criar ou entrar em uma sala",
     "Abra Multiplayer > Sala. Informe nome da sala e apelido. "
     "Use Servidor NavBR para conexao online, LAN para rede local ou Meu PC para host pela Internet. "
     "A porta padrao do host local e TCP 27730."),
    ("4. Jogadores e compatibilidade",
     "A comparacao de sala usa mapa, onibus, HOF, protocolo e a exigencia de onibus fisico quando ativada. "
     "Os dois lados precisam ter conteudo compativel localmente."),
    ("5. Chat e voz",
     "F9 abre o chat por padrao. F10 e o Push-to-Talk padrao. "
     "Chat e PTT podem ser remapeados para combinacoes suportadas pelo NavBR, e conflitos com Inputs\\keyboard.cfg sao bloqueados."),
    ("6. Personagem / RP",
     "Modo experimental. Entre em um mapa, atualize o catalogo de Drivers, selecione um personagem e entao saia do onibus. "
     "W/S movimentam, A/D giram, Shift corre, E retorna ao onibus e Esc executa retorno de emergencia."),
    ("7. Rede da empresa",
     "A Rede da empresa gerencia identidade, equipe, convites e Company Node. "
     "O Company Node usa TCP 27740 e e independente da sala multiplayer TCP 27730."),
    ("8. CCO",
     "O Centro de Controle Operacional acompanha operacao local, motoristas remotos, ocorrencias, atrasos, telemetria e empresa/frota."),
    ("9. Hardware Cockpit",
     "Bridge serial para Arduino, ESP32, letreiros, LEDs e computadores de bordo. "
     "Selecione explicitamente a porta COM e o baud rate. O auto-reconnect tenta somente a porta escolhida."),
    ("10. HUD",
     "Em Configuracoes > HUD voce escolhe preset, aparencia, modulos, posicao e acoes. "
     "Na Alpha.22 o overlay fica associado a janela de gameplay do OMSI e nao deve permanecer sobre outras aplicacoes."),
    ("11. NavBR TP/TS",
     "A tecla K abre a configuracao operacional. Ctrl+Alt+F6 mostra/oculta o TP/TS, "
     "Ctrl+Alt+F7 alterna o tema, Ctrl+Alt+F8 alterna o tamanho e Ctrl+Alt+H mostra/oculta o HUD completo."),
    ("12. Onibus remoto fisico",
     "Recurso experimental e opt-in. O cenario online com bots/AI do simulador seguindo o host foi validado no OMSI real. "
     "Ainda falta validar player A e player B em dois PCs reais, nos dois sentidos, incluindo troca de Kachel, curvas, cruzamentos, reconexao e estado visual."),
]

for title, text in sections:
    story.extend([
        Paragraph(title, styles["NavH1"]),
        Paragraph(text, styles["NavBody"]),
        Spacer(1, 2*mm),
    ])

story.extend([PageBreak(), Paragraph("13. Atalhos de teclado", styles["NavH1"])])

story.append(info_table([
    ["Atalho", "Funcao"],
    ["F9", "Abrir chat (padrao/configuravel)"],
    ["F10", "Push-to-Talk (padrao/configuravel)"],
    ["Enter", "Enviar mensagem no chat"],
    ["Esc", "Fechar chat sem enviar"],
    ["K", "Abrir/fechar configuracao NavBR TP/TS"],
    ["Ctrl+Alt+H", "Mostrar/ocultar HUD completo"],
    ["Ctrl+Alt+F6", "Mostrar/ocultar TP/TS"],
    ["Ctrl+Alt+F7", "Alternar tema do TP/TS"],
    ["Ctrl+Alt+F8", "Alternar tamanho do TP/TS"],
    ["W / A / S / D", "Movimento no Personagem/RP"],
    ["Shift", "Correr no Personagem/RP"],
    ["E", "Voltar/entrar no onibus (ate 8 m)"],
    ["Esc", "Retorno de emergencia no Personagem/RP"],
]))

story.extend([
    Spacer(1, 6*mm),
    Paragraph(
        "Chat e PTT podem usar F1-F4 ou F9-F12 com nenhum modificador, Shift, Ctrl ou Ctrl+Shift. "
        "F5-F8 ficam fora do seletor por serem teclas frequentemente usadas pelo OMSI.",
        styles["NavBody"],
    ),
    PageBreak(),
    Paragraph("14. Diagnostico e logs", styles["NavH1"]),
    Paragraph("Pasta padrao: <b>%LOCALAPPDATA%\\OMSI NavBR Multiplayer</b>.", styles["NavBody"]),
    info_table([
        ["Arquivo", "Uso"],
        ["navbr.log", "Cliente, sessao e diagnosticos gerais."],
        ["navbr-plugin.log", "Plugin OMSI e backend fisico."],
        ["navbr-route.log", "Resolucao de rota e geometria."],
        ["navbr-error.log", "Excecoes nao tratadas."],
    ]),
    Spacer(1, 7*mm),
    Paragraph(
        "Ao reportar um problema, informe versao do NavBR e do OMSI, mapa, onibus, linha/rota, "
        "estado do plugin, recurso experimental ativo e os logs relevantes.",
        styles["NavBody"],
    ),
    Spacer(1, 12*mm),
    Paragraph("OMSI NavBR Multiplayer", ParagraphStyle(
        "EndTitle", parent=styles["NavH1"], alignment=TA_CENTER, textColor=colors.HexColor("#149EE8")
    )),
    Paragraph(
        "Projeto independente para OMSI 2. Alpha.22.",
        ParagraphStyle("EndSmall", parent=styles["NavSmall"], alignment=TA_CENTER),
    ),
])

doc.build(story, onFirstPage=footer, onLaterPages=footer)
print(f"Generated {OUT}")
