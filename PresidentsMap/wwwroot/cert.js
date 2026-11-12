// Cabinet on the certificate page: appoint a prime minister and ministers.
(() => {
  const root = document.getElementById('cabinet');
  if (!root) return;
  const id = root.dataset.id;
  const money = n => '$' + n;
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
  async function api(path, body) {
    const res = await fetch(path, body !== undefined ? { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) } : undefined);
    const data = await res.json().catch(() => null);
    if (!res.ok) throw { status: res.status, message: data && data.error };
    return data;
  }
  async function send(path, body) {
    if (busy) return;
    busy = true; err = ''; render();
    try { cab = await api(path, body); open = null; name = ''; }
    catch (e) {
      err = e.message || (e.status === 429 ? 'ძალიან ბევრი მცდელობაა. დაელოდე ერთი წუთი.' : 'ვერ შეინახა. შეამოწმე კავშირი და სცადე თავიდან.');
      if (e.status === 409) { try { cab = await api('/api/gifts/' + id + '/cabinet'); } catch (_) { } }
    }
    busy = false; render();
  }

  function row(p) {
    const taken = !!p.name;
    const head = taken
      ? h('div', { class: 't' }, h('span', { text: p.name }), h('small', { text: p.title + (p.coups ? ' · გადატრიალება №' + p.coups : '') }))
      : h('div', { class: 't' }, h('span', { text: p.title }), h('small', { text: 'თავისუფალია' }));
    if (open !== p.key) return h('li', {}, head,
      h('button', {
        class: 'btn ghost' + (taken ? ' take' : ''), type: 'button', disabled: busy,
        text: taken ? 'წაართვი სკამი · ' + money(p.price) : 'დანიშნე · ' + (p.price ? money(p.price) : 'უფასოდ'),
        onclick: () => { open = p.key; err = ''; name = ''; render(); document.getElementById('post-name')?.focus(); }
      }));
    return h('li', {}, head,
      h('form', { onsubmit: e => { e.preventDefault(); send('/api/gifts/' + id + '/cabinet', { post: p.key, name, price: p.price }); } },
        h('input', { id: 'post-name', type: 'text', maxlength: '40', placeholder: taken ? 'ვინ იკავებს სკამს' : 'ვის ნიშნავ', 'aria-label': 'სახელი', value: name, oninput: e => { name = e.target.value; } }),
        h('button', { class: 'btn' + (taken ? ' coup' : ''), type: 'submit', disabled: busy, text: busy ? 'ინახება…' : (p.price ? 'გადახდა ' + money(p.price) : 'დანიშვნა') }),
        h('button', { class: 'btn ghost', type: 'button', disabled: busy, text: 'გაუქმება', onclick: () => { open = null; err = ''; render(); } })),
      err ? h('p', { class: 'err', text: err }) : null);
  }

  function render() {
    if (!cab) { root.replaceChildren(h('p', { class: 'small', text: err || 'მთავრობა იტვირთება…' })); return; }
    const empty = cab.posts.filter(p => !p.name).length;
    const kids = [
      h('h2', { text: 'მთავრობა' }),
      h('p', { class: 'small', text: 'პრეზიდენტს მთავრობა სჭირდება. დანიშნე პრემიერი და მინისტრები, ან გაუგზავნე ეს ბმული მეგობრებს. დაკავებული სკამის წართმევაც შეიძლება: გადატრიალება 50%-ით მეტი ღირს.' }),
    ];
    if (cab.prepaid && empty) kids.push(h('p', { class: 'small', text: 'სრული მთავრობა გადახდილია. დარჩენილი თანამდებობები უფასოდ ივსება.' }));
    if (!cab.prepaid && empty >= 2) kids.push(h('div', { class: 'bundle' },
      h('div', {}, h('strong', { text: 'სრული მთავრობა ერთად · ' + money(cab.bundlePrice) + ' ' }), h('s', { text: money(cab.fullPrice) }),
        h('div', { class: 'small', text: 'დარჩენილი ' + empty + ' თანამდებობა 30%-ით იაფად. სახელებს მერე ჩაწერ.' })),
      h('button', { class: 'btn', type: 'button', disabled: busy, text: 'ყიდვა ' + money(cab.bundlePrice), onclick: () => send('/api/gifts/' + id + '/cabinet/bundle', {}) })));
    if (err && !open) kids.push(h('p', { class: 'err', text: err }));
    kids.push(h('ul', { class: 'posts' }, cab.posts.map(row)));
    kids.push(h('p', { class: 'small', text: 'დემო რეჟიმი: გადახდა სიმულაციაა, თანხა არ ჩამოგეჭრება.' }));
    root.replaceChildren(...kids);
  }

  render();
  api('/api/gifts/' + id + '/cabinet').then(c => { cab = c; render(); }, () => { err = 'მთავრობა ვერ ჩაიტვირთა. გადატვირთე გვერდი.'; render(); });
})();
