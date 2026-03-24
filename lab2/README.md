# Raport de Laborator: Tranzacții, Concurență și Performanță 

## 1. Demonstrații ale Problemelor de Concurență

Acest capitol ilustrează fenomenele care apar atunci când mai multe tranzacții accesează simultan aceleași date, utilizând diverse niveluri de izolare.

### A. Demonstrație Dirty Read (Citire Murdară)
* **Scenariul:** Tranzacția A actualizează anul de publicare al unei cărți, dar nu face `COMMIT`. Tranzacția B încearcă să citească această valoare folosind nivelul de izolare `READ UNCOMMITTED`.
* **Comportament specific PostgreSQL:** Deși standardul SQL definește nivelul `READ UNCOMMITTED`, motorul PostgreSQL îl tratează intern ca pe `READ COMMITTED`. Astfel, baza de date previne în mod nativ citirile murdare. Tranzacția B nu va citi valoarea temporară a Tranzacției A, ci va citi ultima valoare confirmată, protejând integritatea datelor.
* **Prevenție teoretică:** Se previne folosind nivelul `READ COMMITTED` (comportamentul implicit în PostgreSQL).

### B. Demonstrație Non-Repeatable Read (Citire Nerepetabilă)
* **Scenariul:** Tranzacția A (folosind `READ COMMITTED`) citește anul unei cărți de două ori. Între cele două citiri, Tranzacția B modifică acel an și dă `COMMIT`. Rezultatul este că Tranzacția A obține două valori diferite pentru aceeași interogare.
* **Prevenție:** Acest fenomen este prevenit prin ridicarea nivelului de izolare la `REPEATABLE READ`, care asigură că datele citite inițial sunt "înghețate" pentru durata tranzacției.

### C. Demonstrație Phantom Read (Citire Fantomă)
* **Scenariul:** Tranzacția A numără cărțile unui anumit autor. Tranzacția B inserează o carte nouă pentru acel autor și dă `COMMIT`. Când Tranzacția A repetă numărătoarea, găsește un rând în plus ("fantoma").
* **Prevenție:** Conform standardului SQL, se previne folosind nivelul `SERIALIZABLE`. *(Notă tehnică: PostgreSQL previne rândurile fantomă încă de la nivelul `REPEATABLE READ` datorită arhitecturii sale MVCC - Multi-Version Concurrency Control).*

### D. Demonstrație Lost Update (Actualizare Pierdută)
* **Scenariul:** Tranzacțiile A și B citesc simultan aceeași valoare (ex: anul de publicare), fiecare calculează o valoare nouă în memorie și face `UPDATE`. Actualizarea care face prima `COMMIT` va fi suprascrisă de a doua, ducând la pierderea primului calcul.
* **Prevenție:** Se rezolvă prin utilizarea blocajelor explicite (ex: `SELECT ... FOR UPDATE`) sau prin trecerea la nivelul `REPEATABLE READ` / `SERIALIZABLE`, unde a doua tranzacție ar genera o eroare de serializare și ar trebui reîncercată.

<img width="973" height="805" alt="image" src="https://github.com/user-attachments/assets/10c5b630-20e1-4381-9e1e-22fcc52248a0" />

---

## 2. Demonstrație Deadlock (Blocaj Reciproc)

* **Problema (Deadlock Error):** Apare atunci când Tranzacția A blochează Resursa 1 și așteaptă Resursa 2, în timp ce Tranzacția B blochează Resursa 2 și o așteaptă pe 1. Niciuna nu poate continua. PostgreSQL detectează acest ciclu infinit (eroarea `40P01`) și anulează automat una dintre tranzacții pentru a debloca sistemul.
* **Rezolvarea (Deadlock Resolved):** Deadlock-urile se previn la nivel de aplicație prin **ordonarea corectă a resurselor**. În demonstrația E2, ambele tranzacții sunt programate să blocheze Resursa 1 (Id=12) prima, și abia apoi Resursa 2 (Id=13). Astfel, B așteaptă pur și simplu ca A să termine, fără a se crea un ciclu de blocaj.

<img width="905" height="622" alt="Captură de ecran 2026-03-24 104503" src="https://github.com/user-attachments/assets/7bc9614b-1777-43e1-bfeb-a337c27affbc" />

---

## 3. Analiza Nivelurilor de Izolare și Scenarii din Lumea Reală

Alegerea nivelului de izolare corect depinde de nevoile specifice ale aplicației:

1. **Read Uncommitted:**
   * *Când se folosește:* Aproape niciodată în sisteme critice. Poate fi util pentru estimări sau rapoarte statistice live pe tabele masive, unde exactitatea la virgulă nu contează (ex: numărul aproximativ de vizitatori activi pe un site).
2. **Read Committed (Standardul implicit pentru majoritatea bazelor de date):**
   * *Când se folosește:* Sistemele web obișnuite (bloguri, rețele sociale, forumuri). Oferă o performanță excelentă și evită citirea datelor "murdare", fiind suficient pentru o interacțiune standard.
3. **Repeatable Read:**
   * *Când se folosește:* Generarea de rapoarte financiare sau bilanțuri contabile la final de lună. Garantează că dacă rulezi un raport care durează 10 minute, datele din primele pagini nu se schimbă până ajungi la final, chiar dacă alți utilizatori fac modificări în fundal.
4. **Serializable:**
   * *Când se folosește:* Sisteme cu strictețe maximă (tranzacții bancare inter-bancare, rezervări de bilete de avion sau gestiunea stocurilor fizice limitate). Execută tranzacțiile ca și cum ar fi rulate una după alta (secvențial). Costul de performanță este foarte mare.

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
<img width="2816" height="1536" alt="Gemini_Generated_Image_x0ckdkx0ckdkx0ck" src="https://github.com/user-attachments/assets/ca0fa7a2-f314-4d7d-8e6c-a5f5d4226a05" />


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
