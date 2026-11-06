// Lucide 的 SVG 元素归一成一条路径：只含绝对坐标的 M、L、C、Z。
// 圆弧、二次曲线、H/V/S/T 都换算掉：Avalonia、WPF 直接解析成 Geometry，WinForms 按命令重放到 GraphicsPath，不必各自实现圆弧

/** 数字：最多 3 位小数，去掉多余的 0 与 -0 */
const num = (n) => {
  const rounded = Math.round(n * 1000) / 1000;
  return Object.is(rounded, -0) ? '0' : String(rounded);
};

/** 逐字符读路径数据：数字可以紧挨着（`1.5.5`、`-2-3`），圆弧的标志位可以连写（`0 01`） */
class Reader {
  constructor(text) {
    this.text = text;
    this.i = 0;
  }

  skip() {
    while (this.i < this.text.length && /[\s,]/.test(this.text[this.i])) this.i++;
  }

  atEnd() {
    this.skip();
    return this.i >= this.text.length;
  }

  command() {
    this.skip();
    const c = this.text[this.i];
    if (c && /[a-zA-Z]/.test(c)) {
      this.i++;
      return c;
    }
    return undefined;
  }

  number() {
    this.skip();
    const match = this.text.slice(this.i).match(/^[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?/);
    if (!match) throw new Error(`路径里第 ${this.i} 个字符处应该是数字：${this.text}`);
    this.i += match[0].length;
    return Number(match[0]);
  }

  flag() {
    this.skip();
    const c = this.text[this.i++];
    if (c !== '0' && c !== '1') throw new Error(`圆弧的标志位应该是 0 或 1：${this.text}`);
    return c === '1';
  }
}

/** 路径的累积器：记着当前点、子路径起点、上一段的控制点（S、T 要用） */
class Builder {
  segments = [];
  x = 0;
  y = 0;
  startX = 0;
  startY = 0;
  lastCubic = undefined;
  lastQuad = undefined;
  closed = false;

  move(x, y) {
    this.segments.push(['M', x, y]);
    [this.x, this.y, this.startX, this.startY] = [x, y, x, y];
    this.lastCubic = this.lastQuad = undefined;
    this.closed = false;
  }

  /** Z 之后直接接着画的（SVG 允许，从子路径起点开始）：补一个显式的 M，GraphicsPath 要求每个子路径有起点 */
  reopen() {
    if (this.closed) this.move(this.startX, this.startY);
  }

  line(x, y) {
    this.reopen();
    this.segments.push(['L', x, y]);
    [this.x, this.y] = [x, y];
    this.lastCubic = this.lastQuad = undefined;
  }

  cubic(x1, y1, x2, y2, x, y) {
    this.reopen();
    this.segments.push(['C', x1, y1, x2, y2, x, y]);
    [this.x, this.y] = [x, y];
    this.lastCubic = [x2, y2];
    this.lastQuad = undefined;
  }

  /** 二次曲线升阶成三次 */
  quad(qx, qy, x, y) {
    const [x0, y0] = [this.x, this.y];
    this.cubic(x0 + (2 / 3) * (qx - x0), y0 + (2 / 3) * (qy - y0), x + (2 / 3) * (qx - x), y + (2 / 3) * (qy - y), x, y);
    this.lastQuad = [qx, qy];
    this.lastCubic = undefined;
  }

  close() {
    this.segments.push(['Z']);
    [this.x, this.y] = [this.startX, this.startY];
    this.lastCubic = this.lastQuad = undefined;
    this.closed = true;
  }

  /** SVG 圆弧（端点参数）→ 不超过 90° 的几段三次贝塞尔（SVG 规范附录 F.6.5 的换算） */
  arc(rx, ry, rotation, largeArc, sweep, x, y) {
    const [x0, y0] = [this.x, this.y];
    if (x0 === x && y0 === y) return;
    rx = Math.abs(rx);
    ry = Math.abs(ry);
    if (!rx || !ry) return this.line(x, y);
    const phi = (rotation * Math.PI) / 180;
    const [cos, sin] = [Math.cos(phi), Math.sin(phi)];
    const dx = (x0 - x) / 2;
    const dy = (y0 - y) / 2;
    const x1p = cos * dx + sin * dy;
    const y1p = -sin * dx + cos * dy;
    const lambda = (x1p * x1p) / (rx * rx) + (y1p * y1p) / (ry * ry);
    if (lambda > 1) {
      rx *= Math.sqrt(lambda);
      ry *= Math.sqrt(lambda);
    }
    const numerator = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p;
    const denominator = rx * rx * y1p * y1p + ry * ry * x1p * x1p;
    const factor = (largeArc === sweep ? -1 : 1) * Math.sqrt(Math.max(0, numerator / denominator));
    const cxp = (factor * rx * y1p) / ry;
    const cyp = (-factor * ry * x1p) / rx;
    const cx = cos * cxp - sin * cyp + (x0 + x) / 2;
    const cy = sin * cxp + cos * cyp + (y0 + y) / 2;
    const angle = (ux, uy, vx, vy) => Math.atan2(ux * vy - uy * vx, ux * vx + uy * vy);
    const theta1 = angle(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry);
    let delta = angle((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry);
    if (!sweep && delta > 0) delta -= 2 * Math.PI;
    if (sweep && delta < 0) delta += 2 * Math.PI;
    this.ellipseArc(cx, cy, rx, ry, phi, theta1, delta);
  }

  /** 中心参数的椭圆弧 → 几段三次贝塞尔，从当前点画到弧的终点 */
  ellipseArc(cx, cy, rx, ry, phi, theta1, delta) {
    const [cos, sin] = [Math.cos(phi), Math.sin(phi)];
    const point = (t) => [cx + rx * Math.cos(t) * cos - ry * Math.sin(t) * sin, cy + rx * Math.cos(t) * sin + ry * Math.sin(t) * cos];
    const derivative = (t) => [-rx * Math.sin(t) * cos - ry * Math.cos(t) * sin, -rx * Math.sin(t) * sin + ry * Math.cos(t) * cos];
    const count = Math.max(1, Math.ceil(Math.abs(delta) / (Math.PI / 2) - 1e-9));
    const step = delta / count;
    const k = (4 / 3) * Math.tan(step / 4);
    for (let i = 0; i < count; i++) {
      const a = theta1 + i * step;
      const b = a + step;
      const [ax, ay] = point(a);
      const [bx, by] = point(b);
      const [adx, ady] = derivative(a);
      const [bdx, bdy] = derivative(b);
      this.cubic(ax + k * adx, ay + k * ady, bx - k * bdx, by - k * bdy, bx, by);
    }
  }

  /** 整个椭圆：从最右边的点起画一圈 */
  ellipse(cx, cy, rx, ry) {
    this.move(cx + rx, cy);
    this.ellipseArc(cx, cy, rx, ry, 0, 0, 2 * Math.PI);
    this.close();
  }

  /** SVG 的 d 属性。每个 path 元素从原点起算：开头的相对 m 按绝对坐标处理（SVG 规范），不接着前一个元素的当前点 */
  path(d) {
    const reader = new Reader(d);
    [this.x, this.y] = [0, 0];
    this.lastCubic = this.lastQuad = undefined;
    let command;
    while (!reader.atEnd()) {
      // 命令可以省略重复：后面直接跟数字时沿用上一个命令（M 之后算 L）
      command = reader.command() ?? (command === 'M' ? 'L' : command === 'm' ? 'l' : command);
      if (!command) throw new Error(`路径要以命令开头：${d}`);
      const relative = command === command.toLowerCase();
      const [ox, oy] = relative ? [this.x, this.y] : [0, 0];
      switch (command.toUpperCase()) {
        case 'M':
          this.move(ox + reader.number(), oy + reader.number());
          break;
        case 'L':
          this.line(ox + reader.number(), oy + reader.number());
          break;
        case 'H':
          this.line((relative ? this.x : 0) + reader.number(), this.y);
          break;
        case 'V':
          this.line(this.x, (relative ? this.y : 0) + reader.number());
          break;
        case 'C':
          this.cubic(ox + reader.number(), oy + reader.number(), ox + reader.number(), oy + reader.number(), ox + reader.number(), oy + reader.number());
          break;
        case 'S': {
          const [rx, ry] = this.lastCubic ? [2 * this.x - this.lastCubic[0], 2 * this.y - this.lastCubic[1]] : [this.x, this.y];
          this.cubic(rx, ry, ox + reader.number(), oy + reader.number(), ox + reader.number(), oy + reader.number());
          break;
        }
        case 'Q':
          this.quad(ox + reader.number(), oy + reader.number(), ox + reader.number(), oy + reader.number());
          break;
        case 'T': {
          const [qx, qy] = this.lastQuad ? [2 * this.x - this.lastQuad[0], 2 * this.y - this.lastQuad[1]] : [this.x, this.y];
          this.quad(qx, qy, ox + reader.number(), oy + reader.number());
          break;
        }
        case 'A': {
          const [rx, ry, rotation] = [reader.number(), reader.number(), reader.number()];
          const [largeArc, sweep] = [reader.flag(), reader.flag()];
          this.arc(rx, ry, rotation, largeArc, sweep, ox + reader.number(), oy + reader.number());
          break;
        }
        case 'Z':
          this.close();
          // Z 后面不能再省略命令
          command = undefined;
          continue;
        default:
          throw new Error(`不认识的路径命令 ${command}：${d}`);
      }
    }
  }

  /** 紧凑写法：命令字母紧跟数字，数字之间一个空格（`M12 2C6.48 2 2 6.48 2 12Z`），Avalonia、WPF 的路径语法都认 */
  toString() {
    return this.segments.map(([c, ...values]) => c + values.map(num).join(' ')).join('');
  }
}

const numbers = (text) => text.trim().split(/[\s,]+/).map(Number);
const attribute = (attrs, name, fallback) => (attrs[name] === undefined ? fallback : Number(attrs[name]));

/** 允许出现的属性（其余的如 transform、stroke 说明图标不是 Lucide 的标准画法，得专门处理） */
const ALLOWED = new Set(['key', 'd', 'cx', 'cy', 'r', 'rx', 'ry', 'x', 'y', 'width', 'height', 'x1', 'y1', 'x2', 'y2', 'points', 'fill']);

/**
 * Lucide 的 node（`[[标签, 属性, 子节点?], …]`）→ `{ path, fillPath }`：所有元素都描边（path）；
 * 写了 fill="currentColor" 的元素另外填充（fillPath，Lucide 里少数图标的实心点），没有时为 undefined
 */
export function normalize(nodes) {
  const stroke = new Builder();
  const fill = new Builder();
  for (const node of nodes) {
    const fillValue = node[1].fill;
    if (fillValue !== undefined && fillValue !== 'none' && fillValue !== 'currentColor') throw new Error(`不支持的 fill="${fillValue}"`);
    draw(stroke, node);
    if (fillValue === 'currentColor') draw(fill, node);
  }
  return { path: stroke.toString(), fillPath: fill.segments.length ? fill.toString() : undefined };
}

function draw(builder, node) {
  const visit = ([tag, attrs, children]) => {
    for (const name of Object.keys(attrs)) if (!ALLOWED.has(name)) throw new Error(`${tag} 有不支持的属性 ${name}`);
    switch (tag) {
      case 'path':
        builder.path(attrs.d);
        break;
      case 'circle':
        builder.ellipse(attribute(attrs, 'cx', 0), attribute(attrs, 'cy', 0), Number(attrs.r), Number(attrs.r));
        break;
      case 'ellipse':
        builder.ellipse(attribute(attrs, 'cx', 0), attribute(attrs, 'cy', 0), Number(attrs.rx), Number(attrs.ry));
        break;
      case 'line':
        builder.move(attribute(attrs, 'x1', 0), attribute(attrs, 'y1', 0));
        builder.line(attribute(attrs, 'x2', 0), attribute(attrs, 'y2', 0));
        break;
      case 'polyline':
      case 'polygon': {
        const points = numbers(attrs.points);
        builder.move(points[0], points[1]);
        for (let i = 2; i < points.length; i += 2) builder.line(points[i], points[i + 1]);
        if (tag === 'polygon') builder.close();
        break;
      }
      case 'rect': {
        const [x, y, width, height] = [attribute(attrs, 'x', 0), attribute(attrs, 'y', 0), Number(attrs.width), Number(attrs.height)];
        let rx = attribute(attrs, 'rx', attribute(attrs, 'ry', 0));
        let ry = attribute(attrs, 'ry', rx);
        rx = Math.min(rx, width / 2);
        ry = Math.min(ry, height / 2);
        const corner = (cx, cy, from) => builder.ellipseArc(cx, cy, rx, ry, 0, from, Math.PI / 2);
        // 圆角正好占满一边时边长为 0，不写这段线（在闭合的轮廓里画不出东西）
        const edge = (ex, ey) => {
          if (ex !== builder.x || ey !== builder.y) builder.line(ex, ey);
        };
        builder.move(x + rx, y);
        edge(x + width - rx, y);
        if (rx && ry) corner(x + width - rx, y + ry, -Math.PI / 2);
        edge(x + width, y + height - ry);
        if (rx && ry) corner(x + width - rx, y + height - ry, 0);
        edge(x + rx, y + height);
        if (rx && ry) corner(x + rx, y + height - ry, Math.PI / 2);
        edge(x, y + ry);
        if (rx && ry) corner(x + rx, y + ry, Math.PI);
        builder.close();
        break;
      }
      case 'g':
        break;
      default:
        throw new Error(`不支持的元素 ${tag}`);
    }
    for (const child of children ?? []) visit(child);
  };
  visit(node);
}
