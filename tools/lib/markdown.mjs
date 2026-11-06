// Markdown 输出：表格单元格里的竖线、换行都要转义，否则表格会被切开
const cell = (value) => String(value ?? '').replace(/\|/g, '\\|').replace(/\r?\n/g, '<br>');

/** 一张表：表头 + 行（每行一个数组） */
export function table(headers, rows) {
  if (!rows.length) return '（无）\n';
  return [
    `| ${headers.map(cell).join(' | ')} |`,
    `|${headers.map(() => '---').join('|')}|`,
    ...rows.map((row) => `| ${row.map(cell).join(' | ')} |`),
  ].join('\n') + '\n';
}

/** 行内代码；空值写成破折号 */
export const code = (value) => (value ? `\`${value}\`` : '—');
