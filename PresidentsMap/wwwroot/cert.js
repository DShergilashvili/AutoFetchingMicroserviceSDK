// Cabinet on the certificate page: appoint a prime minister and ministers.
(() => {
  const root = document.getElementById('cabinet');
  if (!root) return;
  const id = root.dataset.id;
  const money = n => '$' + n;
  const { t, lang } = I18N;
  let cab = null, open = null, busy = false, err = '', name = '';

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
  async function api(path, body, orderId) {
    const res = await fetch(path, body !== undefined
      ? { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Lang': lang, ...(orderId ? { 'X-Payment-Order': orderId } : {}) }, body: JSON.stringify(body) }
      : { headers: { 'X-Lang': lang } });
    const data = await res.json().catch(() => null);
    if (!res.ok) throw { status: res.status, message: data && data.error };
    if (res.status === 202) return { pending: data.paymentOrderId };   // buyer must approve the payment first
    return data;
  }
  function done(view) { cab = view; open = null; name = ''; err = ''; busy = false; render(); }
  async function failed(e) {
    err = (e && e.message) || (e && e.status === 429 ? t('ძალიან ბევრი მცდელობაა. დაელოდე ერთი წუთი.') : t('ვერ შეინახა. შეამოწმე კავშირი და სცადე თავიდან.'));
    if (e && e.status === 409) { try { cab = await api('/api/gifts/' + id + '/cabinet'); } catch (_) { } }
    busy = false; render();
  }
  // Demo mode, or a seat that costs nothing: one request and it is done.
  async function send(path, body) {
    if (busy) return;
    busy = true; err = ''; render();
    try { done(await api(path, body)); } catch (e) { await failed(e); }
  }
  // With a payment provider: its button sends the request, the buyer approves, the button sends it again.
  function mountPay(path, body) {
    const el = document.getElementById('pay-buttons');
    if (el) Pay.mount(el, orderId => api(path, body(), orderId), done, failed);
  }

  function row(p) {
    const taken = !!p.name;
    const head = taken
      ? h('div', { class: 't' }, h('span', { text: p.name }), h('small', { text: p.title + (p.coups ? t(' · გადატრიალება №') + p.coups : '') }))
      : h('div', { class: 't' }, h('span', { text: p.title }), h('small', { text: t('თავისუფალია') }));
    if (open !== p.key) return h('li', {}, head,
      h('button', {
        class: 'btn ghost' + (taken ? ' take' : ''), type: 'button', disabled: busy,
        text: taken ? t('წაართვი სკამი · ') + money(p.price) : t('დანიშნე · ') + (p.price ? money(p.price) : t('უფასოდ')),
        onclick: () => { open = p.key; err = ''; name = ''; render(); document.getElementById('post-name')?.focus(); }
      }));
    return h('li', {}, head,
      h('form', { onsubmit: e => { e.preventDefault(); if (Pay.enabled && p.price) return; send('/api/gifts/' + id + '/cabinet', { post: p.key, name, price: p.price }); } },
        h('input', { id: 'post-name', type: 'text', maxlength: '40', placeholder: taken ? t('ვინ იკავებს სკამს') : t('ვის ნიშნავ'), 'aria-label': t('სახელი'), value: name, oninput: e => { name = e.target.value; } }),
        Pay.enabled && p.price ? null : h('button', { class: 'btn' + (taken ? ' coup' : ''), type: 'submit', disabled: busy, text: busy ? t('ინახება…') : (p.price ? t('გადახდა ') + money(p.price) : t('დანიშვნა')) }),
        h('button', { class: 'btn ghost', type: 'button', disabled: busy, text: t('გაუქმება'), onclick: () => { open = null; err = ''; render(); } })),
      Pay.enabled && p.price ? h('div', { id: 'pay-buttons', 'data-post': p.key, 'data-price': p.price }) : null,
      err ? h('p', { class: 'err', text: err }) : null);
  }

  function render() {
    if (!cab) { root.replaceChildren(h('p', { class: 'small', text: err || t('მთავრობა იტვირთება…') })); return; }
    const empty = cab.posts.filter(p => !p.name).length;
    const kids = [
      h('h2', { text: t('მთავრობა') }),
      h('p', { class: 'small', text: t('პრეზიდენტს მთავრობა სჭირდება. დანიშნე პრემიერი და მინისტრები, ან გაუგზავნე ეს ბმული მეგობრებს. დაკავებული სკამის წართმევაც შეიძლება: გადატრიალება 50%-ით მეტი ღირს.') }),
    ];
    if (cab.prepaid && empty) kids.push(h('p', { class: 'small', text: t('სრული მთავრობა გადახდილია. დარჩენილი თანამდებობები უფასოდ ივსება.') }));
    if (!cab.prepaid && empty >= 2) kids.push(h('div', { class: 'bundle' },
      h('div', {}, h('strong', { text: t('სრული მთავრობა ერთად · ') + money(cab.bundlePrice) + ' ' }), h('s', { text: money(cab.fullPrice) }),
        h('div', { class: 'small', text: t('დარჩენილი ') + empty + t(' თანამდებობა 30%-ით იაფად. სახელებს მერე ჩაწერ.') })),
      open === '#bundle' && Pay.enabled ? h('div', { id: 'pay-buttons', 'data-post': '#bundle' })
        : h('button', {
          class: 'btn', type: 'button', disabled: busy, text: t('ყიდვა ') + money(cab.bundlePrice),
          onclick: () => { if (Pay.enabled) { open = '#bundle'; err = ''; render(); } else send('/api/gifts/' + id + '/cabinet/bundle', {}); }
        })));
    if (err && (!open || open === '#bundle')) kids.push(h('p', { class: 'err', text: err }));
    kids.push(h('ul', { class: 'posts' }, cab.posts.map(row)));
    if (!Pay.enabled) kids.push(h('p', { class: 'small', text: t('დემო რეჟიმი: გადახდა სიმულაციაა, თანხა არ ჩამოგეჭრება.') }));
    root.replaceChildren(...kids);
    const pb = document.getElementById('pay-buttons');
    if (pb && pb.dataset.post === '#bundle') mountPay('/api/gifts/' + id + '/cabinet/bundle', () => ({}));
    else if (pb) mountPay('/api/gifts/' + id + '/cabinet', () => ({ post: pb.dataset.post, name, price: Number(pb.dataset.price) }));
  }

  render();
  Pay.ready.then(render);
  api('/api/gifts/' + id + '/cabinet').then(c => { cab = c; render(); }, () => { err = t('მთავრობა ვერ ჩაიტვირთა. გადატვირთე გვერდი.'); render(); });
})();
