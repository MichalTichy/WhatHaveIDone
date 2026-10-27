# Plánovací návrh stránky Čas

Tento návrh je samostatné HTML/CSS/JS pro diskuzi o obrazovce, nikoliv implementace aplikace nebo ověření kalendářové knihovny. Obsahuje pouze syntetická data. Produkční cíl zůstává **Blazor WebAssembly + Lumex UI/Tailwind a převzatý RSM základ**. Jiná komponentová knihovna se nepoužívá.

## Spuštění

Z kořene repozitáře `python -m http.server 5080 --bind 127.0.0.1`, potom otevřít `http://127.0.0.1:5080/prototype/`. Není potřeba instalovat balíčky ani spouštět .NET či databázi. Změny dat existují jen v paměti stránky; po obnovení se vrátí výchozí příklady. Stránka vždy začíná seznamem, poslední pohled se nepamatuje.

## Komponenty a produkční mapování

| Návrh | Zamýšlené produkční provedení |
| --- | --- |
| Tmavá navigace, profil, hlavička | Převzatý/adaptovaný RSM shell, sidebar a PageHeader |
| Primární a vedlejší tlačítka | AppActionButton / AppStatusButton nad LumexButton; akce alespoň 44 px |
| Textová pole | AppTextbox nad LumexTextbox; produkčně generované `.resx` |
| Projekt | LumexSelect, Bez projektu jako prázdná hodnota |
| Tagy | Lokální jednoduchý výběr s našeptáváním a možností nového názvu; LumexChip pro vybrané hodnoty podle skutečné potřeby |
| Přepnutí pohledu | LumexTabs / LumexTab, dvě varianty téže stránky |
| Boční editor | RSM DetailPanel, stejný formulář pro seznam a kalendář |
| Potvrzení smazání / zahození změn | LumexModal, bezpečný výchozí focus, Escape a návrat |
| Chyba / překryv | LumexAlert nebo příslušná formulářová validace |
| Krátká zpětná vazba | LumexToastProvider přes AppComponentBase |
| Seznam po dnech | Produktový seznam záznamů přímo ve Web; nepotřebuje generický grid |
| Časovač | Produktová komponenta přímo ve Web; současné zadávání a stav běžící položky jsou konkrétní společná odpovědnost |
| Kalendář | Zatím pouze vizuální HTML mřížka; kandidát produkce FullCalendar Standard musí projít samostatným ověřením |

RSM tokeny: tmavá navigace `#222b37`, pozadí `#f6fafc`, text `#202a36`, primární `#009cde`, tmavý primární text `#005a7d`, dělicí čára `#dce8ed`, základní poloměr 10 px. Návrh emuluje vzhled sémantickým HTML; nepoužívá skutečné Razor/Lumex komponenty, Tailwind build ani zkopírované RSM zdroje. Font je systémový, žádný externí font/asset nebyl převzat. Produkční dark mode tento návrh neurčuje.

## Demonstrované interakce

- Kalendář/seznam nad stejnými daty a obdobím, navigace období a Dnes, součty, desktopový týden a mobilní den.
- Spustit/zastavit, pokračování ze seznamu včetně zastavení předchozího timeru. Čas ukázky běží od pevného 26. 10. 2026 11:35, nezávisle na hodinách počítače.
- Přidat, kliknout do prázdného kalendáře, otevřít existující záznam, automaticky ukládat změny, validovat interval, upozornit na překryv a smazat s potvrzením. Text se ukládá po 500 ms bez psaní nebo při opuštění pole; výběr po změně. Zavřít dopíše poslední platné hodnoty a nevrací předchozí zápisy.
- Nový název tagu, návrhy použitých tagů, volitelný projekt/popisek. Pole tagů zatím používá datalist a čárky; finální komponenta nemusí mít totožné ovládání.
- Otevření nového formuláře samo nevytvoří položku. První platná změna jej vytvoří právě jednou; pro zcela předvyplněný interval bez změn je dostupné Vytvořit záznam. Po vytvoření zmizí a další změny se ukládají automaticky.
- Zavření křížkem/Zavřít/Escape při neplatném rozepsaném intervalu nabídne zůstat nebo zahodit jen neuložené změny. Potvrzení dočasně nahradí editor, nevrství se přes druhý otevřený dialog.
- Prázdné období a klávesnice přes nativní formuláře/dialogy. Ostatní položky navigace jsou výslovně vypnuté; jejich průchody popisuje [návrh obrazovek](../../docs/screens-and-flows.md).

## Záměrná omezení

Není zde backend, přihlášení, PWA, import, reporty, projektový editor, serverové čekání/chyby, ochrana proti souběhu ani skutečná lokalizace. Hodnoty času jsou pro názornost místní nástěnné časy; návrh **neověřuje DST ani převod UTC**. Nemá produkční ochranu běžících položek před neplatným budoucím začátkem. Překryv se pouze rozloží do dvou sloupců pro dodané příklady, neřeší obecný algoritmus mnoha překryvů.

Kalendář umožňuje kliknutí pro předvyplnění, ale **nedemonstruje výběr tažením, přesun ani resize**. Ty zůstávají součástí MVP; nebyly odložené ani splněné tímto návrhem. Běžící záznam lze zastavit v horním časovači, konec v editoru je vypnutý. Atomické přepnutí demonstruje Spustit novou v časovači i Pokračovat v seznamu. Rozhraní pod 900 px používá den, širší týden.

Na úzkém návrhu je shell zredukovaný na název produktu; plné mobilní Menu patří k implementaci shellu. Uživatel potvrdil vždy výchozí seznam a automatické ukládání editoru. Proklikání tohoto návrhu není vizuální ověření budoucí aplikace. Neuloženo/Uloženo zde vyjadřuje pouze změnu dat v paměti, ne serverový zápis; serverová chyba, opakování a konflikt verze jsou zatím jen v plánu.

## Provedené ověření návrhu

2026-10-26: původní návrh prošel kontrolou přepínání období, timeru, překryvů, tagů a responzivního editoru. Tehdejší ruční ukládání a zapamatování pohledu byly následně nahrazené potvrzenou volbou uživatele; původní kontrola tyto nové interakce nedokládá.

Aktualizovaný návrh 2026-10-26: v lokálním prohlížeči ověřené automatické uložení textu, doposlání rychlé změny při zavření, stále stejná položka při dalších úpravách, žádné vytvoření pouhým otevřením, vytvoření nezměněného intervalu, neplatný interval a zachování hodnot, Escape/zahození pouze neuložených změn a vždy seznam po přepnutí a reloadu. Kontrola editoru na 1280/768/360 px a nízkém okně 1280 × 540 prošla; zachycené chyby JavaScriptu a konzole žádné. Desktopový seznam a mobilní editor byly vizuálně prohlédnuté.

Aktuální lokální podklady (ignorované gitem): [výchozí seznam](../../artifacts/visual-checks/autosave-list-desktop.png), [mobilní editor](../../artifacts/visual-checks/autosave-editor-mobile.png), [výsledek kontroly](../../artifacts/visual-checks/autosave-verification.json). Starší podklady zůstávají jen historií předchozího návrhu. Nejde o testování produkční implementace, API ani skutečného kalendářového drag/resize.
