// Shared helpers for generating the Supplier Portal Word documents (docx-js).
const {
  Document, Packer, Paragraph, TextRun, HeadingLevel, AlignmentType, Table, TableRow, TableCell,
  WidthType, BorderStyle, ShadingType, PageBreak, Header, Footer, PageNumber, LevelFormat,
  TableOfContents, ExternalHyperlink,
} = require("docx");
const fs = require("fs");

const NAVY = "0B2545";
const NAVY2 = "274A7D";
const GOLD = "CAA04D";
const GREY = "6B7280";
const LIGHT = "F1F4F9";
const HEADROW = "0B2545";
const BORDER = "D3DAE6";
const GREEN = "1B7F4B";
const AMBER = "B7791F";
const RED = "B42318";

const font = "Calibri";

function t(text, opts = {}) { return new TextRun({ text, font, ...opts }); }

function para(text, opts = {}) {
  const runs = Array.isArray(text) ? text : [new TextRun({ text, font, size: opts.size || 21, color: opts.color, bold: opts.bold, italics: opts.italics })];
  return new Paragraph({ children: runs, spacing: { after: opts.after ?? 120, line: 276 }, alignment: opts.align });
}

function h1(text) { return new Paragraph({ heading: HeadingLevel.HEADING_1, children: [new TextRun({ text, font, bold: true, color: NAVY, size: 30 })], spacing: { before: 260, after: 120 } }); }
function h2(text) { return new Paragraph({ heading: HeadingLevel.HEADING_2, children: [new TextRun({ text, font, bold: true, color: NAVY2, size: 25 })], spacing: { before: 200, after: 90 } }); }
function h3(text) { return new Paragraph({ heading: HeadingLevel.HEADING_3, children: [new TextRun({ text, font, bold: true, color: NAVY2, size: 22 })], spacing: { before: 150, after: 70 } }); }

function bullet(text, level = 0) {
  const runs = Array.isArray(text) ? text : [new TextRun({ text, font, size: 21 })];
  return new Paragraph({ children: runs, numbering: { reference: "bullets", level }, spacing: { after: 60, line: 264 } });
}
function num(text, ref = "nums") {
  const runs = Array.isArray(text) ? text : [new TextRun({ text, font, size: 21 })];
  return new Paragraph({ children: runs, numbering: { reference: ref, level: 0 }, spacing: { after: 60 } });
}

function cell(text, { header = false, bg, width, bold = false, align } = {}) {
  const runs = Array.isArray(text)
    ? text
    : [new TextRun({ text: String(text), font, size: header ? 18 : 18, bold: header || bold, color: header ? "FFFFFF" : "22303F" })];
  return new TableCell({
    width: { size: width, type: WidthType.DXA },
    shading: header ? { type: ShadingType.CLEAR, fill: HEADROW, color: "auto" } : (bg ? { type: ShadingType.CLEAR, fill: bg, color: "auto" } : undefined),
    margins: { top: 40, bottom: 40, left: 70, right: 70 },
    children: [new Paragraph({ children: runs, alignment: align, spacing: { after: 0, line: 240 } })],
  });
}

// rows: array of arrays (first row = header). widths: array of DXA summing to total.
function table(header, rows, widths, opts = {}) {
  const total = widths.reduce((a, b) => a + b, 0);
  const headerRow = new TableRow({
    tableHeader: true,
    children: header.map((hd, i) => cell(hd, { header: true, width: widths[i] })),
  });
  const bodyRows = rows.map((r, ri) => new TableRow({
    children: r.map((c, i) => {
      if (c && typeof c === "object" && c.__cell) return cell(c.text, { width: widths[i], bg: ri % 2 ? undefined : LIGHT, bold: c.bold, align: c.align });
      return cell(c, { width: widths[i], bg: ri % 2 ? undefined : LIGHT });
    }),
  }));
  return new Table({
    columnWidths: widths,
    width: { size: total, type: WidthType.DXA },
    borders: {
      top: { style: BorderStyle.SINGLE, size: 4, color: BORDER },
      bottom: { style: BorderStyle.SINGLE, size: 4, color: BORDER },
      left: { style: BorderStyle.SINGLE, size: 4, color: BORDER },
      right: { style: BorderStyle.SINGLE, size: 4, color: BORDER },
      insideHorizontal: { style: BorderStyle.SINGLE, size: 4, color: BORDER },
      insideVertical: { style: BorderStyle.SINGLE, size: 4, color: BORDER },
    },
    rows: [headerRow, ...bodyRows],
  });
}

function hr() {
  return new Paragraph({ border: { bottom: { style: BorderStyle.SINGLE, size: 12, color: GOLD } }, spacing: { after: 120 } });
}

function titlePage(title, subtitle, metaRows) {
  const kids = [
    new Paragraph({ children: [new TextRun({ text: "Dubai Investments", font, bold: true, color: NAVY, size: 30 })], spacing: { before: 800, after: 60 } }),
    hr(),
    new Paragraph({ children: [new TextRun({ text: title, font, bold: true, color: NAVY, size: 52 })], spacing: { before: 200, after: 60 } }),
    new Paragraph({ children: [new TextRun({ text: subtitle, font, color: GREY, size: 28 })], spacing: { after: 400 } }),
  ];
  const widths = [3200, 6600];
  kids.push(table(["Field", "Detail"], metaRows.map(([k, v]) => [{ __cell: true, text: k, bold: true }, v]), widths));
  kids.push(new Paragraph({ children: [new PageBreak()] }));
  return kids;
}

function tocPage() {
  return [
    new Paragraph({ children: [new TextRun({ text: "Table of Contents", font, bold: true, color: NAVY, size: 30 })], spacing: { after: 120 } }),
    new TableOfContents("Contents", { hyperlink: true, headingStyleRange: "1-3" }),
    new Paragraph({ children: [new PageBreak()] }),
  ];
}

function buildDoc({ title, children }) {
  return new Document({
    creator: "Supplier Portal QA",
    title,
    features: { updateFields: true },
    numbering: {
      config: [
        { reference: "bullets", levels: [
          { level: 0, format: LevelFormat.BULLET, text: "•", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 360, hanging: 260 } } } },
          { level: 1, format: LevelFormat.BULLET, text: "◦", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 720, hanging: 260 } } } },
        ]},
        { reference: "nums", levels: [
          { level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 360, hanging: 260 } } } },
        ]},
        { reference: "steps", levels: [
          { level: 0, format: LevelFormat.DECIMAL, text: "Step %1.", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 520, hanging: 420 } } } },
        ]},
      ],
    },
    styles: { default: { document: { run: { font, size: 21 } } } },
    sections: [{
      properties: { page: { margin: { top: 1000, bottom: 1000, left: 1100, right: 1100 } } },
      headers: { default: new Header({ children: [new Paragraph({ alignment: AlignmentType.RIGHT, children: [new TextRun({ text: title, font, size: 15, color: GREY })], border: { bottom: { style: BorderStyle.SINGLE, size: 6, color: GOLD } } })] }) },
      footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ text: "Supplier Portal — Confidential   |   Page ", font, size: 15, color: GREY }), new TextRun({ children: [PageNumber.CURRENT], font, size: 15, color: GREY }), new TextRun({ text: " of ", font, size: 15, color: GREY }), new TextRun({ children: [PageNumber.TOTAL_PAGES], font, size: 15, color: GREY })] })] }) },
      children,
    }],
  });
}

async function write(doc, path) {
  const buf = await Packer.toBuffer(doc);
  fs.writeFileSync(path, buf);
  console.log("WROTE", path, buf.length, "bytes");
}

module.exports = { para, h1, h2, h3, bullet, num, table, hr, titlePage, tocPage, buildDoc, write, t, TextRun, PageBreak, colors: { NAVY, NAVY2, GOLD, GREY, LIGHT, GREEN, AMBER, RED }, font };
