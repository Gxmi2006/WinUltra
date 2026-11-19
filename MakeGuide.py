# Builds WinUltra-Guide.pdf: a short, picture-led guide for friends. Run: python MakeGuide.py
# Screenshots live in guide-images\.
import os
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.lib.colors import HexColor, white
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_CENTER
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfbase.pdfmetrics import registerFontFamily
from reportlab.platypus import (BaseDocTemplate, Frame, PageTemplate, Paragraph, Spacer, Table, TableStyle,
                                Image, KeepTogether, PageBreak)
from reportlab.lib.utils import ImageReader

HERE = os.path.dirname(os.path.abspath(__file__))
IMG = os.path.join(HERE, "guide-images")
FONTS = r"C:\Windows\Fonts"
pdfmetrics.registerFont(TTFont("Segoe", os.path.join(FONTS, "segoeui.ttf")))
pdfmetrics.registerFont(TTFont("Segoe-SB", os.path.join(FONTS, "seguisb.ttf")))
pdfmetrics.registerFont(TTFont("Segoe-B", os.path.join(FONTS, "segoeuib.ttf")))
pdfmetrics.registerFont(TTFont("Segoe-L", os.path.join(FONTS, "segoeuil.ttf")))
registerFontFamily("Segoe", normal="Segoe", bold="Segoe-SB", italic="Segoe", boldItalic="Segoe-SB")

INK = HexColor("#1B1D2E")
DIM = HexColor("#5C6073")
ACCENT = HexColor("#7A5CFF")
CARD = HexColor("#F3F2FA")
DARK = HexColor("#14152A")
NIGHT = HexColor("#2E3366")

body = ParagraphStyle("body", fontName="Segoe", fontSize=10, leading=14.5, textColor=INK)
caption = ParagraphStyle("caption", parent=body, fontSize=9.2, leading=13, textColor=DIM, alignment=TA_CENTER)
h2 = ParagraphStyle("h2", fontName="Segoe-B", fontSize=15, leading=19, textColor=INK, spaceBefore=12, spaceAfter=5)
step_num = ParagraphStyle("num", fontName="Segoe-B", fontSize=18, leading=20, textColor=ACCENT)
step_txt = ParagraphStyle("st", parent=body, fontSize=9.6, leading=13.5)
tip = ParagraphStyle("tip", parent=body, leftIndent=12, bulletIndent=0, spaceAfter=2)

W, H = A4
MARGIN = 17 * mm
CONTENT = W - 2 * MARGIN


def header(c, doc):
    c.saveState()
    band = 46 * mm
    c.setFillColor(DARK)
    c.rect(0, H - band, W, band, stroke=0, fill=1)
    for i, a in enumerate([0.12, 0.08, 0.05, 0.03]):
        c.setFillColor(ACCENT, alpha=a)
        c.circle(W - 35 * mm, H - 8 * mm, 50 * mm + i * 13 * mm, stroke=0, fill=1)
    c.setFillAlpha(1)
    c.drawImage(os.path.join(HERE, "WinUltra.png"), MARGIN, H - band + 11 * mm, 24 * mm, 24 * mm, mask="auto")
    c.setFillColor(white)
    c.setFont("Segoe-B", 28)
    c.drawString(MARGIN + 30 * mm, H - 24 * mm, "WinUltra")
    c.setFont("Segoe-L", 12)
    c.setFillColor(HexColor("#C9C6E8"))
    c.drawString(MARGIN + 30 * mm, H - 32 * mm, "A glass Dynamic Island for Windows 11.")
    c.restoreState()
    footer(c, doc)


def footer(c, doc):
    c.saveState()
    c.setFont("Segoe", 8)
    c.setFillColor(DIM)
    c.drawString(MARGIN, 9 * mm, "WinUltra quick guide")
    c.drawRightString(W - MARGIN, 9 * mm, str(doc.page))
    c.restoreState()


def picture(name, width):
    path = os.path.join(IMG, name)
    w, h = ImageReader(path).getSize()
    return Image(path, width=width, height=width * h / w)


def shot(name, text, width):
    """A screenshot with a short caption under it."""
    return [picture(name, width), Spacer(1, 3), Paragraph(text, caption)]


def side_by_side(a, b):
    col = (CONTENT - 6 * mm) / 2
    t = Table([[shot(*a, width=col), shot(*b, width=col)]], colWidths=[col + 6 * mm, col])
    t.setStyle(TableStyle([("VALIGN", (0, 0), (-1, -1), "TOP"), ("LEFTPADDING", (0, 0), (-1, -1), 0),
                           ("RIGHTPADDING", (0, 0), (0, -1), 6 * mm), ("RIGHTPADDING", (1, 0), (1, -1), 0),
                           ("TOPPADDING", (0, 0), (-1, -1), 0), ("BOTTOMPADDING", (0, 0), (-1, -1), 8)]))
    return t


def steps(items):
    """Numbered steps in soft cards, side by side."""
    n = len(items)
    col = (CONTENT - (n - 1) * 4 * mm) / n
    cells = []
    for i, text in enumerate(items):
        inner = Table([[Paragraph(str(i + 1), step_num), Paragraph(text, step_txt)]], colWidths=[9 * mm, col - 9 * mm - 16])
        inner.setStyle(TableStyle([("VALIGN", (0, 0), (-1, -1), "TOP"), ("LEFTPADDING", (0, 0), (-1, -1), 0),
                                   ("RIGHTPADDING", (0, 0), (-1, -1), 0), ("TOPPADDING", (0, 0), (-1, -1), 0)]))
        cells.append(inner)
    widths = []
    for i in range(n):
        widths.append(col + (4 * mm if i < n - 1 else 0))
    t = Table([cells], colWidths=widths)
    style = [("VALIGN", (0, 0), (-1, -1), "TOP"), ("TOPPADDING", (0, 0), (-1, -1), 9), ("BOTTOMPADDING", (0, 0), (-1, -1), 9)]
    for i in range(n):
        style += [("BACKGROUND", (i, 0), (i, 0), CARD), ("LEFTPADDING", (i, 0), (i, 0), 8),
                  ("RIGHTPADDING", (i, 0), (i, 0), 8 + (4 * mm if i < n - 1 else 0))]
    t.setStyle(TableStyle(style))
    return t


story = []

# ---------------------------------------------------------------- page 1
story.append(Paragraph("Install it in 30 seconds", h2))
story.append(steps([
    "Double-click <b>WinUltra.exe</b>.",
    "If Windows says <b>\u201cWindows protected your PC\u201d</b>, click <b>More info</b>, then <b>Run anyway</b>.",
    "That's it. It starts with Windows from now on, and there's a shortcut on your desktop.",
]))

story.append(Paragraph("The island", h2))
line_row = Table([[picture("line.png", 52 * mm),
                   Paragraph("This glowing line at the top of your screen is the island. <b>Move your mouse onto it</b> "
                             "(or press <b>Ctrl+Alt+Space</b>) and it opens. Move away and it closes. "
                             "To switch tabs, just <b>rest the mouse on one</b>.", body)]],
                 colWidths=[60 * mm, CONTENT - 60 * mm])
line_row.setStyle(TableStyle([("VALIGN", (0, 0), (-1, -1), "MIDDLE"), ("LEFTPADDING", (0, 0), (-1, -1), 0)]))
story.append(line_row)
story.append(Spacer(1, 8))
story.append(side_by_side(
    ("tab-media.png", "<b>Media</b>: whatever is playing in Spotify, YouTube or any player. Pause or skip from here."),
    ("tab-controls.png", "<b>Controls</b>: tap a tile to turn it on or off. Drag the tall sliders up or down for brightness and volume.")))
story.append(side_by_side(
    ("tab-stats.png", "<b>Stats</b>: how busy your CPU and graphics card are, your internet speed and battery."),
    ("tab-timer.png", "<b>Timer</b>: tap a preset to start. <b>Focus 25</b> gives you 25 quiet minutes with no pop-ups.")))

# ---------------------------------------------------------------- page 2
story.append(PageBreak())
story.append(Paragraph("Little pop-ups", h2))
pop_row = Table([[picture("popup-caps.png", 70 * mm),
                  Paragraph("The island grows for a moment to tell you something: volume or brightness changes, "
                            "charger in or out, new notifications, earbuds connecting, Caps Lock, and a "
                            "<b>green or orange dot</b> when an app is using your camera or microphone.", body)]],
                colWidths=[76 * mm, CONTENT - 76 * mm])
pop_row.setStyle(TableStyle([("VALIGN", (0, 0), (-1, -1), "MIDDLE"), ("LEFTPADDING", (0, 0), (-1, -1), 0)]))
story.append(pop_row)

story.append(Paragraph("Handy extras", h2))
for text in [
    "<b>Copy history</b>: press <b>Ctrl+Alt+Shift+V</b> anywhere, then click something you copied earlier to paste it.",
    "<b>Shelf</b>: drag files onto the island to park them, and drag them out later to use them. Pin an app or folder to launch it in one click.",
    "<b>Tray icon</b> (bottom right, maybe under the <b>^</b>): open the island, stop starting with Windows, quit, or uninstall.",
]:
    story.append(Paragraph(text, tip, bulletText="\u2022"))

doc = BaseDocTemplate(os.path.join(HERE, "WinUltra-Guide.pdf"), pagesize=A4, title="WinUltra quick guide", author="WinUltra",
                      leftMargin=MARGIN, rightMargin=MARGIN, topMargin=MARGIN, bottomMargin=16 * mm)
first = Frame(MARGIN, 16 * mm, CONTENT, H - 46 * mm - 4 * mm - 16 * mm, id="first")
rest = Frame(MARGIN, 16 * mm, CONTENT, H - MARGIN - 16 * mm, id="rest")
doc.addPageTemplates([PageTemplate(id="First", frames=[first], onPage=header, autoNextPageTemplate="Rest"),
                      PageTemplate(id="Rest", frames=[rest], onPage=footer)])
doc.build(story)
print("WinUltra-Guide.pdf written")
