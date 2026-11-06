# Handleture – design

6. november 2026 · aftalt med Marc i chatten, skrevet af Claude

Al kode, navne, kommentarer og commits på engelsk. Brugerfladetekster på dansk i `web/src/app/i18n/da.json` (aldrig
hardkodet), servertekster i `.resx` som i dag.

## 1. Hvorfor

Indkøbslisten er blevet en løbende liste: købte varer ryddes aldrig (afkrydsede varer foldes sammen nederst og
hentes side for side). Den nuværende "tur" (`POST /api/shopping-list/trip`) beregnes forfra ud fra hele listen hver
gang, den åbnes, og tager derfor alle købte varer nogensinde med. Den kan heller ikke rettes til.

Familiens rytme er: i løbet af ugen kommer varer på listen; om søndagen trykker man **"Opret handletur"**, og appen
regner ud, hvilke butikker og tilbud der skal med. Turen skal kunne tilpasses, før og under indkøbet, og bagefter
ligge som historik.

## 2. Mål

1. **Opret handletur**: et øjebliksbillede af det, der mangler at blive købt, fordelt på butikker efter den
   nuværende butiksplan (REMA altid, nr. 2 der sparer mest, nr. 3 over grænsen, butiksgrupper tæller som ét stop,
   flagede varer i deres butik).
2. **Vælg butikker**: brugeren eller Claude kan tilføje og fjerne butikker på turen; varerne fordeles igen.
3. **Flyt én vare** til en anden butik, fx når REMA er udsolgt, og den skal købes i Kvickly. Skal være hurtigt i
   butikken.
4. **Ét flueben**: en vare findes kun ét sted. Krydses den af på turen, er den krydset af på listen, og omvendt
   (også på den anden telefon i realtid).
5. **Nye varer undervejs**: kommer der varer på listen, efter turen er oprettet, viser turen en besked som
   "Nye på listen: Kaffe, Rugbrød, Sodavand +4" med én knap, der tager dem med på turen.
6. **Afslut tur**: brugeren trykker "Afslut tur". Opretter man en ny tur, mens en er i gang, afsluttes den gamle.
7. **Arkiv**: afsluttede ture ligger som historik. Fra en gammel tur kan man lægge udvalgte varer på listen igen.
8. **Claude via MCP** kan oprette turen (også med bestemte butikker), se den, vælge butikker, flytte varer, tage nye
   varer med og afslutte den.

## 3. Ikke-mål (nu)

- Statistik over priser og forbrug på tværs af ture.
- At redigere eller genåbne en afsluttet tur.
- At fjerne en enkelt vare fra en aktiv tur (den kan krydses af, eller fjernes fra listen).
- Flere aktive ture på samme tid.
- Ændringer i, hvordan butiksplanen vælger butikker (`StoreSelector`'s regler er uændrede).

## 4. Anti-mål (ville være en fejl, selv om det virker)

- To flueben for samme vare (et på turen og et på listen), der kan komme ud af trit.
- At turen ændrer sig af sig selv, mens man står i butikken (fx fordi Pia tilføjer kaffe derhjemme, eller fordi
  dagens tilbud er skiftet). Turen ændres kun, når nogen beder om det.
- At en flytning i butikken ("REMA var udsolgt") bliver til et fast valg for varen fremover.
- At historikken går i stykker, hvis en vare senere fjernes fra listen.

## 5. Adfærd

### 5.1 Oprette en tur

- Turen indeholder alle varer på listen, der **ikke er købt**, på det tidspunkt den oprettes.
- Butikkerne vælges med `StoreSelector` som i dag. Kaldes der med bestemte butikker (fx Claude: "kun REMA og Lidl"),
  bruges de, og hjemmebutikken kommer altid med.
- Hver vare får en butik og, hvis der er et, dagens bedste tilbud i den butik. Tilbuddets overskrift, pris, antal
  (`PieceCount`), linjepris og sidste gyldighedsdag gemmes på turen (øjebliksbillede), ligesom varens navn, mængde,
  variant og gruppe (tag).
- Varens `PlannedStoreId` på listen sættes til turens butik (bruges allerede af "købt" til købshistorikken).
- Findes der en aktiv tur, afsluttes den først (se 5.6).
- Turen gemmer det anslåede beløb sparet (`EstimatedSaving`).

### 5.2 Vise turen

- Samme opbygning som i dag (spec 12.6): én sektion pr. butik i køreordenen (andre stop først, største kurv først,
  hjemmebutikken sidst), undergrupper efter tag, pris diskret til sidst, subtotal pr. butik, anslået besparelse,
  "Del som tekst" (samme tekstformat som `TripText` i dag).
- **Mens turen er aktiv, vises varen, som den er på listen nu** (Claudes valg): navn, mængde, variant, gruppe og
  flueben læses fra `ShoppingListItem`. Lægger Pia fx en pakke kaffe mere på listen, og den lægges sammen med kaffen
  på turen, viser turen den nye mængde. Turens egen kopi bruges kun til historik og til varer, der er fjernet fra
  listen.
- En vare, der er fjernet fra listen, efter turen blev oprettet: var den købt, vises den stadig som købt (ud fra den
  frosne kopi); var den ikke købt, vises den ikke.
- Hjemmebutikken vises altid, også uden varer (som i dag).

- **Butiksgrupper** (Lidl og Coop 365 i Odder) tæller stadig som ét stop på turen (Claudes valg): er én butik i en
  gruppe med, er de andre i gruppen også mulige, præcis som `StoreSelector.Choose` gør i dag. En butik i en gruppe
  uden varer vises ikke.

### 5.3 Vælge butikker

- Brugeren eller Claude sender den ønskede mængde butikker. Hjemmebutikken kan ikke fjernes.
- Alle varer, der **ikke er købt** og **ikke er flyttet i hånden**, fordeles igen på de valgte butikker med
  `StoreSelector`'s fordeling (billigste tilbud blandt de valgte butikker; flaget butik, hvis den er med, ellers
  hjemmebutikken; ingen tilbud = hjemmebutikken). Tilbud og priser hentes for dagens dato.
- Varer flyttet i hånden bliver i deres butik, så længe butikken er med. Fjernes butikken, fordeles de som de andre.
- Købte varer bliver, hvor de er.

### 5.4 Flytte én vare

- Brugeren trykker på en vare på turen og vælger en anden butik blandt husstandens aktive butikker. Claude kan gøre
  det samme.
- Er butikken ikke på turen, kommer den med.
- Varen markeres som flyttet i hånden (se 5.3). Varens flag på listen og husstandens faste butiksvalg røres ikke.
- Prisen bliver dagens bedste tilbud på varen i den nye butik, ellers ingen pris.
- Bliver en butik tom (ikke hjemmebutikken), forsvinder den fra turen.

### 5.5 Nye varer undervejs

- "Nye varer" er varer på listen, der ikke er købt og ikke er på den aktive tur.
- Turen viser dem øverst: de første tre navne og "+N" for resten, med knappen "Tag med på turen".
- Tages de med, fordeles de kun på butikker, der allerede er på turen (ingen ny butik). Flaget butik, der ikke er på
  turen, eller intet tilbud blandt turens butikker: hjemmebutikken.
- Tages de ikke med, bliver de på listen til næste tur.

### 5.6 Afslutte en tur

- "Afslut tur" afslutter den aktive tur uden spørgsmål og viser bagefter fx "3 varer bliver på listen".
- Ved afslutning kopieres for hver vare på turen navn, mængde, variant, gruppe og om den var købt fra listen til
  turen (så historikken ikke ændrer sig, hvis varen senere krydses fra eller fjernes fra listen).
- **Fjernes en vare fra listen, mens den er på en tur** ("Slet vare" eller "Fjern købte varer fra listen"), fryses den
  på samme måde lige før sletningen (Claudes valg). Så går en tur i gang ikke i stykker, hvis nogen rydder de købte
  varer.
- Varer, der ikke blev købt, bliver stående på listen.
- En afsluttet tur kan ikke ændres.

### 5.7 Arkiv

- Liste over afsluttede ture, nyeste først, 20 ad gangen med "Hent flere": dato, butikker, antal varer, antal købt,
  kendt beløb.
- En afsluttet tur vises som en aktiv tur, men skrivebeskyttet: købt / ikke købt efter det, der blev gemt ved
  afslutning.
- Man kan markere varer og trykke "Læg på listen". De lægges på med navn, mængde, variant og gruppe gennem den
  eksisterende `ShoppingListWriter` (står varen allerede på listen, lægges mængderne sammen som i dag). Butiksflag
  følger ikke med (husstandens faste butiksvalg gælder som altid).

### 5.8 Realtid

- Alle ændringer af en tur sender den eksisterende `ListChanged`-besked til husstanden. Turen og listen genindlæses
  hos begge telefoner. Ingen ny SignalR-besked.

## 6. Data (forslag; detaljer til den, der bygger)

Nye tabeller, alle med `HouseholdId` via turen:

| Tabel | Felter | Bemærkning |
| --- | --- | --- |
| `ShoppingTrip` | Id, HouseholdId, CreatedAt, CreatedByUserId, FinishedAt (null = aktiv), EstimatedSaving | Højst én aktiv pr. husstand (unikt filtreret indeks på HouseholdId hvor FinishedAt er null) |
| `ShoppingTripStore` | TripId, StoreId | Butikkerne på turen (også hjemmebutikken og tomme butikker brugeren har valgt) |
| `ShoppingTripItem` | Id, TripId, ShoppingListItemId (null når varen er slettet), StoreId, Name, Quantity, Variant, Tag, OfferId, OfferHeading, UnitPrice, Count, LinePrice, LastValidDay, MovedByHand, WasBought (sættes ved afslutning og ved sletning fra listen) | FK til listevaren **uden** kaskaderegel (Claudes valg): koden fryser og nulstiller linket (`ExecuteUpdate`) før sletning i `RemoveItems`, så SQL Server ikke får flere kaskadeveje |

- Domænelogik i entiteterne (fx `ShoppingTrip.Finish(now)`, `ShoppingTripItem.MoveTo(store, offer)`), delt logik i
  `Common/` (fx en `TripViews`/`TripPlanner` der bygger DTO'er og fordeler varer), én handler pr. use case under
  `Features/Trips/`, som resten af projektet (SPEC afsnit 7).
- `StoreSelector` udvides med en offentlig fordeling på et givet sæt butikker (det, `Choose` allerede gør internt
  efter at have valgt butikker), så 5.3–5.5 genbruger samme regler. `Choose`'s resultat må ikke ændre sig.
- EF-migrering lægges i `Common/Data/Migrations` (`dotnet ef migrations add ShoppingTrips`). Den køres af appen ved
  start på NUC'en som i dag.

## 7. API (forslag)

| Metode og sti | Formål |
| --- | --- |
| `GET /api/trips/active` | Den aktive tur med varer pr. butik, nye varer og tekst; 204 hvis ingen |
| `POST /api/trips` | Opret tur (afslutter den aktive); body: valgfri `storeIds` |
| `PUT /api/trips/{id}/stores` | Vælg butikker; body: `storeIds` |
| `POST /api/trips/{id}/items/{tripItemId}/store` | Flyt én vare; body: `storeId` |
| `POST /api/trips/{id}/new-items` | Tag nye varer fra listen med |
| `POST /api/trips/{id}/finish` | Afslut; svarer med antal varer, der bliver på listen |
| `GET /api/trips?skip=&take=` | Arkiv, nyeste først |
| `GET /api/trips/{id}` | Én tur (aktiv eller afsluttet) |
| `POST /api/trips/{id}/to-list` | Læg udvalgte varer på listen igen; body: `tripItemIds` |

Ændringer på en afsluttet tur svarer 400 (eller 409) med en klar fejl. Andre husstandes ture svarer 404.

Det gamle `POST /api/shopping-list/trip` (`BuildTrip`) fjernes, og den ucommittede rettelse
`ShoppingListItem.OnTripOn` (kun manglende plus købt i dag) med test fjernes også; den er overflødig med gemte ture.

## 8. MCP-værktøjer (Claude)

- `build_shopping_list` beholder navnet (connectoren kender det), men **opretter nu en tur**: valgfri madplan og ekstra
  varer først som i dag, plus valgfri butikker (navne som i `add_to_shopping_list`). Beskrivelsen skal sige, at den
  afslutter en aktiv tur.
- Nye: `get_trip` (aktiv tur, eller en bestemt med id), `set_trip_stores`, `move_trip_item`, `add_new_items_to_trip`,
  `finish_trip`. Arkivlisten og "læg på listen igen" er kun i appen i første omgang.
- Hvert værktøj kalder samme handler som endpointet.

## 9. App (Angular)

- **Listen**: knappen "Find tilbud" bliver til "Opret handletur". Er der en aktiv tur, spørger den først
  ("Der er en tur i gang. Afslut den og opret en ny?") og går til turen bagefter.
- **Tur** (fanen "Tur"):
  - Ingen aktiv tur: kort tekst og knappen "Opret handletur", plus link til "Tidligere ture".
  - Aktiv tur: overskrift med dato, anslået besparelse, butiksfaner (Samlet + én pr. butik) som i dag, knappen
    "Butikker" der åbner et ark med en afkrydsning pr. stop (butiksgrupper som én række, fx "Lidl / Coop 365";
    hjemmebutikken låst), beskeden om nye varer,
    varerne med flueben, "Del som tekst" og "Afslut tur".
  - Tryk på en vares navn åbner et ark "Flyt til" med husstandens butikker; tryk på fluebenet krydser af.
- **Tidligere ture**: liste med "Hent flere"; en tur åbnes skrivebeskyttet med afkrydsning til "Læg på listen".
- Samme kompakte stil som den nye liste (små skrifter, 40 px rækker, farvede butiksbånd). Testes i 375 px.

## 10. Det, der overlades til den, der bygger

- Præcise navne på klasser, DTO'er og ruter, så længe de følger projektets mønstre.
- Om butiksfanernes rækkefølge og subtotaler beregnes ved visning (anbefalet) eller gemmes.
- Det præcise udseende af arkene og arkivet inden for stilen i afsnit 9.
- Tests: domæneregler og fordeling som rene unit tests (projektet har ingen databasetests). EF-forespørgsler, der
  bruger nye udtryk, tjekkes med `ToQueryString()` eller tilsvarende.

## 11. Claudes valg efter review

Reviewerens spørgsmål blev afgjort uden at spørge Marc, fordi de følger af "ét flueben" og anti-målene:
- Aktiv tur viser listens nuværende navn, mængde og flueben; kopien er til historik (5.2).
- Sletning fra listen fryser varen på turen først; ingen kaskaderegel i databasen (5.6, 6).
- Butiksgrupper er ét stop, også ved valg af butikker og nye varer (5.2, 9).

## 12. Afprøvning

- Unit tests for fordeling på valgte butikker, flytning, nye varer og afslutning (WasBought).
- `dotnet test` og `ng build` grønne.
- I browseren med testdata i 375 px, lys og mørk: opret, vælg butikker, flyt vare, nye varer, flueben begge veje,
  afslut, arkiv og "læg på listen".
- Ikke muligt herfra: rigtig database og telefon. Det skal Marc se efter deploy.
