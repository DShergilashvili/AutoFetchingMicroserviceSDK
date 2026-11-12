// Two languages, gettext style: Georgian text is the key, EN holds its English.
(() => {
  const EN = {
    // page chrome (index.html)
    'პრეზიდენტების რუკა': 'Presidents Map',
    'აჩუქე ქვეყანა ერთი დღით: სიმბოლური პრეზიდენტის სერტიფიკატი საყვარელი ადამიანისთვის.': 'Gift a country for a day: an honorary president certificate for someone you love.',
    'აჩუქე ქვეყანა ერთი დღით. აირჩიე ქვეყანა და თარიღი, ჩაწერე ვის ჩუქნი, და იმ დღეს ის ამ ქვეყნის სიმბოლური პრეზიდენტია. თითო ქვეყანა თითო დღეს მხოლოდ ერთ ადამიანს ეკუთვნის.': 'Gift a country for a day. Pick a country and a date, say who it is for, and on that day they are its honorary president. Each country belongs to only one person per day.',
    'გაჩუქებული დღეები': 'Days gifted',
    'ხაზინა': 'Treasury',
    'დემო რეჟიმი.': 'Demo mode.',
    'გადახდა სიმულაციაა, რეალური თანხა არ იხდება.': 'Payments are simulated; no real money is charged.',
    'საჩუქრის დღე': 'Gift day',
    'მსოფლიო რუკა': 'World map',
    'მთელი რუკა': 'Whole map',
    'თავისუფალი ამ დღეს': 'Available that day',
    'დაკავებული': 'Taken',
    'შენი საჩუქარი': 'Your gift',
    'ბოლო საჩუქრები': 'Latest gifts',
    'ჯერ არავის არაფერი უჩუქებია. პირველი საჩუქრის შემდეგ აქ გამოჩნდება სია.': 'Nobody has gifted anything yet. The list appears here after the first gift.',
    'ყველა ქვეყანა': 'All countries',
    'მოძებნე ქვეყანა': 'Find a country',
    'ქვეყნის ძებნა': 'Country search',
    // map page
    'ამ დღის მონაცემები ვერ ჩაიტვირთა. შეამოწმე კავშირი და სცადე თავიდან.': "This day's data did not load. Check your connection and try again.",
    'ჩაწერე, ვის ჩუქნი (მინიმუმ 2 სიმბოლო).': 'Enter who the gift is for (at least 2 characters).',
    'ძალიან ბევრი მცდელობაა. დაელოდე ერთი წუთი და სცადე თავიდან.': 'Too many attempts. Wait a minute and try again.',
    'საჩუქარი ვერ შეინახა. შეამოწმე კავშირი და სცადე თავიდან.': 'The gift was not saved. Check your connection and try again.',
    ': ფასი ორმაგია': ': double price',
    ' (შენი საჩუქარი)': ' (your gift)',
    'ბმულის კოპირება': 'Copy link',
    'დაკოპირდა': 'Copied',
    'კოპირება ვერ მოხერხდა': 'Could not copy',
    'საჩუქარი: ': 'A gift: ',
    ' ერთი დღით პრეზიდენტია. ქვეყანა: ': ' is president for a day. Country: ',
    ', დღე: ': ', date: ',
    'საპატიო პრეზიდენტის სერტიფიკატი': 'Honorary President Certificate',
    'ქვეყანა: ': 'Country: ',
    'მმართველობის დღე: ': 'Day in office: ',
    'გამჩუქებელი: ': 'A gift from ',
    'სიმბოლური ტიტული. არ იძლევა არანაირ იურიდიულ ან პოლიტიკურ უფლებას.': 'A symbolic title. It grants no legal or political rights.',
    'სერტიფიკატი და მთავრობის დანიშვნა': 'Certificate and cabinet',
    'გაუგზავნე ბმული ან გააზიარე': 'Send the link or share it',
    'ეს დღე დაკავებულია': 'This day is taken',
    'თავისუფალია': 'Available',
    'სერტიფიკატი იტვირთება…': 'Loading the certificate…',
    'პრეზიდენტი ამ დღეს': 'President that day',
    'გამჩუქებელი': 'Gifted by',
    'ეს ქვეყანა ამ დღეს უკვე გაჩუქებულია. ზემოთ აირჩიე სხვა თარიღი ან რუკაზე სხვა ქვეყანა.': 'This country is already gifted on this day. Pick another date above or another country on the map.',
    'ვის ჩუქნი': 'Who is it for',
    'შენი სახელი (არასავალდებულო)': 'Your name (optional)',
    'მილოცვა სერტიფიკატზე (არასავალდებულო)': 'Message on the certificate (optional)',
    'აჩუქე ეს დღე · ': 'Gift this day · ',
    'ფასი ქვეყნის ფართობზეა დამოკიდებული. სადღესასწაულო დღეებში (ვალენტინობა, 8 მარტი, ახალი წელი) ორმაგია.': "The price depends on the country's size. It doubles on holidays (Valentine's Day, March 8, New Year).",
    'გადასახდელი თანხა': 'Amount to pay',
    'ეს დემო გადახდაა. ბარათი არ გჭირდება და თანხა არ ჩამოგეჭრება.': 'This is a demo payment. You need no card and nothing is charged.',
    'ინახება…': 'Saving…',
    'გადახდის დადასტურება': 'Confirm payment',
    'გაუქმება': 'Cancel',
    'რუკა ვერ ჩაიტვირთა. გადატვირთე გვერდი.': 'The map did not load. Reload the page.',
    // cabinet
    'ძალიან ბევრი მცდელობაა. დაელოდე ერთი წუთი.': 'Too many attempts. Wait a minute.',
    'ვერ შეინახა. შეამოწმე კავშირი და სცადე თავიდან.': 'Not saved. Check your connection and try again.',
    ' · გადატრიალება №': ' · coup no. ',
    'წაართვი სკამი · ': 'Take the seat · ',
    'დანიშნე · ': 'Appoint · ',
    'უფასოდ': 'free',
    'ვინ იკავებს სკამს': 'Who takes the seat',
    'ვის ნიშნავ': 'Who gets the post',
    'სახელი': 'Name',
    'გადახდა ': 'Pay ',
    'დანიშვნა': 'Appoint',
    'მთავრობა იტვირთება…': 'Loading the cabinet…',
    'მთავრობა': 'Cabinet',
    'პრეზიდენტს მთავრობა სჭირდება. დანიშნე პრემიერი და მინისტრები, ან გაუგზავნე ეს ბმული მეგობრებს. დაკავებული სკამის წართმევაც შეიძლება: გადატრიალება 50%-ით მეტი ღირს.': 'A president needs a cabinet. Appoint a prime minister and ministers, or send this link to friends. An occupied seat can be taken too: a coup costs 50% more.',
    'სრული მთავრობა გადახდილია. დარჩენილი თანამდებობები უფასოდ ივსება.': 'The whole cabinet is paid for. The remaining posts are filled for free.',
    'სრული მთავრობა ერთად · ': 'The whole cabinet at once · ',
    'დარჩენილი ': 'The remaining ',
    ' თანამდებობა 30%-ით იაფად. სახელებს მერე ჩაწერ.': ' posts for 30% less. Add the names later.',
    'ყიდვა ': 'Buy for ',
    'დემო რეჟიმი: გადახდა სიმულაციაა, თანხა არ ჩამოგეჭრება.': 'Demo mode: payments are simulated and nothing is charged.',
    'მთავრობა ვერ ჩაიტვირთა. გადატვირთე გვერდი.': 'The cabinet did not load. Reload the page.',
  };
  const MONTHS = {
    ka: ['იანვარი', 'თებერვალი', 'მარტი', 'აპრილი', 'მაისი', 'ივნისი', 'ივლისი', 'აგვისტო', 'სექტემბერი', 'ოქტომბერი', 'ნოემბერი', 'დეკემბერი'],
    en: ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'],
  };

  // A certificate page arrives already in its own language; elsewhere: saved choice, then browser language.
  const root = document.documentElement;
  let lang = root.dataset.lang;
  if (!lang) {
    try { lang = localStorage.getItem('lang'); } catch (e) { }
    if (lang !== 'ka' && lang !== 'en') lang = (navigator.language || '').toLowerCase().startsWith('ka') ? 'ka' : 'en';
  }
  root.lang = lang;

  const t = s => lang === 'en' && EN[s] !== undefined ? EN[s] : s;
  const fmt = d => {
    const day = Number(d.slice(8)), month = MONTHS[lang][Number(d.slice(5, 7)) - 1], year = d.slice(0, 4);
    return lang === 'en' ? month + ' ' + day + ', ' + year : day + ' ' + month + ', ' + year;
  };

  // Translate the static page text in place.
  if (lang === 'en') {
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    for (let n = walker.nextNode(); n; n = walker.nextNode()) {
      const key = n.nodeValue.trim();
      if (EN[key] !== undefined) n.nodeValue = n.nodeValue.replace(key, EN[key]);
    }
    for (const el of document.querySelectorAll('[placeholder],[aria-label]'))
      for (const a of ['placeholder', 'aria-label']) if (EN[el.getAttribute(a)] !== undefined) el.setAttribute(a, EN[el.getAttribute(a)]);
    if (EN[document.title] !== undefined) document.title = EN[document.title];
    const desc = document.querySelector('meta[name="description"]');
    if (desc && EN[desc.content] !== undefined) desc.content = EN[desc.content];
  }

  const btn = document.getElementById('lang');
  if (btn) {
    btn.textContent = lang === 'en' ? 'ქართული' : 'English';
    btn.addEventListener('click', () => {
      try { localStorage.setItem('lang', lang === 'en' ? 'ka' : 'en'); } catch (e) { }
      location.reload();
    });
  }

  window.I18N = { lang, t, fmt };
})();
