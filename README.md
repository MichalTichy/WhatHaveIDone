# What Have I Done?!

Minimalistická osobní evidence času: .NET 10, Blazor WebAssembly/PWA, Blazor Blueprint UI, PostgreSQL a Marten. Jeden API host obsluhuje také web. Každý uživatel má vlastní data, bez organizací a workspaces.

Implementováno je přihlášení/registrace, serverové Entra SSO pro jeden tenant, časovač, seznam s automatickým ukládáním, editovatelný kalendář, projekty, dynamické tagy, přehledy/CSV, API klíče, MCP připojení AI asistenta a import na pozadí přes Toggl API nebo podporované CSV. Vybraný profil Toggl v9 slouží pro navazující úpravu Riganti IS; nejde o úplnou kopii Togglu. Emailing, reset hesla a offline editace nejsou součástí první verze.

## Lokální start

Potřebujete SDK z [global.json](global.json), Node.js 24/npm a Docker s Compose. V PowerShellu z kořene:

```powershell
./eng/start.ps1
```

Skript vytvoří ignorovaný `.env` s náhodným heslem, spustí PostgreSQL a aplikaci na **http://localhost:5088**. Restore a sestavení CSS/kalendáře jsou součástí .NET buildu. Ve druhém terminálu lze založit syntetická data:

```powershell
./eng/seed.ps1
```

Účet je `demo@example.test`; jeho vygenerované heslo najdete pouze v lokálním `.env` jako `DEMO_PASSWORD`. Seed lze opakovat, existující změny nepřepisuje. Nový vlastní účet můžete založit přímo v aplikaci. Při prvním přihlášení se otevře Import, který lze přeskočit.

Celá aplikace v kontejnerech, bez lokálního SDK/Node:

```powershell
# Poprvé: Copy-Item .env.example .env a nastavte vlastní heslo.
docker compose --profile app up -d --build --wait
```

Na portu 5088 smí běžet jen jedna z variant. Data i ochranné klíče mají persistentní volumes.

## Ověření a struktura

```powershell
./eng/start.ps1 -DatabaseOnly
dotnet test WhatHaveIDone.slnx -warnaserror
dotnet publish src/WhatHaveIDone.API -c Release -o artifacts/publish -warnaserror
```

Integrační testy vytvářejí a odstraňují vlastní databázi `whid_tests_<guid>`. Čtou lokální `.env`, případně `WHID_TEST_CONNECTION`; připojení potřebuje oprávnění vytvořit databázi. Nepoužívejte produkční účet. Build zahrnuje npm/CSS a nepoužívá interní NuGet feed ani zdrojové checkouty RSM/Edifio.

`src/` obsahuje Web, API, API.Toggl, API.Models, Models, Logic a dva konkrétní persistence/Identity adaptéry. API hostuje nativní rozhraní pro frontend; samostatná knihovna API.Toggl vlastní kompatibilní v9 endpointy, DTO a formát chyb. Nativní API, Toggl adaptér i MCP v API/Mcp používají stejnou Logic a běží v jednom serveru. Čtrnáct potřebných projektů v `SharedInfrastructure/` jsou lokální kopie. Zápisy jdou přes use cases, čtení přes specifikace; WASM zná jen vlastní HTTP kontrakty.

Autentizace přímo používá ASP.NET Identity UserManager/SignInManager v account endpointech a standardním OIDC eventu v Infrastructure.Identity. Společná privátní sign-in metoda doplní chybějící profil a výchozího klienta v jedné Marten transakci a až potom vydá cookie. Autentizace nemá MediatR wrappery ani account/provisioning service; tracking operace dál používají use cases.

Marten používá společné tabulky s tenantem pro každého uživatele. Scoped `CurrentTenancyProvider` získává ID z existujícího claimu `NameIdentifier` přes sdílený `HttpContextCurrentUserIdProvider`; klient nemůže tenant zvolit hlavičkou, parametrem ani workspace ID. Běžné dokumenty mají `Id` a `Version`, bez duplicitního `OwnerId`. Globální API klíče si ponechávají `UserId` pro autentizaci a kontrolu vlastníka. Worker hledá čekající importy explicitním dotazem napříč tenanty a zápisy provádí ve scope jejich uživatele.

Stav importu je v `UserProfile.Import`; až 19 předchozích výsledků je kompaktně v `ImportHistory`. Dokončení a zrušení odstraní token, souborovou cestu, náhled i pomocná metadata. API dál vrací nejvýše 20 běhů. Import a nastavení mají vlastní verze, aby průběh nezneplatňoval nesouvisející editaci nastavení. Startup před změnou Marten schématu transakčně převede staré vlastnictví a importy; nejednoznačná data zastaví migraci místo jejich přiřazení do výchozího tenantu.

Soubory uvnitř projektů jsou seskupené podle funkcí, například `TimeEntries`, `Projects`, `Import` a `Settings`. API DTO jsou v odpovídajících složkách `API.Models`, doménové dokumenty v `Models` a use cases se specifikacemi v `Logic`. Společné modelové namespaces zůstávají stabilní, včetně identit uložených dokumentů.

Ve Webu jsou stránky i jejich související komponenty ve `Features/Authentication`, `Time`, `Projects`, `Reporting`, `Import` a `Settings`. Například `Features/Time` obsahuje stránku Čas, editor záznamu i zdrojové skripty kalendáře; `Features/Settings` obsahuje také MCP dialog. Znovupoužitelné UI zůstává v `Components`, shell v `Layout`. API testy mají stejné funkční členění a společnou testovací infrastrukturu ve složce `Support`.


- [Plánovací návrh stránky Čas a jeho omezení](prototype/doc/components.md)
- [Pravidla repozitáře](AGENTS.md)
- [Architektonický skill](.agents/Skills/dotnet-application-architecture/SKILL.md)
- [Práce s lokální Shared.Infrastructure](.agents/Skills/shared-infra/SKILL.md)

Plánovací a ověřovací poznámky ve složce `docs/` zůstávají lokální a nejsou součástí repozitáře. Nastavení MCP je dostupné přímo v **Nastavení → API klíče → Připojit AI / MCP**. Nabízí prompt pro AI a konfigurace VS Code, Cursor a Claude Code pro adresu `/mcp`. Klíč nastavte soukromě jako `WHID_API_KEY` v prostředí AI klienta; prompt ani konfigurace skutečný klíč neobsahují. MCP používá Bearer klíč a stejná aplikační pravidla jako GUI.

Lokálně kopírované infrastrukturní projekty mají vlastní [manifest původu](SharedInfrastructure/PROVENANCE.md). UI komponenty a serverový auth tok vycházejí z RSM commitu `7eb0637e06fdafc53469c66b3650db0e16af2b06` a jsou přizpůsobené WASM a osobním účtům. DM Sans v `src/WhatHaveIDone.Web/wwwroot/fonts/` pochází z google/fonts commitu `e7db6ca603fd4862010383ff17ffd6cc4ffac1c0`; původní SIL OFL 1.1 je přiložená u fontu. Timely a Timemator jsou vizuální inspirací, jejich kód ani značkové podklady se nekopírují.

První navazující funkcí je **draft výkazu**, oddělený od potvrzených záznamů a reportů. MVP kvůli němu neobsahuje prázdné projekty, sběrače ani menu.

Přenos se skutečným Toggl účtem, přihlášení přes skutečného Entra poskytovatele a synchronizace s upraveným Riganti IS vyžadují ověření v nakonfigurovaném prostředí. Lokálně jsou pokryté protokolové odpovědi, kvóty, obnovování importu a provisioning účtů. Před veřejným open-source vydáním zbývá vyřešit distribuční práva převzatého interního kódu a licenci výsledné aplikace; zdrojový checkout licenci neposkytoval.

UI controls use BlazorBlueprint.Components 4.2.0 with the application Tailwind build and a warm light theme. Component CSS is served locally from the package; ES modules load on demand. FullCalendar remains the editable calendar.
