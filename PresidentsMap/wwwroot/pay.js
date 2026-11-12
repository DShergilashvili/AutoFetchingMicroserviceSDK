// Payment in the browser. With no PayPal key configured on the server the site runs in demo mode
// and Pay.enabled stays false; pages then use their plain "confirm" buttons.
window.Pay = (() => {
  let clientId = null, sdk = null;
  const ready = fetch('/api/config').then(r => r.json()).then(c => { clientId = c.paypalClientId || null; }).catch(() => { });

  function loadSdk() {
    if (!sdk) sdk = new Promise((resolve, reject) => {
      const s = document.createElement('script');
      s.src = 'https://www.paypal.com/sdk/js?client-id=' + encodeURIComponent(clientId) + '&currency=USD&intent=capture';
      s.onload = resolve; s.onerror = reject;
      document.head.append(s);
    });
    return sdk;
  }

  // attempt(orderId) sends the purchase to our server. Called first with null: the server answers
  // { pending: <order id> }. After the buyer approves, called again with that id: the server
  // captures the money and answers with the finished purchase.
  async function mount(el, attempt, onDone, onError) {
    try { await loadSdk(); } catch (e) { return onError({}); }
    if (!el.isConnected) return;
    paypal.Buttons({
      style: { layout: 'horizontal', tagline: false, height: 40 },
      createOrder: async () => {
        const r = await attempt(null);
        if (r && r.pending) return r.pending;
        onDone(r);                                  // nothing to pay after all
        throw { handled: true };
      },
      onApprove: async data => { try { onDone(await attempt(data.orderID)); } catch (e) { onError(e); } },
      onError: e => { if (!e || !e.handled) onError(e && e.status ? e : {}); },
    }).render(el);
  }

  return { ready, mount, get enabled() { return !!clientId; } };
})();
