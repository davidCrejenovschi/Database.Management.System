# Raport de Laborator: Tranzacții, Concurență și Performanță 

## 1. Demonstrații ale Problemelor de Concurență

Într-un mediu real de producție, o bază de date nu este accesată secvențial de un singur utilizator, ci de zeci sau sute de utilizatori simultan. "Concurența" se referă tocmai la capacitatea bazei de date de a gestiona aceste cereri paralele. Atunci când mai multe procese (sau tranzacții) încearcă să citească și să modifice aceleași informații în exact același timp, sistemul se poate "încurca", ducând la anomalii grave ale datelor. 

Pentru a demonstra aceste probleme în cadrul laboratorului, am simulat utilizatori concurenți folosind fire de execuție paralele (`Task.Run` în C#) și pauze controlate (`Task.Delay`) asupra tabelului `Books`. Mai jos sunt detaliate cele patru anomalii majore testate.

### A. Demonstrație Dirty Read (Citirea Murdară)

Un coleg redactează un document financiar important și modifică o cifră de afaceri, dar încă nu a salvat documentul (nu a apăsat *Save*). Tu tragi cu ochiul peste umărul lui, vezi noua cifră și o trimiți mai departe conducerii. Câteva secunde mai târziu, colegul tău își dă seama că a greșit, șterge modificarea și închide documentul fără să salveze. În acest moment, tu ai trimis mai departe o informație "murdară" – o informație care, din punct de vedere oficial, nu a existat niciodată. Într-o bază de date, acest lucru se întâmplă când Tranzacția A actualizează o înregistrare dar nu a făcut commit, iar Tranzacția B citește acele date necomise.

**Ce s-a întâmplat în aplicația noastră:**
În cod, Tranzacția A a accesat cartea cu ID-ul 12 și i-a schimbat anul de publicare într-o valoare absurdă: 9999. Apoi, a intrat intenționat într-o pauză de 3 secunde, amânând confirmarea (Commit-ul). În acest timp, Tranzacția B a venit și a cerut să citească anul aceleiași cărți, specificând nivelul de izolare `READ UNCOMMITTED` (care, teoretic, permite citirile murdare).

* **Particularitatea PostgreSQL:** Testul a demonstrat o caracteristică de siguranță nativă a motorului PostgreSQL. Chiar dacă i-am cerut explicit să citească date "murdare", Postgres previne acest lucru prin design. Intern, a tratat cererea ca pe un `READ COMMITTED`, ignorând valoarea temporară de 9999 și returnând Tranzacției B ultima valoare validă cunoscută. 

**Cum prevenim asta:**
În sistemele de baze de date care permit acest fenomen, soluția este ridicarea nivelului de izolare la minim `READ COMMITTED`.

### B. Demonstrație Non-Repeatable Read (Citire Nerepetabilă)

Momentul în care îți verifici soldul contului bancar pe telefon și vezi 500 de lei. Lași telefonul pe birou un minut, te uiți din nou, și acum scrie 450 de lei, pentru că tocmai s-a procesat automat plata pentru un abonament în fundal. Informația s-a schimbat între două "priviri" succesive. [cite_start]În baza de date, problema apare când Tranzacția A citește o înregistrare de două ori, dar Tranzacția B actualizează înregistrarea exact între cele două citiri[cite: 34]. Astfel, prima tranzacție nu se mai poate baza pe datele pe care credea că le are.

**Ce s-a întâmplat în aplicația noastră:**
Am rulat Tranzacția A (folosind `READ COMMITTED`) care a citit anul de publicare pentru cartea cu ID-ul 12. Imediat după, Tranzacția B a intervenit agresiv: a schimbat anul cărții în 2025 și a salvat imediat modificarea. Când Tranzacția A și-a reluat activitatea și a cerut din nou anul de publicare pentru aceeași carte, a obținut valoarea nouă (2025), contrazicând prima citire. Acest lucru este inacceptabil în scenarii precum generarea de rapoarte lungi, unde datele trebuie să rămână constante de la prima până la ultima pagină.

**Cum prevenim asta:**
Fenomenul se previne setând nivelul de izolare la `REPEATABLE READ`. Acest nivel creează o "fotografie" virtuală a datelor în momentul în care tranzacția începe, garantând că vei primi exact aceeași valoare indiferent de câte ori citești acel rând, chiar dacă alții îl modifică în fundal.

### C. Demonstrație Phantom Read (Citire Fantomă)

Dacă la problema anterioară se modificau date *existente*, aici ne lovim de date noi, apărute de nicăieri. Imaginează-ți că intri într-o bibliotecă și numeri manual cărțile de pe un raft: sunt fix 10. Închizi ochii pentru câteva secunde, timp în care bibliotecarul pune o carte nouă pe raft. Când deschizi ochii și numeri din nou, găsești 11 cărți. Acea carte apărută brusc este o "fantomă". [cite_start]Tehnic, apare când Tranzacția A execută aceeaşi interogare de două ori, iar Tranzacția B inserează un rând care corespunde interogării între execuții[cite: 50].

**Ce s-a întâmplat în aplicația noastră:**
Tranzacția A a executat o comandă de numărare (`SELECT COUNT`) pentru a afla câte cărți a scris autorul cu ID-ul 12. În timp ce Tranzacția A aștepta, Tranzacția B a inserat o carte complet nouă în tabel (`Phantom Book`), atribuindu-i același autor (ID=12). La a doua numărătoare făcută de Tranzacția A, rezultatul a crescut subit cu 1. 

**Cum prevenim asta:**
Deoarece nu poți pune un lacăt de protecție pe un rând care nu există încă, standardul SQL impune cel mai strict nivel de izolare pentru a opri fantomele: `SERIALIZABLE`. Acesta blochează o întreagă "zonă" logică, nepermițând nimănui să insereze date care ar putea altera rezultatul interogării tale.

### D. Demonstrație Lost Update (Actualizare Pierdută)

Acesta este cel mai periculos fenomen pentru integritatea datelor, similar cu editarea simultană a unei pagini de Wikipedia. Tu și un alt utilizator deschideți pagina în același timp. Tu scrii un paragraf nou și dai "Salvează". Imediat după, celălalt utilizator (care avea pe ecran varianta veche a paginii) adaugă propriul paragraf și dă "Salvează". Rezultatul? Pagina va conține doar textul lui. Munca ta a fost ștearsă complet. [cite_start]În limbajul bazelor de date, două tranzacții citesc aceeaşi valoare, o modifică și o scriu înapoi, iar o actualizare se pierde[cite: 65].

**Ce s-a întâmplat în aplicația noastră:**
Ambele tranzacții au avut misiunea de a citi anul de publicare al cărții (ID 12) și de a adăuga o valoare la el. 
1. Tranzacția A a citit anul inițial și a calculat în memorie un nou an adăugând +10. 
2. În același timp, Tranzacția B a citit același an inițial și a calculat o adunare cu +5.
3. Tranzacția A a salvat rezultatul ei matematic în baza de date.
4. Imediat după, Tranzacția B a salvat orbește rezultatul ei.
Finalul? Modificarea legitimă a Tranzacției A s-a evaporat, baza de date păstrând doar calculul Tranzacției B. Dacă era vorba de retrageri de bani dintr-un cont comun, tranzacția pierdută ar fi generat o gaură financiară.

**Cum prevenim asta:**
Problema se rezolvă prin blocaje pesimiste ("Pessimistic Locking"). Se citește rândul cu clauza `SELECT ... FOR UPDATE`, care blochează fizic acel rând. Nicio altă tranzacție nu va putea nici măcar să *citească* acel an până când tu nu termini calculele și salvezi noul rezultat.

<img width="973" height="805" alt="image" src="images/Captură de ecran 2026-03-24 104645.png" />

---

## 2. Demonstrație Deadlock (Blocaj Reciproc)

Doi angajați la birou care trebuie să îndosarieze niște acte: Angajatul A are capsatorul și are nevoie urgentă de perforator pentru a-și termina treaba. Angajatul B a luat perforatorul și are nevoie de capsator. Niciunul nu este dispus să cedeze instrumentul pe care îl ține în mână până când nu îl primește pe celălalt. Rezultatul? Amândoi stau pe loc și se privesc la nesfârșit, iar munca se oprește complet. 

În lumea bazelor de date, acest fenomen se numește **Deadlock** (blocaj reciproc). Apare atunci când două (sau mai multe) tranzacții dețin fiecare un lacăt (lock) pe o resursă și așteaptă eliberarea altei resurse, deținută de cealaltă tranzacție, creând un cerc vicios din care niciuna nu poate ieși singură.

### E1. Provocarea Blocajului (Deadlock Error)

**Ce s-a întâmplat în aplicația noastră:**
Pentru a demonstra problema, am scris două tranzacții care concurează pentru aceleași două cărți (ID-urile 12 și 13), dar în ordine inversă:
1. Tranzacția A a blocat prima carte (ID=12) pentru a-i actualiza anul.
2. În exact același timp, Tranzacția B a blocat a doua carte (ID=13).
3. După o scurtă pauză, Tranzacția A a încercat să modifice și cartea cu ID=13. Baza de date a pus-o "în așteptare", deoarece Tranzacția B o deținea deja.
4. Imediat după, Tranzacția B a încercat să modifice cartea cu ID=12. A fost pusă și ea în așteptare, deoarece Tranzacția A o ținea blocată.

În acest moment, s-a format ciclul de Deadlock. 

* **Reacția sistemului PostgreSQL:** O bază de date modernă nu stă blocată la nesfârșit. Motorul PostgreSQL scanează periodic tranzacțiile, iar când detectează acest "nod gordian", intervine ca un arbitru: anulează (ucide) forțat una dintre tranzacții, aruncând eroarea specifică `40P01` (deadlock detected). Acest sacrificiu permite celeilalte tranzacții să preia resursa lipsă și să se finalizeze. În aplicația noastră, am prins această excepție într-un bloc `catch` și am afișat mesajul de eroare generat de server.

### E2. Rezolvarea Blocajului (Deadlock Resolved)

Dacă ne întoarcem la exemplul cu angajații, soluția este impunerea unei reguli de ordine în companie: "Toată lumea ia *întotdeauna* prima dată capsatorul, și abia apoi perforatorul". Dacă Angajatul A a luat capsatorul, Angajatul B pur și simplu se așază la rând și așteaptă. Angajatul A ia și perforatorul, își termină treaba, le pune pe amândouă pe masă, iar apoi B le poate folosi. Nu se mai creează niciun blocaj.

**Cum am implementat asta în cod:**
Deadlock-urile nu se rezolvă din setările bazei de date (prin niveluri de izolare), ci prin **disciplina arhitecturii software** (la nivel de cod aplicație). În testul E2, am rescris ordinea în care tranzacțiile cer resursele.
* Ambele tranzacții au fost instruite să blocheze *mai întâi* cartea cu ID=12, iar mai apoi cartea cu ID=13.
* Astfel, când Tranzacția A a pus lacăt pe cartea 12, Tranzacția B (care voia tot cartea 12 prima) a fost pusă imediat în așteptare civilizată, fără a apuca să blocheze altceva. 
* Tranzacția A și-a continuat execuția nestingherită: a blocat cartea 13, a făcut actualizările, a dat `COMMIT` și a eliberat ambele rânduri.
* Imediat după acel `COMMIT`, Tranzacția B a primit undă verde, a blocat pe rând cărțile 12 și 13 și și-a finalizat execuția cu succes.

<img width="905" height="622" alt="Captură de ecran 2026-03-24 104503" src="images/Captură de ecran 2026-03-24 104503.png" />

---

## 3. Analiza Nivelurilor de Izolare și Scenarii din Lumea Reală

Alegerea nivelului de izolare corect nu este doar o decizie tehnică, ci în primul rând una de arhitectură a aplicației și de reguli de business. Fiecare treaptă superioară de izolare aduce mai multă siguranță matematică pentru datele mele, dar consumă mai multe resurse, aplică mai multe lacăte (locks) și încetinește inevitabil timpul de răspuns. 

În urma testelor efectuate și a analizei fenomenelor de concurență, am sistematizat aplicabilitatea fiecărui nivel în scenarii reale de producție:

### A. READ UNCOMMITTED (Citire Neconfirmată)
Este nivelul cu cea mai slabă protecție, dar cu cea mai mare viteză de execuție, deoarece ignoră aproape complet mecanismele de blocare. Baza de date permite citirea unor informații care se află în plin proces de modificare și care ar putea fi anulate (Rollback) în secunda următoare. *(Notă: Așa cum am demonstrat în primul experiment, PostgreSQL ignoră această setare din motive de siguranță internă și o ridică automat la Read Committed).*
* **Scenariu din lumea reală:** Acolo unde viteza extremă este necesară, iar precizia la virgulă este complet irelevantă. Un exemplu clasic este sistemul de estimare a traficului sau contorul de "Like-uri" și vizualizări pentru un videoclip viral (ex: YouTube). Dacă platforma raportează 1.000.500 de vizualizări în loc de 1.000.505 din cauza unor tranzacții nefinalizate în fundal, impactul asupra utilizatorului final este absolut zero, dar câștigul de performanță pentru server este uriaș, nefiind nevoit să blocheze tabela la fiecare click.

### B. READ COMMITTED (Citire Confirmată)
Este nivelul implicit (default) în PostgreSQL și în marea majoritate a sistemelor de baze de date. Garantează că tranzacția mea va citi doar date care au fost deja salvate definitiv. Oferă probabil cel mai bun compromis între stabilitate și viteză, permițând aplicației să deservească mii de utilizatori simultan.
* **Scenariu din lumea reală:** Aplicațiile web standard, platformele de social media, blogurile și navigarea în magazinele online (e-commerce). Când un client navighează printr-un catalog cu mii de produse, este perfect acceptabil și firesc ca prețul sau stocul unui produs să fie actualizat de un administrator de sistem între două click-uri succesive ale clientului (situație care reprezintă, practic, un Non-Repeatable Read asumat).

### C. REPEATABLE READ (Citire Repetabilă)
Acest nivel rezolvă inconsecvențele din `Read Committed`. Când inițiez o tranzacție cu această setare, baza de date îmi creează o "fotografie" (snapshot) a datelor din acel moment precis. Orice aș citi pe durata tranzacției mele, informația va rămâne neschimbată, ignorând total orice `UPDATE` sau `DELETE` făcut de alți utilizatori în fundal.
* **Scenariu din lumea reală:** Generarea de rapoarte de analiză complexe, bilanțuri financiare lunare sau închiderea de casă. Dacă execut un algoritm de calcul care durează 15 minute pentru a însuma toate tranzacțiile dintr-o zi, este absolut critic ca rândurile citite în primul minut să nu fie modificate de alții în minutul 14. Raportul trebuie să reflecte o stare unică și coerentă a companiei de la începutul până la finalul rulării scriptului.

### D. SERIALIZABLE (Serializabil)
Reprezintă nivelul absolut de protecție. Sistemul garantează că executarea concurentă a mai multor tranzacții va avea exact același efect ca și cum ar fi fost rulate strict una după alta (secvențial). Previne toate anomaliile, inclusiv apariția "fantomelor". Costul de performanță este uriaș, iar baza de date va refuza (va da eroare) tranzacțiile care intră în conflict, obligând codul aplicației mele să le prindă în blocuri `try-catch` și să le reîncerce.
* **Scenariu din lumea reală:** Operațiunile de o importanță critică unde consistența bate viteza. Exemplul perfect este transferul interbancar de fonduri sau sistemele de rezervări cu stoc fizic strict limitat (achiziția ultimului loc pe un zbor sau cumpărarea de bilete la un festival foarte căutat). În aceste cazuri, este inacceptabil ca două procese să citească simultan că a mai rămas un singur bilet și ambele să încerce să îl vândă către doi clienți diferiți. Sistemul forțează clienții să se așeze la o "coadă" strictă.

---

## 4. Analiza Performanței Inserărilor în Lot (Batch)

Pentru a demonstra impactul metodelor de procesare asupra performanței, am inserat 5000 de înregistrări folosind trei abordări diferite, fiecare rulată de 3 ori pentru a extrage o medie.

### Rezultatele Benchmark-ului

1. Testing Auto-Commit (1 tx per insert)...
   Runs: 1725ms, 1252ms, 1224ms | AVERAGE: 1400ms

2. Testing Batch Commits (Commit every 100 inserts)...
   Runs: 759ms, 761ms, 680ms | AVERAGE: 733ms

3. Testing Single Transaction + ExecuteBatch(50)...
   Runs: 149ms, 156ms, 148ms | AVERAGE: 151ms
<br>
<img width="2816" height="1536" alt="Gemini_Generated_Image_x0ckdkx0ckdkx0ck" src="images/Gemini_Generated_Image_x0ckdkx0ckdkx0ck.png" />


### Concluzii de Performanță
Din datele obținute, reiese clar că:
1. **Auto-commit (O tranzacție per inserare)** este extrem de ineficient. Pentru fiecare dintre cele 5000 de rânduri, sistemul deschide o tranzacție, scrie pe disc, confirmă operațiunea (I/O) și închide tranzacția. Acest overhead de rețea și disc este masiv.
2. **Commit în loturi (la fiecare 100 de rânduri)** îmbunătățește masiv performanța, deoarece reduce numărul de tranzacții I/O pe disc de la 5000 la doar 50.
3. **Tranzacția Unică cu Statement Batching (NpgsqlBatch / executeBatch)** este de departe cea mai rapidă metodă. Aceasta nu doar că folosește o singură tranzacție, dar împachetează instrucțiunile SQL la nivel de driver și le trimite către server într-un singur pachet de rețea, eliminând latența de comunicare (round-trip time) dintre aplicația C# și serverul PostgreSQL.

---

## 5. Compromisuri: Performanță vs. Consistența Datelor

Dezvoltarea aplicațiilor cu baze de date reprezintă un echilibru constant între performanță (viteză) și consistență (corectitudine matematică):

* **Dacă prioritizăm Consistența Absolută (ex: Serializable):** Aplicația este sigură 100% împotriva oricărui fenomen de concurență. Totuși, **performanța scade dramatic**. Apar lacăte (`locks`) severe la nivel de tabele, utilizatorii vor experimenta timp de așteptare mare (latență), iar probabilitatea de Deadlock crește, forțând aplicația să reîncerce tranzacții eșuate.
* **Dacă prioritizăm Performanța (ex: Read Committed / Auto-commit asincron):** Sistemul poate suporta mii de cereri pe secundă. Riscul asumat este apariția anomaliilor (Non-Repeatable Reads sau Phantom Reads). 

**Soluția în practică:** Nu se folosește un singur nivel peste tot. Regulile de business critice (ex: procesarea unei plăți) se izolează sever, în timp ce operațiunile generale (ex: afișarea catalogului de cărți) folosesc niveluri mai relaxate pentru a menține aplicația rapidă și responsivă.
