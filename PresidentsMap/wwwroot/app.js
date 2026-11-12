// Map page. All state of record lives on the server; this file only draws it.
const $ = id => document.getElementById(id);
const NS = 'http://www.w3.org/2000/svg';
const money = n => '$' + Number(n).toLocaleString('en-US');
const { t, fmt, lang } = I18N;
const todayISO = () => { const n = new Date(); return new Date(n.getTime() - n.getTimezoneOffset() * 6e4).toISOString().slice(0, 10); };
const LS = 'presidents-map';

let MAP = [], byId = {}, paths = {};
let selected = 'GE', date = todayISO();
let day = { multiplier: 1, special: null, gifts: {} };   // gifts: country code -> {to, from}
let summary = { count: 0, total: 0, recent: [] };
let mine = {};        // "GE_2027-02-14" -> certificate id, remembered in this browser
let certs = {};       // certificate id -> full gift
let stage = 'idle', quoted = 0, err = '', loadErr = '', panelKey = '';
const draft = { to: '', from: '', msg: '' };

try { const s = JSON.parse(localStorage.getItem(LS) || '{}'); mine = s.mine || {}; draft.from = s.from || ''; } catch (e) { }
const save = () => { try { localStorage.setItem(LS, JSON.stringify({ mine, from: draft.from })); } catch (e) { } };

const cur = () => selected + '_' + date;
const priceOf = id => byId[id].p * day.multiplier;

async function api(path, body, orderId) {
  const res = await fetch(path, body
    ? { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Lang': lang, ...(orderId ? { 'X-Payment-Order': orderId } : {}) }, body: JSON.stringify(body) }
    : { headers: { 'X-Lang': lang } });
  const data = await res.json().catch(() => null);
  if (!res.ok) throw { status: res.status, message: data && data.error };
  if (res.status === 202) return { pending: data.paymentOrderId };   // buyer must approve the payment first
  return data;
}

function h(tag, props, ...kids) {
  const el = document.createElement(tag);
  for (const k in props || {}) {
    const v = props[k];
    if (k === 'class') el.className = v; else if (k === 'text') el.textContent = v;
    else if (k.startsWith('on')) el.addEventListener(k.slice(2), v);
    else if (v === true) el.setAttribute(k, ''); else if (v !== false && v != null) el.setAttribute(k, v);
  }
  for (const kid of kids.flat()) if (kid != null && kid !== false) el.append(kid);
  return el;
}

/* ---------- data ---------- */
async function loadDay() {
  const asked = date;
  try {
    const d = await api('/api/days/' + asked);
    if (asked !== date) return;            // the user already moved to another date
    day = { multiplier: d.multiplier, special: d.special, gifts: Object.fromEntries(d.gifts.map(g => [g.country, g])) };
    loadErr = '';
  } catch (e) { loadErr = t('ამ დღის მონაცემები ვერ ჩაიტვირთა. შეამოწმე კავშირი და სცადე თავიდან.'); }
  render();
}
async function loadSummary() {
  try { summary = await api('/api/summary'); } catch (e) { }
  renderStats(); renderBoard();
}
async function loadCert(id) {
  try { certs[id] = await api('/api/gifts/' + id); }
  catch (e) { if (e.status === 404) { for (const k in mine) if (mine[k] === id) delete mine[k]; save(); } }
  panelKey = ''; render();
}

/* ---------- map ---------- */
const svg = $('map');
let vb = [0, 0, 1000, 500], anim = 0;
function zoomTo(target) {
  cancelAnimationFrame(anim);
  const from = vb.slice(), t0 = performance.now();
  const still = matchMedia('(prefers-reduced-motion: reduce)').matches;
  const step = now => {
    const k = still ? 1 : Math.min(1, (now - t0) / 320), e = 1 - Math.pow(1 - k, 3);
    vb = from.map((v, i) => v + (target[i] - v) * e); svg.setAttribute('viewBox', vb.map(v => v.toFixed(1)).join(' '));
    if (k < 1) anim = requestAnimationFrame(step);
  };
  anim = requestAnimationFrame(step);
  $('reset').hidden = target[2] >= 1000;
}
function zoomCountry(id) {
  const [x0, y0, x1, y1] = byId[id].b;
  let w = Math.max(x1 - x0 + 70, (y1 - y0 + 70) * 2, 240); w = Math.min(w, 1000); const hh = w / 2;
  let x = (x0 + x1) / 2 - w / 2, y = (y0 + y1) / 2 - hh / 2;
  x = Math.max(0, Math.min(1000 - w, x)); y = Math.max(0, Math.min(500 - hh, y));
  zoomTo([x, y, w, hh]);
}
$('reset').addEventListener('click', () => zoomTo([0, 0, 1000, 500]));

function select(id, zoom, d) {
  if (stage === 'saving') return;
  selected = id; stage = 'idle'; err = '';
  if (zoom) zoomCountry(id);
  if (d && d !== date) { date = d; dateEl.value = d; day = { multiplier: 1, special: null, gifts: {} }; loadDay(); }
  render();
}
const dateEl = $('date'); dateEl.value = date; dateEl.min = date;
dateEl.addEventListener('change', () => {
  if (stage === 'saving' || !/^\d{4}-\d{2}-\d{2}$/.test(dateEl.value)) { dateEl.value = date; return; }
  date = dateEl.value; stage = 'idle'; err = ''; day = { multiplier: 1, special: null, gifts: {} };
  render(); loadDay();
});

/* ---------- purchase ---------- */
function startPay() {
  if (draft.to.trim().length < 2) { err = t('ჩაწერე, ვის ჩუქნი (მინიმუმ 2 სიმბოლო).'); return render(); }
  err = ''; quoted = priceOf(selected); stage = 'confirm'; save(); render();
}
// The purchase request. Sent once in demo mode; with a payment provider it is sent twice,
// the second time carrying the order the buyer approved.
function giftAttempt() {
  const key = cur(), body = { country: selected, date, to: draft.to, from: draft.from, message: draft.msg, lang };
  return {
    key,
    send: orderId => api('/api/gifts', body, orderId),
  };
}
async function giftDone(key, g) {
  certs[g.id] = g; mine[key] = g.id; save();
  draft.to = ''; draft.msg = '';
  stage = 'idle';
  await Promise.all([loadDay(), loadSummary()]);
}
async function giftFailed(e) {
  err = (e && e.message) || (e && e.status === 429 ? t('ძალიან ბევრი მცდელობაა. დაელოდე ერთი წუთი და სცადე თავიდან.') : t('საჩუქარი ვერ შეინახა. შეამოწმე კავშირი და სცადე თავიდან.'));
  stage = 'idle';
  await Promise.all([loadDay(), loadSummary()]);
}
async function pay() {
  const a = giftAttempt();
  stage = 'saving'; render();
  try { await giftDone(a.key, await a.send(null)); } catch (e) { await giftFailed(e); }
}

/* ---------- render ---------- */
function render() { renderMap(); renderList(); renderPanel(); }
const cls = id => mine[id + '_' + date] ? ' mine' : day.gifts[id] ? ' taken' : '';

function renderMap() {
  for (const c of MAP) paths[c.id].setAttribute('class', 'c' + cls(c.id) + (c.id === selected ? ' sel' : ''));
  const p = paths[selected]; if (p && p.nextSibling) svg.append(p);
  $('date-note').textContent = loadErr || fmt(date) + (day.special ? ' · ' + day.special + t(': ფასი ორმაგია') : '');
}
function renderStats() {
  $('st-taken').textContent = summary.count;
  $('st-sum').textContent = money(summary.total);
}
function renderBoard() {
  const rows = summary.recent;
  $('board').hidden = !rows.length; $('board-empty').hidden = !!rows.length;
  $('board').replaceChildren(...rows.filter(x => byId[x.country]).map((x, i) => h('li', { onclick: () => select(x.country, true, x.date) },
    h('span', { class: 'rk', text: i + 1 }),
    h('span', { class: 'nm' }, x.to + (mine[x.country + '_' + x.date] ? t(' (შენი საჩუქარი)') : ''), h('span', { text: byId[x.country].n + ' · ' + fmt(x.date) })),
    h('span', { class: 'pr', text: money(x.price) }))));
}
function renderList() {
  const q = $('search').value.trim().toLowerCase();
  $('list').replaceChildren(...MAP.filter(c => !q || c.n.toLowerCase().includes(q)).map(c =>
    h('button', { type: 'button', class: c.id === selected ? 'sel' : '', onclick: () => select(c.id, true) },
      h('span', {}, h('i', { class: 'dot' + cls(c.id) }), c.n),
      h('span', { text: money(priceOf(c.id)) }))));
}
$('search').addEventListener('input', renderList);

function shareLinks(msg, url) {
  const t = encodeURIComponent(msg), u = encodeURIComponent(url), tu = encodeURIComponent(msg + ' ' + url);
  return [['Facebook', 'https://www.facebook.com/sharer/sharer.php?u=' + u],
  ['X', 'https://twitter.com/intent/tweet?text=' + t + '&url=' + u],
  ['WhatsApp', 'https://wa.me/?text=' + tu],
  ['Telegram', 'https://t.me/share/url?url=' + u + '&text=' + t]];
}
function certificate(c, g) {
  const url = location.origin + '/cert/' + g.id;
  const copyBtn = h('button', {
    class: 'btn ghost', type: 'button', text: t('ბმულის კოპირება'), onclick: async () => {
      try { await navigator.clipboard.writeText(url); copyBtn.textContent = t('დაკოპირდა'); }
      catch (e) { copyBtn.textContent = t('კოპირება ვერ მოხერხდა'); }
    }
  });
  const msg = t('საჩუქარი: ') + g.to + t(' ერთი დღით პრეზიდენტია. ქვეყანა: ') + c.n + t(', დღე: ') + fmt(g.date) + '.';
  return [h('div', { class: 'cert' },
    h('div', { class: 'k', text: t('საპატიო პრეზიდენტის სერტიფიკატი') }),
    h('div', { class: 'no', text: '№ ' + g.number }),
    h('div', { class: 'who', text: g.to }),
    h('div', { class: 'land', text: t('ქვეყანა: ') + c.n }),
    h('div', { text: t('მმართველობის დღე: ') + fmt(g.date) }),
    g.message ? h('div', { class: 'motto', text: (lang === 'en' ? '“' : '„') + g.message + (lang === 'en' ? '”' : '“') }) : null,
    g.from ? h('div', { text: t('გამჩუქებელი: ') + g.from }) : null,
    h('div', { class: 'foot', text: t('სიმბოლური ტიტული. არ იძლევა არანაირ იურიდიულ ან პოლიტიკურ უფლებას.') })),
  h('a', { class: 'btn', href: '/cert/' + g.id, target: '_blank', rel: 'noopener', text: t('სერტიფიკატი და მთავრობის დანიშვნა') }),
  h('div', { class: 'share' }, h('div', { class: 'small', text: t('გაუგზავნე ბმული ან გააზიარე') }), h('div', { class: 'row' },
    ...shareLinks(msg, url).map(([n, u]) => h('a', { class: 'btn ghost', href: u, target: '_blank', rel: 'noopener noreferrer', text: n })),
    copyBtn))];
}

function renderPanel() {
  if (!byId[selected]) return;
  const c = byId[selected], certId = mine[cur()], taken = day.gifts[selected];
  const key = [cur(), stage, err, quoted, certId, JSON.stringify(taken || 0), day.multiplier, !!certs[certId]].join('|');
  if (key === panelKey) return; panelKey = key;
  const price = priceOf(selected);
  const kids = [h('div', {}, h('div', { class: 'eyebrow', text: fmt(date) + ' · ' + (certId ? t('შენი საჩუქარი') : taken ? t('ეს დღე დაკავებულია') : t('თავისუფალია')) }), h('h2', { text: c.n }))];

  if (certId) {
    if (certs[certId]) kids.push(...certificate(c, certs[certId]));
    else { kids.push(h('p', { class: 'small', text: t('სერტიფიკატი იტვირთება…') })); loadCert(certId); }
  } else if (taken) {
    kids.push(h('dl', { class: 'facts' },
      h('dt', { text: t('პრეზიდენტი ამ დღეს') }), h('dd', { text: taken.to }),
      taken.from ? [h('dt', { text: t('გამჩუქებელი') }), h('dd', { text: taken.from })] : null),
      h('p', { class: 'small', text: t('ეს ქვეყანა ამ დღეს უკვე გაჩუქებულია. ზემოთ აირჩიე სხვა თარიღი ან რუკაზე სხვა ქვეყანა.') }));
  } else if (stage === 'idle') {
    kids.push(
      h('label', { class: 'field' }, t('ვის ჩუქნი'),
        h('input', { id: 'f-to', type: 'text', maxlength: '40', value: draft.to, oninput: e => { draft.to = e.target.value; } })),
      h('label', { class: 'field' }, t('შენი სახელი (არასავალდებულო)'),
        h('input', { id: 'f-from', type: 'text', maxlength: '40', autocomplete: 'name', value: draft.from, oninput: e => { draft.from = e.target.value; } })),
      h('label', { class: 'field' }, t('მილოცვა სერტიფიკატზე (არასავალდებულო)'),
        h('textarea', { id: 'f-msg', maxlength: '140', oninput: e => { draft.msg = e.target.value; } }, draft.msg)),
      err ? h('p', { class: 'err', text: err }) : null,
      h('button', { class: 'btn', type: 'button', onclick: startPay, text: t('აჩუქე ეს დღე · ') + money(price) }),
      h('p', { class: 'small', text: t('ფასი ქვეყნის ფართობზეა დამოკიდებული. სადღესასწაულო დღეებში (ვალენტინობა, 8 მარტი, ახალი წელი) ორმაგია.') }));
  } else {
    kids.push(h('div', { class: 'pay' },
      h('div', { text: t('გადასახდელი თანხა') }), h('strong', { text: money(quoted) }),
      h('div', { text: draft.to.trim() + ' · ' + c.n + ' · ' + fmt(date) }),
      Pay.enabled ? h('div', { id: 'pay-buttons' }) : h('p', { class: 'small', text: t('ეს დემო გადახდაა. ბარათი არ გჭირდება და თანხა არ ჩამოგეჭრება.') }),
      h('div', { class: 'row' },
        Pay.enabled ? null : h('button', { class: 'btn', type: 'button', disabled: stage === 'saving', onclick: pay, text: stage === 'saving' ? t('ინახება…') : t('გადახდის დადასტურება') }),
        h('button', { class: 'btn ghost', type: 'button', disabled: stage === 'saving', onclick: () => { stage = 'idle'; render(); }, text: t('გაუქმება') }))));
  }
  $('dossier').replaceChildren(...kids.flat().filter(Boolean));
  if ($('pay-buttons')) { const a = giftAttempt(); Pay.mount($('pay-buttons'), a.send, g => giftDone(a.key, g), giftFailed); }
}

/* ---------- start ---------- */
(async () => {
  try { MAP = await api('/data/countries.json'); }
  catch (e) { $('dossier').replaceChildren(h('p', { class: 'err', text: t('რუკა ვერ ჩაიტვირთა. გადატვირთე გვერდი.') })); return; }
  if (lang === 'en') { for (const c of MAP) c.n = c.e; MAP.sort((a, b) => a.n.localeCompare(b.n, 'en')); }
  byId = Object.fromEntries(MAP.map(c => [c.id, c]));
  for (const c of MAP) {
    const p = document.createElementNS(NS, 'path'); p.setAttribute('d', c.d); p.setAttribute('class', 'c');
    const t = document.createElementNS(NS, 'title'); t.textContent = c.n; p.append(t);
    p.addEventListener('click', () => select(c.id, true)); svg.append(p); paths[c.id] = p;
  }
  render();
  loadDay(); loadSummary();
  Pay.ready.then(() => { $('mode').hidden = Pay.enabled; panelKey = ''; render(); });
})();
