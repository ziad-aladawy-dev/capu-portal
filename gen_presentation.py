# -*- coding: utf-8 -*-
"""
Generates Graduation_Project_Presentation.pptx for the CapitalUniversity portal
graduation project. Academic structure: Introduction -> Problem Statement ->
Related Work -> Architecture -> Implementation -> Demo -> Evaluation -> Conclusion.

Usage:  py gen_presentation.py
"""

from pptx import Presentation
from pptx.util import Inches, Pt, Emu
from pptx.dml.color import RGBColor
from pptx.enum.text import PP_ALIGN, MSO_ANCHOR
from pptx.enum.shapes import MSO_SHAPE

# ---------------------------------------------------------------- palette ----
NAVY      = RGBColor(0x1A, 0x1F, 0x5E)   # brand primary (frontend tokens.css)
NAVY_DEEP = RGBColor(0x10, 0x14, 0x42)   # dark slide background
GOLD      = RGBColor(0xC9, 0xA8, 0x4C)   # brand accent
GOLD_SOFT = RGBColor(0xF2, 0xEA, 0xD3)   # pale gold tint
ICE       = RGBColor(0xEE, 0xF1, 0xFA)   # card background on light slides
ICE_LINE  = RGBColor(0xD5, 0xDB, 0xEF)
WHITE     = RGBColor(0xFF, 0xFF, 0xFF)
INK       = RGBColor(0x23, 0x26, 0x38)   # body text
MUTED     = RGBColor(0x5C, 0x61, 0x78)   # secondary text
ICE_DARK  = RGBColor(0x2A, 0x30, 0x73)   # card background on dark slides

HEAD_FONT = "Cambria"
BODY_FONT = "Calibri"

SW, SH = Inches(13.333), Inches(7.5)

prs = Presentation()
prs.slide_width = SW
prs.slide_height = SH
BLANK = prs.slide_layouts[6]


# ---------------------------------------------------------------- helpers ----
def new_slide(bg=WHITE):
    s = prs.slides.add_slide(BLANK)
    s.background.fill.solid()
    s.background.fill.fore_color.rgb = bg
    return s


def textbox(slide, x, y, w, h, anchor=MSO_ANCHOR.TOP):
    tb = slide.shapes.add_textbox(x, y, w, h)
    tf = tb.text_frame
    tf.word_wrap = True
    tf.margin_left = tf.margin_right = tf.margin_top = tf.margin_bottom = 0
    tf.vertical_anchor = anchor
    return tf


def para(tf, text, size=15, bold=False, color=INK, font=BODY_FONT,
         align=PP_ALIGN.LEFT, space_after=6, space_before=0, first=False,
         italic=False):
    p = tf.paragraphs[0] if first and not tf.paragraphs[0].runs else tf.add_paragraph()
    p.alignment = align
    p.space_after = Pt(space_after)
    p.space_before = Pt(space_before)
    r = p.add_run()
    r.text = text
    f = r.font
    f.name = font
    f.size = Pt(size)
    f.bold = bold
    f.italic = italic
    f.color.rgb = color
    return p


def bullets(tf, items, size=15, color=INK, gap=8, first=True):
    for i, it in enumerate(items):
        if isinstance(it, tuple):           # (lead, rest) -> bold lead
            p = tf.paragraphs[0] if (first and i == 0) else tf.add_paragraph()
            p.space_after = Pt(gap)
            r1 = p.add_run(); r1.text = "•  " + it[0]
            r1.font.name = BODY_FONT; r1.font.size = Pt(size)
            r1.font.bold = True; r1.font.color.rgb = color
            r2 = p.add_run(); r2.text = " " + it[1]
            r2.font.name = BODY_FONT; r2.font.size = Pt(size)
            r2.font.color.rgb = color
        else:
            para(tf, "•  " + it, size=size, color=color,
                 space_after=gap, first=(first and i == 0))


def shape(slide, kind, x, y, w, h, fill, line=None, line_w=None, shadow=False):
    sp = slide.shapes.add_shape(kind, x, y, w, h)
    sp.fill.solid()
    sp.fill.fore_color.rgb = fill
    if line is None:
        sp.line.fill.background()
    else:
        sp.line.color.rgb = line
        sp.line.width = line_w or Pt(1)
    sp.shadow.inherit = False
    return sp


def card(slide, x, y, w, h, fill=ICE):
    return shape(slide, MSO_SHAPE.ROUNDED_RECTANGLE, x, y, w, h, fill)


def set_shape_text(sp, lines, anchor=MSO_ANCHOR.MIDDLE,
                   margin=Inches(0.12)):
    """lines: list of (text, size, bold, color, font) tuples."""
    tf = sp.text_frame
    tf.word_wrap = True
    tf.vertical_anchor = anchor
    tf.margin_left = tf.margin_right = margin
    tf.margin_top = tf.margin_bottom = Inches(0.06)
    for i, (txt, size, bold, color, font) in enumerate(lines):
        p = tf.paragraphs[0] if i == 0 else tf.add_paragraph()
        p.alignment = PP_ALIGN.CENTER
        p.space_after = Pt(2)
        r = p.add_run(); r.text = txt
        f = r.font
        f.name = font; f.size = Pt(size); f.bold = bold; f.color.rgb = color


def badge(slide, x, y, d, glyph, fill=GOLD, glyph_color=NAVY_DEEP, size=15):
    c = shape(slide, MSO_SHAPE.OVAL, x, y, d, d, fill)
    set_shape_text(c, [(glyph, size, True, glyph_color, BODY_FONT)],
                   margin=Inches(0.02))
    return c


def header(slide, kicker, title, dark=False):
    tcol = WHITE if dark else NAVY
    kcol = GOLD
    tf = textbox(slide, Inches(0.65), Inches(0.42), Inches(12.0), Inches(1.25))
    para(tf, kicker.upper(), size=12, bold=True, color=kcol, font=BODY_FONT,
         space_after=4, first=True)
    para(tf, title, size=33, bold=True, color=tcol, font=HEAD_FONT,
         space_after=0)


def footer(slide, n, dark=False):
    tf = textbox(slide, Inches(0.65), Inches(7.08), Inches(12.0), Inches(0.32))
    para(tf, f"Capital University Student Portal  ·  Graduation Project  ·  {n:02d}",
         size=9, color=(ICE_LINE if dark else MUTED), first=True)


def arrow_down(slide, cx, y, h=Inches(0.26), w=Inches(0.34), fill=GOLD):
    shape(slide, MSO_SHAPE.DOWN_ARROW, cx - w / 2, y, w, h, fill)


def arrow_lr(slide, x, cy, w=Inches(0.55), h=Inches(0.3), fill=GOLD):
    shape(slide, MSO_SHAPE.LEFT_RIGHT_ARROW, x, cy - h / 2, w, h, fill)


PAGE = [0]
def page():
    PAGE[0] += 1
    return PAGE[0]


# ============================================================ 1 · TITLE ======
s = new_slide(NAVY_DEEP)
badge(s, Inches(6.067), Inches(1.0), Inches(1.2), "CU", size=30)
tf = textbox(s, Inches(1.2), Inches(2.55), Inches(10.93), Inches(2.1))
para(tf, "Capital University Student Portal", size=46, bold=True, color=WHITE,
     font=HEAD_FONT, align=PP_ALIGN.CENTER, space_after=10, first=True)
para(tf, "A Modular Enterprise Platform for Managing the Academic Lifecycle",
     size=19, color=GOLD, align=PP_ALIGN.CENTER, italic=True)
tf = textbox(s, Inches(1.2), Inches(5.0), Inches(10.93), Inches(1.7))
para(tf, "Graduation Project — Faculty of Computer Science & Engineering",
     size=15, color=WHITE, align=PP_ALIGN.CENTER, space_after=4, first=True)
para(tf, "Presented by: [Student Name]      Supervised by: [Supervisor Name]",
     size=14, color=ICE_LINE, align=PP_ALIGN.CENTER, space_after=4)
para(tf, "Academic Year 2025 / 2026", size=13, color=ICE_LINE,
     align=PP_ALIGN.CENTER)
page()

# ============================================================ 2 · AGENDA =====
s = new_slide()
header(s, "Overview", "Agenda")
items = [
    ("01", "Introduction", "Context, stakeholders, and project scope"),
    ("02", "Problem Statement", "Gaps in current university information systems"),
    ("03", "Related Work", "Commercial SIS, LMS platforms, and in-house systems"),
    ("04", "System Architecture", "Modular monolith, layering, and plug-in modules"),
    ("05", "Security & Data Model", "Scope-first authorization and hierarchy modeling"),
    ("06", "Implementation", "Backend, frontend, and synchronization platform"),
    ("07", "Demonstration", "Administrative and student-facing walkthroughs"),
    ("08", "Evaluation & Conclusion", "Testing strategy, results, and future work"),
]
x0s = [Inches(0.65), Inches(6.95)]
for i, (num, t, d) in enumerate(items):
    col, row = divmod(i, 4)
    x = x0s[col]
    y = Inches(2.0) + row * Inches(1.22)
    badge(s, x, y + Inches(0.08), Inches(0.52), num, size=13)
    tf = textbox(s, x + Inches(0.75), y, Inches(5.0), Inches(1.1))
    para(tf, t, size=17, bold=True, color=NAVY, space_after=2, first=True)
    para(tf, d, size=12.5, color=MUTED, space_after=0)
footer(s, page())

# ====================================================== 3 · INTRODUCTION =====
s = new_slide()
header(s, "Section 1", "Introduction")
tf = textbox(s, Inches(0.65), Inches(2.0), Inches(6.7), Inches(4.6))
bullets(tf, [
    ("Domain.", "Universities coordinate a complex academic lifecycle: "
     "admission, course registration, scheduling, grading, fees, and "
     "student services."),
    ("Stakeholders.", "Students, instructors, student-affairs staff, and "
     "administrators each require distinct views and authority over the "
     "same institutional data."),
    ("Goal.", "Deliver a single bilingual web platform with two coordinated "
     "portals — an administrative portal and a student portal — "
     "backed by one consistent domain model."),
    ("Approach.", "A modular monolith (.NET 9) exposing a REST API to a "
     "modular React 19 single-page application, synchronized with the "
     "university's existing student information system (SIS)."),
], size=15.5, gap=14)
stats = [("2", "Coordinated portals", "Administrative & student-facing"),
         ("4", "User roles", "Admin, affairs, instructor, student"),
         ("2", "Languages", "Arabic & English, full RTL"),
         ("7", "Domain modules", "Plug-in backend features")]
for i, (n, t, d) in enumerate(stats):
    col, row = divmod(i, 2)
    x = Inches(7.75) + col * Inches(2.55)
    y = Inches(2.05) + row * Inches(2.25)
    c = card(s, x, y, Inches(2.35), Inches(2.0))
    set_shape_text(c, [(n, 44, True, NAVY, HEAD_FONT),
                       (t, 13, True, INK, BODY_FONT),
                       (d, 10.5, False, MUTED, BODY_FONT)])
footer(s, page())

# ================================================= 4 · PROBLEM STATEMENT =====
s = new_slide()
header(s, "Section 2", "Problem Statement")
tf = textbox(s, Inches(0.65), Inches(1.95), Inches(5.6), Inches(4.7))
bullets(tf, [
    "Academic operations are typically spread across disconnected tools: "
    "spreadsheets, a legacy SIS, and paper-based service requests.",
    "Access control in existing systems is coarse — role labels rarely "
    "express “which faculty, which department, which semester” a "
    "staff member may act on.",
    "Student-facing services (transcripts, certificates, payments) require "
    "in-person visits and manual reconciliation with the treasury.",
    "Any replacement must coexist with the official SIS, which remains the "
    "institutional source of truth.",
], size=15, gap=14)
challenges = [
    ("❖", "Fragmentation", "No single system covers the full lifecycle"),
    ("✖", "Coarse access control", "Roles without structural or temporal scope"),
    ("✎", "Manual workflows", "Paper requests, queues, and hand reconciliation"),
    ("⇄", "Data drift", "Portal and SIS records diverge over time"),
]
for i, (g, t, d) in enumerate(challenges):
    y = Inches(1.95) + i * Inches(1.22)
    c = card(s, Inches(6.7), y, Inches(5.95), Inches(1.05))
    badge(s, Inches(6.92), y + Inches(0.27), Inches(0.5), g, size=14)
    tf = textbox(s, Inches(7.62), y + Inches(0.16), Inches(4.85), Inches(0.85))
    para(tf, t, size=14.5, bold=True, color=NAVY, space_after=1, first=True)
    para(tf, d, size=11.5, color=MUTED, space_after=0)
footer(s, page())

# ======================================================== 5 · OBJECTIVES =====
s = new_slide()
header(s, "Section 2", "Project Objectives")
objs = [
    "Provide a unified web platform covering the academic lifecycle end-to-end "
    "— structure, calendar, courses, registration, grades, fees, and services.",
    "Design a fine-grained authorization model combining roles with structural "
    "and temporal scope (RBAC extended with attribute-based constraints).",
    "Adopt a modular architecture in which domain features are independent "
    "plug-in modules with their own permissions and persistence mapping.",
    "Implement reliable bidirectional synchronization with the university's "
    "existing SIS, with auditing, checkpoints, and retry semantics.",
    "Deliver a bilingual Arabic/English user experience with complete "
    "right-to-left layout support.",
    "Enforce quality through layered automated testing, including tests that "
    "verify the architecture itself.",
]
for i, t in enumerate(objs):
    col, row = divmod(i, 3)
    x = Inches(0.65) + col * Inches(6.3)
    y = Inches(2.05) + row * Inches(1.6)
    badge(s, x, y + Inches(0.06), Inches(0.52), f"{i+1}", size=15)
    tf = textbox(s, x + Inches(0.75), y, Inches(5.35), Inches(1.5))
    para(tf, t, size=13.5, color=INK, space_after=0, first=True)
footer(s, page())

# ====================================================== 6 · RELATED WORK =====
s = new_slide()
header(s, "Section 3", "Related Work & Context")
cols = [
    ("Commercial SIS", "Ellucian Banner, PeopleSoft Campus",
     ["Comprehensive administrative coverage",
      "High licensing and customization cost",
      "Rigid data models; weak Arabic/RTL support"]),
    ("Learning platforms", "Moodle, Canvas",
     ["Strong course-delivery features",
      "Course-centric — not designed for administrative "
      "lifecycle, fees, or structure management",
      "Limited fine-grained administrative authorization"]),
    ("In-house legacy systems", "Typical institutional builds",
     ["Tailored to local regulations and workflows",
      "Often monolithic and tightly coupled",
      "Hard to extend; minimal automated testing"]),
]
for i, (t, sub, pts) in enumerate(cols):
    x = Inches(0.65) + i * Inches(4.18)
    c = card(s, x, Inches(1.95), Inches(3.95), Inches(3.45))
    tf = textbox(s, x + Inches(0.25), Inches(2.2), Inches(3.45), Inches(3.0))
    para(tf, t, size=16, bold=True, color=NAVY, space_after=1, first=True)
    para(tf, sub, size=11, color=MUTED, italic=True, space_after=8)
    bullets(tf, pts, size=12, gap=7, first=False)
c = card(s, Inches(0.65), Inches(5.65), Inches(12.03), Inches(1.15), fill=GOLD_SOFT)
set_shape_text(c, [
    ("Positioning", 13, True, NAVY, BODY_FONT),
    ("This project targets the gap between the three: lifecycle coverage of a "
     "SIS, the extensibility of a modular platform, and scope-aware "
     "authorization — while coexisting with the official SIS rather than "
     "replacing it.", 12.5, False, INK, BODY_FONT)])
footer(s, page())

# ================================================= 7 · SYSTEM ARCHITECTURE ===
s = new_slide()
header(s, "Section 4", "System Architecture")
layers = [
    ("React 19 Single-Page Application", "Administrative portal · Student portal · i18n (AR/EN)"),
    ("REST API — ASP.NET Core (.NET 9)", "28 controllers · JWT auth · scope-resolving middleware"),
    ("Application Core — CQRS via MediatR", "7 plug-in domain modules · permission manifests · validators"),
    ("Infrastructure", "EF Core 9 · Redis cache · outbox processor · QuestPDF"),
    ("SQL Server", "Single relational store · soft-delete global filters"),
]
lx, lw = Inches(0.9), Inches(6.6)
y = Inches(1.95)
for i, (t, d) in enumerate(layers):
    fill = NAVY if i in (0, 4) else ICE
    tcol = WHITE if i in (0, 4) else NAVY
    dcol = ICE_LINE if i in (0, 4) else MUTED
    b = shape(s, MSO_SHAPE.ROUNDED_RECTANGLE, lx, y, lw, Inches(0.78), fill)
    set_shape_text(b, [(t, 14, True, tcol, BODY_FONT),
                       (d, 10.5, False, dcol, BODY_FONT)])
    if i < len(layers) - 1:
        arrow_down(s, lx + lw / 2, y + Inches(0.79), h=Inches(0.18))
    y += Inches(0.99)
# Sync column (right)
sx, sw2 = Inches(8.35), Inches(4.0)
b = shape(s, MSO_SHAPE.ROUNDED_RECTANGLE, sx, Inches(2.35), sw2, Inches(1.5), GOLD)
set_shape_text(b, [("Synchronization Platform", 14.5, True, NAVY_DEEP, BODY_FONT),
                   ("Hangfire jobs · 6 sync modules", 11, False, NAVY_DEEP, BODY_FONT),
                   ("checkpoints · retries · audit trail", 11, False, NAVY_DEEP, BODY_FONT)])
b = shape(s, MSO_SHAPE.ROUNDED_RECTANGLE, sx, Inches(4.75), sw2, Inches(1.1), NAVY)
set_shape_text(b, [("External University SIS", 13.5, True, WHITE, BODY_FONT),
                   ("Institutional source of truth", 10.5, False, ICE_LINE, BODY_FONT)])
shape(s, MSO_SHAPE.UP_DOWN_ARROW, sx + sw2 / 2 - Inches(0.15),
      Inches(3.95), Inches(0.3), Inches(0.7), GOLD)               # sync <-> SIS
arrow_lr(s, Inches(7.62), Inches(3.1), w=Inches(0.62))            # core <-> sync
tf = textbox(s, sx, Inches(6.1), sw2, Inches(0.7))
para(tf, "The sync engine reconciles portal data with the SIS in both "
     "directions on scheduled jobs.", size=11, color=MUTED, italic=True,
     first=True)
footer(s, page())

# ============================================ 8 · BACKEND MODULAR DESIGN =====
s = new_slide()
header(s, "Section 4", "Backend: Modular Monolith Design")
tf = textbox(s, Inches(0.65), Inches(1.95), Inches(6.55), Inches(4.7))
bullets(tf, [
    ("Layered solution.", "API host, Core (Abstractions / Domain / "
     "Application / Infrastructure), SharedKernel, plug-in Modules, and a "
     "parallel Sync platform — 30 projects in total."),
    ("Plug-in registration.", "Each module exposes Add<Module>Module() "
     "extension registering its services, repositories, and permission "
     "manifest into the host's dependency-injection container."),
    ("Persistence injection.", "Modules contribute EF Core entity "
     "configurations through assembly injection into the shared DbContext "
     "model — no core changes required to add a module."),
    ("Eventual consistency.", "Domain events are persisted transactionally "
     "via the outbox pattern and delivered by a background processor."),
    ("Single deployable.", "Modular boundaries give micro-service-style "
     "separation without distributed-system operational cost."),
], size=14, gap=11)
tf = textbox(s, Inches(7.6), Inches(1.95), Inches(5.0), Inches(0.4))
para(tf, "Domain modules", size=15, bold=True, color=NAVY, first=True)
mods = ["Student", "Registration", "Course Offering", "Academic Records",
        "Schedule", "Payments", "Student Services"]
for i, m in enumerate(mods):
    y = Inches(2.45) + i * Inches(0.62)
    c = card(s, Inches(7.6), y, Inches(5.0), Inches(0.52))
    tfm = textbox(s, Inches(7.85), y + Inches(0.1), Inches(4.5), Inches(0.35))
    para(tfm, m + "  ·  module + abstractions pair", size=12.5,
         color=INK, bold=False, first=True)
footer(s, page())

# ============================================ 9 · AUTHORIZATION MODEL ========
s = new_slide()
header(s, "Section 5", "Scope-First Authorization Model")
boxes = [
    ("Requested scope", "structure / year / semester\nfrom request headers", ICE, NAVY),
    ("∩", "", WHITE, GOLD),
    ("Allowed scope", "role grants +\nper-staff overrides", ICE, NAVY),
    ("→", "", WHITE, GOLD),
    ("Effective scope", "data visibility\nboundary", GOLD_SOFT, NAVY),
    ("→", "", WHITE, GOLD),
    ("Permission check", "module.resource.action", NAVY, WHITE),
]
x = Inches(0.65)
for t, d, fill, tcol in boxes:
    if t in ("∩", "→"):
        tf = textbox(s, x, Inches(2.32), Inches(0.42), Inches(0.6))
        para(tf, t, size=26, bold=True, color=GOLD, align=PP_ALIGN.CENTER, first=True)
        x += Inches(0.47)
        continue
    b = shape(s, MSO_SHAPE.ROUNDED_RECTANGLE, x, Inches(2.05), Inches(2.62), Inches(1.15), fill)
    lines = [(t, 14, True, tcol, BODY_FONT)]
    for ln in d.split("\n"):
        lines.append((ln, 10.5, False, (ICE_LINE if fill == NAVY else MUTED), BODY_FONT))
    set_shape_text(b, lines)
    x += Inches(2.72)
tf = textbox(s, Inches(0.65), Inches(3.7), Inches(12.0), Inches(3.1))
bullets(tf, [
    ("Two scope axes.", "Structural (University → Faculty → Department "
     "→ Program → Level) and temporal (Academic Year → Semester); "
     "every request is evaluated inside their intersection."),
    ("Canonical permissions.", "Names follow module.resource.action (e.g. "
     "courses.courses.View); each module declares its catalog in a code-defined "
     "permission manifest synchronized to the database at startup."),
    ("Implication graph.", "Action hierarchies (Edit implies View) are expanded "
     "at write time, making runtime checks a single O(1) lookup."),
    ("Caching.", "Permission sets are cached in Redis and invalidated when "
     "roles or overrides change."),
    ("Enforced by tests.", "A contract test verifies every [HasPermission] "
     "attribute references a declared constant — no orphan permissions."),
], size=13.5, gap=8)
footer(s, page())

# ==================================================== 10 · DATA MODEL ========
s = new_slide()
header(s, "Section 5", "Data Model: Recursive Academic Hierarchy")
levels = ["University", "Faculty", "Department", "Program", "Level"]
y = Inches(1.95)
for i, lv in enumerate(levels):
    x = Inches(0.9) + i * Inches(0.42)
    b = shape(s, MSO_SHAPE.ROUNDED_RECTANGLE, x, y, Inches(2.9), Inches(0.62),
              NAVY if i == 0 else ICE)
    set_shape_text(b, [(lv, 13.5, True, WHITE if i == 0 else NAVY, BODY_FONT)])
    if i < len(levels) - 1:
        arrow_down(s, x + Inches(0.42) + Inches(1.45), y + Inches(0.64),
                   h=Inches(0.16), w=Inches(0.26))
    y += Inches(0.84)
tf = textbox(s, Inches(0.9), y + Inches(0.1), Inches(4.6), Inches(0.6))
para(tf, "One recursive entity — StructureNode — models the entire "
     "tree.", size=12, color=MUTED, italic=True, first=True)
tf = textbox(s, Inches(6.3), Inches(2.0), Inches(6.35), Inches(4.8))
bullets(tf, [
    ("Single-table hierarchy.", "All organizational levels are rows of one "
     "self-referencing StructureNode entity rather than separate "
     "Faculty/Department/Program tables."),
    ("Materialized path + depth.", "Each node stores its full ancestor path, "
     "so subtree, ancestor, and descendant queries resolve with a prefix "
     "match — no recursive SQL at read time."),
    ("Typed children.", "Structure rules constrain which node types may nest "
     "under which (e.g. a Program cannot contain a Faculty)."),
    ("Soft delete.", "Deletion marks the subtree by path prefix; EF Core "
     "global query filters hide deleted and inactive rows everywhere."),
    ("Safe restructuring.", "Moving or reordering a node rewrites paths and "
     "depths for its whole subtree atomically."),
], size=14, gap=11)
footer(s, page())

# ==================================================== 11 · SYNC PLATFORM =====
s = new_slide()
header(s, "Section 6", "Synchronization Platform")
b = shape(s, MSO_SHAPE.ROUNDED_RECTANGLE, Inches(0.75), Inches(2.3), Inches(3.0), Inches(1.7), NAVY)
set_shape_text(b, [("External SIS", 15, True, WHITE, BODY_FONT),
                   ("students · staff · courses", 11, False, ICE_LINE, BODY_FONT),
                   ("schedules · finance", 11, False, ICE_LINE, BODY_FONT)])
arrow_lr(s, Inches(3.9), Inches(3.15), w=Inches(0.7))
b = shape(s, MSO_SHAPE.ROUNDED_RECTANGLE, Inches(4.75), Inches(2.05), Inches(3.85), Inches(2.2), GOLD)
set_shape_text(b, [("Sync Engine (Hangfire)", 15, True, NAVY_DEEP, BODY_FONT),
                   ("Students · Staff · Courses", 11.5, False, NAVY_DEEP, BODY_FONT),
                   ("Schedules · Finance · Registration", 11.5, False, NAVY_DEEP, BODY_FONT),
                   ("one ISyncModule per domain", 10.5, False, NAVY, BODY_FONT)])
arrow_lr(s, Inches(8.75), Inches(3.15), w=Inches(0.7))
b = shape(s, MSO_SHAPE.ROUNDED_RECTANGLE, Inches(9.6), Inches(2.3), Inches(3.0), Inches(1.7), NAVY)
set_shape_text(b, [("Portal Database", 15, True, WHITE, BODY_FONT),
                   ("SQL Server", 11, False, ICE_LINE, BODY_FONT),
                   ("portal-owned + SIS-owned data", 10.5, False, ICE_LINE, BODY_FONT)])
tf = textbox(s, Inches(0.75), Inches(4.75), Inches(12.0), Inches(2.2))
bullets(tf, [
    ("Ownership map.", "Every synchronized record carries an external "
     "identifier; each entity type has a declared owner, so pulls update "
     "without overwriting portal-owned fields."),
    ("Resilience.", "Checkpoint store prevents duplicate processing; failures "
     "retry with exponential backoff and surface in a sync audit log."),
    ("Scheduling.", "Hangfire runs pull and push jobs per module on "
     "configurable schedules, with run history and failure notifications."),
], size=13.5, gap=8)
footer(s, page())

# ============================================ 12 · FRONTEND ARCHITECTURE =====
s = new_slide()
header(s, "Section 6", "Frontend Architecture")
tf = textbox(s, Inches(0.65), Inches(1.95), Inches(6.9), Inches(4.8))
bullets(tf, [
    ("Modular SPA.", "React 19 + Vite; each feature is a self-contained "
     "module exporting pages, routes, navigation entries, and locale files."),
    ("Manifest-driven routing.", "A route registry aggregates module "
     "manifests — routes, breadcrumbs, lazy loading, and menus are "
     "derived from metadata, mirroring the backend's plug-in design."),
    ("Server state.", "TanStack Query caches API data with scope-aware query "
     "keys; mutations invalidate precisely."),
    ("Scope propagation.", "An Axios interceptor attaches the JWT and the "
     "selected structure/year/semester headers to every request, matching "
     "the backend's effective-scope resolution."),
    ("Permission gating.", "A PermissionContext drives route guards, menu "
     "filtering, and per-action UI gates from the same permission names "
     "used server-side."),
    ("Internationalization.", "i18next with Arabic and English bundles; "
     "RTL handled via CSS logical properties and design tokens."),
], size=13.5, gap=9)
fstats = [("17", "feature modules"), ("27", "routed pages"),
          ("21", "API service clients"), ("24", "locale bundles")]
for i, (n, t) in enumerate(fstats):
    y = Inches(2.0) + i * Inches(1.17)
    c = card(s, Inches(8.1), y, Inches(4.5), Inches(1.0))
    set_shape_text(c, [(n + "  ", 26, True, NAVY, HEAD_FONT),
                       (t, 12.5, False, MUTED, BODY_FONT)])
footer(s, page())

# ========================================= 13 · IMPLEMENTATION METHOD ========
s = new_slide()
header(s, "Section 6", "Implementation Methodology")
c = card(s, Inches(0.65), Inches(1.95), Inches(6.0), Inches(4.75))
tf = textbox(s, Inches(0.95), Inches(2.2), Inches(5.4), Inches(4.3))
para(tf, "Engineering practices", size=16, bold=True, color=NAVY,
     space_after=8, first=True)
bullets(tf, [
    "CQRS with MediatR — commands and queries as discrete handlers "
    "behind validation and authorization behaviors.",
    "FluentValidation on every command; zod schemas mirror validation "
    "client-side.",
    "Redis-backed caching for permissions and structure reads, with "
    "explicit invalidation triggers.",
    "QuestPDF renders bilingual transcripts and receipts server-side.",
    "Prerequisite graphs validated as DAGs with cycle detection on both "
    "client and server.",
], size=13, gap=8, first=False)
c = card(s, Inches(7.0), Inches(1.95), Inches(5.65), Inches(4.75))
tf = textbox(s, Inches(7.3), Inches(2.2), Inches(5.05), Inches(4.3))
para(tf, "Development process", size=16, bold=True, color=NAVY,
     space_after=8, first=True)
bullets(tf, [
    "Specification-first: a master specification and per-domain documents "
    "preceded implementation.",
    "Iterative sprints per portal area, each ending with an audit-and-fix "
    "pass against the specification.",
    "Layered data seeding — identity, structure, calendar, and a "
    "large-volume seeder that populates both portals for realistic "
    "evaluation.",
    "Production-readiness audit covering security hardening, password "
    "policy, and admin provisioning.",
], size=13, gap=8, first=False)
footer(s, page())

# ============================================ 14 · STAFF PORTAL FEATURES =====
s = new_slide()
header(s, "Section 6", "Feature Overview — Administrative Portal")
feats = [
    ("⌂", "University structure", "Tree editor for faculties, departments, programs, and levels with drag re-ordering"),
    ("◷", "Academic calendar", "Year and semester lifecycle: create, open, close, archive — permission-gated"),
    ("✎", "Course catalog & plans", "Courses with credit hours and prerequisite chains; per-program academic plans"),
    ("▦", "Offerings & scheduling", "Section capacity, instructor assignment, and a scheduling matrix with conflict detection"),
    ("✓", "Permissions & roles", "Permission-matrix editor with scoped grants and per-staff overrides"),
    ("$", "Treasury & orders", "Fee orders, payment tracking, and receipt reconciliation against the treasury system"),
]
for i, (g, t, d) in enumerate(feats):
    col, row = divmod(i, 3)
    x = Inches(0.65) + (i % 3) * Inches(4.18)
    yy = Inches(2.0) + (i // 3) * Inches(2.45)
    c = card(s, x, yy, Inches(3.95), Inches(2.2))
    badge(s, x + Inches(0.25), yy + Inches(0.25), Inches(0.55), g, size=19)
    tf = textbox(s, x + Inches(0.25), yy + Inches(0.95), Inches(3.45), Inches(1.15))
    para(tf, t, size=14.5, bold=True, color=NAVY, space_after=3, first=True)
    para(tf, d, size=11, color=MUTED, space_after=0)
footer(s, page())

# ========================================== 15 · STUDENT PORTAL FEATURES =====
s = new_slide()
header(s, "Section 6", "Feature Overview — Student Portal")
feats = [
    ("☖", "Dashboard", "Academic snapshot: GPA, credit progress, current registrations, and announcements"),
    ("✎", "Course registration", "Self-service enrollment with registration windows, capacity, and prerequisite checks"),
    ("▦", "Weekly schedule", "Personal timetable grid generated from enrolled course offerings"),
    ("★", "Grades & transcript", "Term-by-term grades, GPA/CGPA, and downloadable bilingual PDF transcript"),
    ("✉", "Service requests", "Catalog of services with dynamic forms and staged approval workflows"),
    ("$", "Fees & payments", "Outstanding fees grouped into orders, paid online via gateway with webhook confirmation"),
]
for i, (g, t, d) in enumerate(feats):
    x = Inches(0.65) + (i % 3) * Inches(4.18)
    yy = Inches(2.0) + (i // 3) * Inches(2.45)
    c = card(s, x, yy, Inches(3.95), Inches(2.2))
    badge(s, x + Inches(0.25), yy + Inches(0.25), Inches(0.55), g, size=19)
    tf = textbox(s, x + Inches(0.25), yy + Inches(0.95), Inches(3.45), Inches(1.15))
    para(tf, t, size=14.5, bold=True, color=NAVY, space_after=3, first=True)
    para(tf, d, size=11, color=MUTED, space_after=0)
footer(s, page())

# ===================================================== 16-19 · DEMO ==========
def demo_slide(n_label, title, points, outcome):
    s = new_slide()
    header(s, f"Section 7 · Demonstration {n_label}", title)
    ph = shape(s, MSO_SHAPE.ROUNDED_RECTANGLE, Inches(0.65), Inches(2.0),
               Inches(6.6), Inches(4.6), ICE, line=ICE_LINE, line_w=Pt(1.25))
    set_shape_text(ph, [
        ("▶", 40, True, GOLD, BODY_FONT),
        ("Live demonstration", 16, True, NAVY, BODY_FONT),
        ("[ screen recording / live walkthrough placeholder ]", 12, False,
         MUTED, BODY_FONT)])
    tf = textbox(s, Inches(7.6), Inches(2.0), Inches(5.1), Inches(0.4))
    para(tf, "Talking points", size=15, bold=True, color=NAVY, first=True)
    tf = textbox(s, Inches(7.6), Inches(2.5), Inches(5.1), Inches(3.3))
    for i, p in enumerate(points):
        pr = para(tf, f"{i+1}.  {p}", size=12.5, color=INK, space_after=8,
                  first=(i == 0))
    c = card(s, Inches(7.6), Inches(5.85), Inches(5.1), Inches(0.95), fill=GOLD_SOFT)
    set_shape_text(c, [("What this shows", 11, True, NAVY, BODY_FONT),
                       (outcome, 11, False, INK, BODY_FONT)])
    footer(s, page())

demo_slide("1", "Administration & Security", [
    "Sign in as administrator on the staff portal.",
    "Open the university structure tree; add a department under a faculty.",
    "Open the permission matrix; grant a staff member a scoped permission "
    "limited to that faculty and the current semester.",
    "Sign in as that staff member — data outside the granted scope is "
    "invisible, and unauthorized actions are absent from the UI.",
], "Scope-first authorization working end-to-end: grants made in the matrix "
   "immediately reshape both API responses and the interface.")

demo_slide("2", "Academic Operations", [
    "Create a semester under the current academic year and open it.",
    "Add a course with a prerequisite; attempt a circular prerequisite and "
    "show the cycle being rejected.",
    "Create a course offering, assign an instructor and capacity.",
    "Use the scheduling matrix to place meeting slots; trigger and resolve "
    "an instructor double-booking warning.",
], "Domain rules — calendar lifecycle, prerequisite DAG validation, and "
   "schedule conflict detection — enforced at the moment of entry.")

demo_slide("3", "Student Journey", [
    "Sign in as a student; review the dashboard's GPA and credit summary.",
    "Open course registration; register for an offering and watch remaining "
    "capacity update.",
    "View the generated weekly schedule grid.",
    "Open grades; switch the interface to Arabic to show the RTL layout; "
    "download the bilingual PDF transcript.",
], "The complete self-service student loop — registration through "
   "transcript — in both languages from one data model.")

demo_slide("4", "Services, Fees & Payments", [
    "As a student, submit a service request (e.g. official transcript) "
    "through its dynamically defined form.",
    "As staff, review and approve the request in its workflow stage.",
    "Show the fee generated by approval, grouped into a payment order.",
    "Complete payment via the gateway sandbox; the webhook confirms the "
    "order and a receipt and notification appear.",
], "Administrative workflow, financial integration, and asynchronous webhook "
   "handling cooperating across both portals.")

# ==================================================== 20 · TESTING ===========
s = new_slide()
header(s, "Section 8", "Testing & Quality Assurance")
tests = [
    ("Unit tests", "Domain logic and application handlers: authentication, "
     "authorization, courses, schedules, payments, localization"),
    ("Integration tests", "Database behavior through EF Core and external "
     "service interaction against stubs"),
    ("Architecture tests", "Automated dependency rules — layer "
     "boundaries of the modular monolith are asserted in CI, not by "
     "convention"),
    ("Contract tests", "Every [HasPermission] attribute must reference a "
     "declared permission constant; manifests stay complete by construction"),
    ("Sync platform tests", "Checkpoint handling, retry behavior, and "
     "module push/pull logic of the synchronization engine"),
    ("Frontend harness", "Vitest + Testing Library setup for component "
     "tests; coverage growing alongside features"),
]
for i, (t, d) in enumerate(tests):
    x = Inches(0.65) + (i % 3) * Inches(4.18)
    yy = Inches(2.0) + (i // 3) * Inches(2.4)
    c = card(s, x, yy, Inches(3.95), Inches(2.15))
    tf = textbox(s, x + Inches(0.25), yy + Inches(0.22), Inches(3.45), Inches(1.75))
    para(tf, t, size=14.5, bold=True, color=NAVY, space_after=4, first=True)
    para(tf, d, size=11.5, color=MUTED, space_after=0)
footer(s, page())

# ==================================================== 21 · RESULTS ===========
s = new_slide()
header(s, "Section 8", "Results & System Scale")
stats = [("30", "backend projects", "across API, core, modules, sync"),
         ("28", "REST controllers", "covering every domain area"),
         ("7", "plug-in modules", "with isolated permissions & persistence"),
         ("17", "frontend modules", "aggregated by route manifests"),
         ("27", "routed pages", "across both portals"),
         ("6", "test suites", "incl. architecture & contract tests")]
for i, (n, t, d) in enumerate(stats):
    x = Inches(0.65) + (i % 3) * Inches(4.18)
    yy = Inches(2.0) + (i // 3) * Inches(2.15)
    c = card(s, x, yy, Inches(3.95), Inches(1.9))
    set_shape_text(c, [(n, 48, True, NAVY, HEAD_FONT),
                       (t, 13.5, True, INK, BODY_FONT),
                       (d, 11, False, MUTED, BODY_FONT)])
tf = textbox(s, Inches(0.65), Inches(6.35), Inches(12.0), Inches(0.6))
para(tf, "Both portals run against a large seeded dataset — students, "
     "registrations, grades, fees, and requests — demonstrating realistic "
     "end-to-end operation.", size=12.5, color=MUTED, italic=True,
     align=PP_ALIGN.CENTER, first=True)
footer(s, page())

# ====================================== 22 · LIMITATIONS & FUTURE WORK =======
s = new_slide()
header(s, "Section 8", "Limitations & Future Work")
c = card(s, Inches(0.65), Inches(1.95), Inches(6.0), Inches(4.75))
tf = textbox(s, Inches(0.95), Inches(2.2), Inches(5.4), Inches(4.3))
para(tf, "Current limitations", size=16, bold=True, color=NAVY,
     space_after=8, first=True)
bullets(tf, [
    "Single deployable: modules are isolated logically but share one "
    "process and database.",
    "Academic-records synchronization from the SIS is partially "
    "implemented relative to other sync modules.",
    "Frontend automated test coverage is thin compared with the backend.",
    "Notification delivery is in-app only; e-mail/SMS channels are "
    "designed but not wired to providers.",
], size=13.5, gap=10, first=False)
c = card(s, Inches(7.0), Inches(1.95), Inches(5.65), Inches(4.75), fill=GOLD_SOFT)
tf = textbox(s, Inches(7.3), Inches(2.2), Inches(5.05), Inches(4.3))
para(tf, "Future work", size=16, bold=True, color=NAVY, space_after=8,
     first=True)
bullets(tf, [
    "Extract high-load modules (registration, payments) into services "
    "when scale demands — the module boundaries already exist.",
    "Mobile application reusing the REST API and permission model.",
    "Analytics dashboards over the accumulated academic data.",
    "Single sign-on integration with institutional identity providers.",
    "Container orchestration and CI/CD pipeline for automated delivery.",
], size=13.5, gap=10, first=False)
footer(s, page())

# ==================================================== 23 · CONCLUSION ========
s = new_slide(NAVY_DEEP)
header(s, "Section 8", "Conclusion", dark=True)
tf = textbox(s, Inches(0.95), Inches(2.1), Inches(11.4), Inches(4.4))
bullets(tf, [
    ("A working platform.", "The project delivers a bilingual, dual-portal "
     "university system covering structure, calendar, courses, registration, "
     "grades, services, and payments end-to-end."),
    ("An architectural argument.", "A modular monolith with plug-in modules, "
     "manifest-declared permissions, and architecture tests achieves "
     "service-style boundaries at monolith operational cost."),
    ("A security contribution.", "Scope-first authorization — intersecting "
     "structural and temporal scope before permission evaluation — "
     "expresses real university delegation patterns that plain RBAC cannot."),
    ("A pragmatic integration.", "Checkpointed, audited bidirectional "
     "synchronization lets the portal coexist with the institutional SIS "
     "instead of demanding its replacement."),
], size=16, color=WHITE, gap=16)
footer(s, page(), dark=True)

# ==================================================== 24 · THANK YOU =========
s = new_slide(NAVY_DEEP)
badge(s, Inches(6.067), Inches(1.7), Inches(1.2), "CU", size=30)
tf = textbox(s, Inches(1.2), Inches(3.3), Inches(10.93), Inches(1.6))
para(tf, "Thank You", size=48, bold=True, color=WHITE, font=HEAD_FONT,
     align=PP_ALIGN.CENTER, space_after=10, first=True)
para(tf, "Questions & Discussion", size=20, color=GOLD,
     align=PP_ALIGN.CENTER, italic=True)
tf = textbox(s, Inches(1.2), Inches(5.3), Inches(10.93), Inches(0.5))
para(tf, "Capital University Student Portal · Graduation Project · 2026",
     size=13, color=ICE_LINE, align=PP_ALIGN.CENTER, first=True)
page()

prs.save("Graduation_Project_Presentation.pptx")
print(f"Saved Graduation_Project_Presentation.pptx with {PAGE[0]} slides.")
