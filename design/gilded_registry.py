# -*- coding: utf-8 -*-
"""Gilded Registry — visual presentation plates for the CAPU graduation project."""
import math
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib.colors import HexColor

import os
FONTS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fonts")
pdfmetrics.registerFont(TTFont("Gloock", FONTS + r"\Gloock-Regular.ttf"))
pdfmetrics.registerFont(TTFont("Crimson", FONTS + r"\CrimsonPro-Regular.ttf"))
pdfmetrics.registerFont(TTFont("CrimsonIt", FONTS + r"\CrimsonPro-Italic.ttf"))
pdfmetrics.registerFont(TTFont("Mono", FONTS + r"\IBMPlexMono-Regular.ttf"))

W, H = 960, 540
NAVY   = HexColor("#101442")
NAVY2  = HexColor("#1A1F5E")
NIGHT  = HexColor("#0B0E33")
GOLD   = HexColor("#C9A84C")
GOLDDIM= HexColor("#8A7434")
BONE   = HexColor("#F2EAD3")
ICE    = HexColor("#8E97C4")

OUT = r"D:\_\Projects\capu-portal\design\CAPU_Gilded_Registry.pdf"
c = canvas.Canvas(OUT, pagesize=(W, H))

def bg(col=NAVY):
    c.setFillColor(col); c.rect(0, 0, W, H, stroke=0, fill=1)

def edge_dark():
    # slow deepening of night at page edges
    c.setFillColor(NIGHT)
    c.setFillAlpha(0.35); c.rect(0, 0, W, 14, stroke=0, fill=1)
    c.rect(0, H-14, W, 14, stroke=0, fill=1)
    c.setFillAlpha(1)

def regmarks(col=GOLDDIM):
    c.setStrokeColor(col); c.setLineWidth(0.6)
    m, s = 26, 8
    for x, y, dx, dy in ((m,m,1,1),(W-m,m,-1,1),(m,H-m,1,-1),(W-m,H-m,-1,-1)):
        c.line(x, y, x+dx*s, y); c.line(x, y, x, y+dy*s)

def label(x, y, txt, size=6.4, col=GOLD, align="l", track=1.6):
    txt = txt.upper()
    c.setFillColor(col); c.setFont("Mono", size)
    w = c.stringWidth(txt, "Mono", size) + track*(len(txt)-1)
    sx = x - w/2 if align == "c" else (x - w if align == "r" else x)
    t = c.beginText(); t.setTextOrigin(sx, y); t.setCharSpace(track)
    t.textOut(txt); t.setCharSpace(0)          # Tc persists in graphics state
    c.drawText(t)

def inscription(x, y, txt, size, col=BONE, track=6, font="Gloock", align="c"):
    c.setFillColor(col); c.setFont(font, size)
    w = c.stringWidth(txt, font, size) + track*(len(txt)-1)
    sx = x - w/2 if align == "c" else x
    t = c.beginText(); t.setTextOrigin(sx, y); t.setCharSpace(track)
    t.textOut(txt); t.setCharSpace(0)
    c.drawText(t)

def folio(n, title):
    label(W/2, 22, f"plate {n} · {title}", 6.0, GOLDDIM, "c")
    c.setStrokeColor(GOLDDIM); c.setLineWidth(0.5)
    c.line(W/2-90, 32, W/2+90, 32)

def ring(cx, cy, r, lw=0.7, col=GOLD, alpha=1):
    c.setStrokeColor(col); c.setLineWidth(lw); c.setStrokeAlpha(alpha)
    c.circle(cx, cy, r, stroke=1, fill=0); c.setStrokeAlpha(1)

def dotring(cx, cy, r, n, dr=1.1, col=GOLD, alpha=1, phase=0.0):
    c.setFillColor(col); c.setFillAlpha(alpha)
    for i in range(n):
        a = phase + 2*math.pi*i/n
        c.circle(cx + r*math.cos(a), cy + r*math.sin(a), dr, stroke=0, fill=1)
    c.setFillAlpha(1)

# ───────────────────────────── PLATE I · COVER ─────────────────────────────
bg(); edge_dark()
# great seal, offset right
cx, cy = 724, 280
for r, lw, al in ((196,0.5,.4),(180,1.0,.9),(168,0.5,.55),(140,0.7,.7),(124,0.4,.45),(96,0.9,.85),(58,0.5,.5)):
    ring(cx, cy, r, lw, GOLD, al)
dotring(cx, cy, 154, 96, 0.9, GOLD, .8)
dotring(cx, cy, 110, 48, 1.2, GOLD, .9)
dotring(cx, cy, 76, 24, 1.0, GOLD, .6, phase=.13)
inscription(cx, cy+16, "CU", 64, GOLD, 10)
label(cx, cy-22, "est. registry of record", 5.4, GOLDDIM, "c")
label(cx, cy-196-16, "fig. i — the seal", 5.6, GOLDDIM, "c")
# left inscription block
c.setStrokeColor(GOLD); c.setLineWidth(1.1); c.line(78, 392, 78, 158)
inscription(92, 330, "THE", 21, ICE, 8, align="l")
inscription(92, 282, "STUDENT", 44, BONE, 6, align="l")
inscription(92, 234, "PORTAL", 44, BONE, 6, align="l")
c.setFont("CrimsonIt", 13.5); c.setFillColor(GOLD)
c.drawString(93, 198, "a unified academic information system")
label(93, 168, "capital university · graduation project · 2026", 6.4, ICE)
label(93, 154, "faculty examination copy · §1.0", 6.0, GOLDDIM)
regmarks()
c.showPage()

# ─────────────────────── PLATE II · CONTENTS AS LEDGER ─────────────────────
bg(); edge_dark(); regmarks()
inscription(W/2, H-86, "CONTENTS", 26, BONE, 12)
c.setStrokeColor(GOLDDIM); c.setLineWidth(0.5); c.line(W/2-110, H-104, W/2+110, H-104)
sections = [
    ("I","INTRODUCTION","problem & objectives"),
    ("II","LITERATURE","related work"),
    ("III","ARCHITECTURE","modular monolith"),
    ("IV","DOMAIN","cqrs · hierarchy · dag"),
    ("V","AUTHORITY","scope-first access"),
    ("VI","SYNCHRONY","sis integration"),
    ("VII","INTERFACE","two portals · rtl"),
    ("VIII","EVALUATION","scale & conclusion"),
]
n = len(sections); x0, x1 = 110, W-110
for i, (rn, name, sub) in enumerate(sections):
    x = x0 + (x1-x0)*i/(n-1)
    h = 150 + 36*math.sin(math.pi*i/(n-1))          # gentle arch of columns
    c.setStrokeColor(GOLD); c.setLineWidth(1.0)
    c.line(x, 120, x, 120+h)
    c.setLineWidth(0.5); c.setStrokeAlpha(0.5)
    c.line(x-7, 120, x+7, 120); c.line(x-5, 120+h, x+5, 120+h)
    c.setStrokeAlpha(1)
    dotring(x, 120+h+14, 6, 12, 0.7, GOLD, .85)
    inscription(x, 120+h+34, rn, 13, GOLD, 2.5)
    label(x, 104, name, 6.6, BONE, "c")
    label(x, 92, sub, 5.4, ICE, "c", 0.8)
folio("ii", "order of proceedings")
c.showPage()

# ─────────────────── PLATE III · ARCHITECTURE OF THE ARCHIVE ────────────────
bg(); edge_dark(); regmarks()
inscription(W/2, H-72, "ONE BUILDING, MANY ROOMS", 19, BONE, 7)
label(W/2, H-92, "fig. ii — the modular monolith", 6.0, GOLDDIM, "c")
bx, by, bw, bh = 200, 120, 560, 300
c.setStrokeColor(GOLD); c.setLineWidth(1.3); c.rect(bx, by, bw, bh, stroke=1, fill=0)
c.setLineWidth(0.5); c.setStrokeAlpha(0.5); c.rect(bx-8, by-8, bw+16, bh+16, stroke=1, fill=0); c.setStrokeAlpha(1)
rooms = [("IDENTITY",0,0,2,1),("ACADEMIC",2,0,2,2),("TREASURY",0,1,2,1),
         ("REQUESTS",4,0,2,1),("NOTIFICATIONS",4,1,2,1),("SYNCHRONY",0,2,3,1),("DASHBOARDS",3,2,3,1)]
cw, ch = bw/6, bh/3
for name, gx, gy, gw, gh in rooms:
    x, y = bx+gx*cw, by+bh-(gy+gh)*ch
    c.setStrokeColor(GOLD); c.setLineWidth(0.6); c.setStrokeAlpha(0.8)
    c.rect(x+5, y+5, gw*cw-10, gh*ch-10, stroke=1, fill=0); c.setStrokeAlpha(1)
    label(x+gw*cw/2, y+gh*ch/2-2, name, 6.2, BONE, "c")
    dotring(x+gw*cw/2, y+gh*ch/2+13, 4, 8, 0.55, GOLD, .7)
# foundation line + tiny piles
c.setStrokeColor(GOLD); c.setLineWidth(1.0); c.line(bx-40, by-18, bx+bw+40, by-18)
for i in range(56):
    x = bx-40 + (bw+80)*i/55
    c.setLineWidth(0.5); c.line(x, by-18, x-3, by-26)
label(W/2, by-42, "shared kernel · one database · one deployment", 6.0, ICE, "c")
label(bx-14, by+bh+18, "§3.1", 6.0, GOLDDIM, "r")
folio("iii", "system architecture")
c.showPage()

# ──────────────────── PLATE IV · RINGS OF AUTHORITY ────────────────────────
bg(); edge_dark(); regmarks()
cx, cy = W/2, 252
scopes = [("DOMAIN",178),("FACULTY",140),("PROGRAM",102),("CLASS",64)]
for i,(name,r) in enumerate(scopes):
    ring(cx, cy, r, 1.0 if i%2 else 0.6, GOLD, .9-.12*i)
    dotring(cx, cy, r, 12+18*i, 0.8, GOLD, .55, phase=i*.21)
    label(cx, cy+r+7, name, 5.8, BONE, "c")
c.setFillColor(GOLD); c.circle(cx, cy, 17, stroke=0, fill=1)
c.setFillColor(NAVY); c.setFont("Gloock", 13); c.drawCentredString(cx, cy-4.5, "U")
# radial grant line: authority descends inward
c.setStrokeColor(BONE); c.setLineWidth(0.8)
a = math.radians(207)
c.line(cx+198*math.cos(a), cy+198*math.sin(a), cx+19*math.cos(a), cy+19*math.sin(a))
for rr in (178,140,102,64):
    c.setFillColor(BONE); c.circle(cx+rr*math.cos(a), cy+rr*math.sin(a), 2.2, stroke=0, fill=1)
label(cx+214*math.cos(a), cy+214*math.sin(a), "one grant", 5.8, BONE, "r")
inscription(W/2, H-72, "AUTHORITY IS A PLACE", 19, BONE, 7)
label(W/2, H-92, "fig. iii — scope-first authorization · permission × scope", 6.0, GOLDDIM, "c")
label(W-78, 64, "module.entity.action @ node", 6.0, ICE, "r", 1.2)
folio("iv", "the authorization model")
c.showPage()

# ─────────────────── PLATE V · THE ORDER OF KNOWLEDGE (DAG) ─────────────────
bg(); edge_dark(); regmarks()
inscription(W/2, H-72, "NOTHING BEFORE ITS TIME", 19, BONE, 7)
label(W/2, H-92, "fig. iv — prerequisite graph · acyclic by proof", 6.0, GOLDDIM, "c")
cols = [1,2,3,2,1]; nodes = {}
x0 = 180
for ci, cnt in enumerate(cols):
    x = x0 + ci*150
    for ni in range(cnt):
        y = 250 + (ni-(cnt-1)/2)*120
        nodes[(ci,ni)] = (x,y)
edges = [((0,0),(1,0)),((0,0),(1,1)),((1,0),(2,0)),((1,0),(2,1)),((1,1),(2,1)),
         ((1,1),(2,2)),((2,0),(3,0)),((2,1),(3,0)),((2,1),(3,1)),((2,2),(3,1)),
         ((3,0),(4,0)),((3,1),(4,0))]
c.setStrokeColor(GOLD); c.setLineWidth(0.6); c.setStrokeAlpha(0.75)
for a_, b_ in edges:
    (xa,ya),(xb,yb) = nodes[a_], nodes[b_]
    c.line(xa+15, ya, xb-15, yb)
c.setStrokeAlpha(1)
lvl_names = ["100","200","300","400","capstone"]
for (ci,ni),(x,y) in nodes.items():
    ring(x, y, 15, 0.9, GOLD); ring(x, y, 11, 0.4, GOLD, .5)
    c.setFillColor(NAVY2); c.circle(x, y, 10.5, stroke=0, fill=1)
    c.setFillColor(BONE); c.setFont("Mono", 6); c.drawCentredString(x, y-2, f"{(ci+1)*100+ni}")
for ci, name in enumerate(lvl_names):
    label(x0+ci*150, 96, f"lvl {name}", 5.6, ICE, "c")
    c.setStrokeColor(GOLDDIM); c.setLineWidth(0.4)
    c.line(x0+ci*150, 108, x0+ci*150, 118)
# forbidden cycle, struck through
fx, fy = 812, 392
ring(fx, fy, 26, 0.6, GOLDDIM, .8)
c.setStrokeColor(HexColor("#C62828")); c.setLineWidth(1.2)
c.line(fx-19, fy-19, fx+19, fy+19); c.line(fx-19, fy+19, fx+19, fy-19)
label(fx, fy-42, "cycle: refused", 5.6, ICE, "c")
label(W-78, 64, "depth-first search · O(V+E)", 6.0, ICE, "r", 1.2)
folio("v", "curriculum as graph")
c.showPage()

# ─────────────────── PLATE VI · THE TWO LEDGERS (SYNC) ──────────────────────
bg(); edge_dark(); regmarks()
inscription(W/2, H-72, "TWO LEDGERS, ONE TRUTH", 19, BONE, 7)
label(W/2, H-92, "fig. v — synchronization with the system of record", 6.0, GOLDDIM, "c")
lx, rx, cy2, R = 280, 680, 252, 110
for cx_, name, sub in ((lx,"SIS","system of record"),(rx,"PORTAL","system of engagement")):
    ring(cx_, cy2, R, 1.1, GOLD, .95); ring(cx_, cy2, R-9, 0.4, GOLD, .45)
    dotring(cx_, cy2, R-22, 60, 0.7, GOLD, .5)
    inscription(cx_, cy2+6, name, 22, BONE, 5)
    label(cx_, cy2-16, sub, 5.6, ICE, "c")
# flow channel
for i in range(9):
    y = cy2-32+i*8
    c.setStrokeColor(GOLD); c.setLineWidth(0.55); c.setStrokeAlpha(0.75)
    c.line(lx+R+8, y, rx-R-8, y)
c.setStrokeAlpha(1)
for i in range(7):
    x = lx+R+26 + i*((rx-R-26)-(lx+R+26))/6
    c.setFillColor(GOLD); c.circle(x, cy2+4, 1.8, stroke=0, fill=1)
label(W/2, cy2+56, "pull · upsert · idempotent", 5.4, BONE, "c", 1.0)
label(W/2, cy2-58, "retry · backoff · checkpoint", 5.4, ICE, "c", 1.0)
# outbox tallies beneath
ty = 116
for i in range(72):
    x = 200 + i*7.8
    c.setStrokeColor(GOLD if i%9 else BONE); c.setLineWidth(0.6)
    c.line(x, ty, x, ty+10 if i%9 else ty+14)
label(W/2, ty-16, "transactional outbox — every change recorded before it travels", 5.6, GOLDDIM, "c")
folio("vi", "the synchronization platform")
c.showPage()

# ─────────────────── PLATE VII · THE CENSUS (EVALUATION) ────────────────────
bg(BONE)
c.setFillColor(NAVY); c.rect(0, 0, W, 14, stroke=0, fill=1); c.rect(0, H-14, W, 14, stroke=0, fill=1)
regmarks(NAVY)
c.setFillColor(NAVY)
inscription(W/2, H-72, "THE CENSUS", 22, NAVY, 9)
c.setStrokeColor(NAVY); c.setLineWidth(0.5); c.line(W/2-86, H-90, W/2+86, H-90)
label(W/2, H-104, "fig. vi — one mark, one record · seeded at full scale", 6.0, GOLDDIM, "c")
import random as _r; _r.seed(7)
gx0, gy0, cols_, rows_ = 130, 120, 70, 22
for r_ in range(rows_):
    for k in range(cols_):
        x = gx0 + k*10; y = gy0 + r_*12
        v = _r.random()
        if v > 0.985:
            c.setFillColor(GOLD); c.circle(x, y, 2.6, stroke=0, fill=1)
        else:
            c.setFillColor(NAVY); c.setFillAlpha(0.55 if v > .5 else .8)
            c.rect(x-1.1, y-3.2, 2.2, 6.4, stroke=0, fill=1); c.setFillAlpha(1)
stats = [("students","12,400"),("registrations","118,000"),("grade entries","96,500"),("api p95","<180 ms")]
sx = 156
for name, val in stats:
    c.setFillColor(NAVY); c.setFont("Gloock", 15); c.drawString(sx, 84, val)
    label(sx+1, 70, name, 5.8, GOLDDIM)
    sx += 180
folio_col = GOLDDIM
label(W/2, 36, "plate vii · evaluation & scale", 6.0, folio_col, "c")
c.showPage()

# ──────────────────────── PLATE VIII · COLOPHON ─────────────────────────────
bg(NIGHT); regmarks()
cx, cy = W/2, 300
for r, lw, al in ((110,0.8,.9),(96,0.4,.5),(72,0.6,.7),(40,0.4,.5)):
    ring(cx, cy, r, lw, GOLD, al)
dotring(cx, cy, 84, 36, 0.8, GOLD, .7)
inscription(cx, cy-8, "FINIS", 17, BONE, 9)
c.setFont("CrimsonIt", 13); c.setFillColor(GOLD)
c.drawCentredString(cx, 152, "what a university knows, made navigable")
label(cx, 124, "capital university · student portal · mmxxvi", 6.0, ICE, "c")
label(cx, 110, "submitted in partial fulfilment · examined with gratitude", 5.4, GOLDDIM, "c")
c.showPage()

c.save()
print("OK", OUT)
