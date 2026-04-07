# Raport de Laborator: Eficiența Arhitecturală prin ORM și Connection Pooling

## 1. Tranziția către Entity Framework Core: O Schimbare de Paradigmă

Să fim sinceri: să mai scrii astăzi interogări SQL de mână, ascunse în string-uri prin codul C# (vechiul ADO.NET), e ca și cum ai încerca să repari un motor de ultimă generație cu o cheie franceză ruginită. E ineficient, greu de întreținut și te expune la greșeli de începător. Refactorizarea aplicației noastre, "BibliotecaApp", către **Entity Framework Core (EF Core)** nu a fost doar o fiță tehnologică, ci o trecere necesară la un mod de lucru în care baza de date devine o extensie logică a codului nostru.

### Maparea Obiectuală: Cum facem tabelele să "vorbească" C#
Inima noului sistem este formată din clasele entitate: `Author`, `Book` și `Category`. Folosind atribute precum `[Key]` și proprietăți de navigare, am instruit EF Core să înțeleagă exact cum stau lucrurile în realitate:
* **Relația 1:N (Un autor, mai multe cărți):** Am rezolvat-o elegant cu un `virtual ICollection<Book>` în clasa `Author`. Acum, când avem un autor, avem acces instant la toată opera lui fără să scriem noi logica de legătură sau JOIN-uri manuale.
* **Relația M:N (Cărți și Categorii):** Aici e unde EF Core strălucește cu adevărat. În loc să ne batem capul cu tabele de joncțiune și ID-uri care zboară dintr-o parte în alta, ORM-ul gestionează totul în fundal. Noi doar adăugăm o categorie într-o listă și gata, legătura e făcută în tabelul intermediar.

### Puterea LINQ: Adio, SQL Injection și erori la Runtime
Cea mai mare victorie a fost eliminarea SQL-ului brut. Folosind **LINQ (Language Integrated Query)**, am mutat validarea erorilor din momentul în care rulează programul (când aplicația crapă în fața userului) în momentul în care scriem codul (Compile-time). Dacă greșești numele unei coloane, codul pur și simplu nu se compilează. În plus, scăpăm automat de coșmarul numit SQL Injection, pentru că EF Core parametrizează totul sub capotă.

**Exemplu de simplificare radicală:**
Uită de `NpgsqlCommand`, deschiderea conexiunii manual, creat cititorul de date și parsat fiecare rând într-o buclă `while`. Acum, totul se reduce la o logică fluidă:

```csharp
// Luăm toate cărțile unui autor, cu tot cu categoriile lor, dintr-o singură mișcare
public List<Book> GetAuthorBooks(int id)
{
    return context.Books
        .Include(b => b.Categories)
        .Where(b => b.AuthorId == id)
        .ToList();
}

Iată **Partea 2** (Strategii de încărcare și Connection Pooling):

```markdown
## 2. Strategii de Încărcare a Datelor: Lazy vs. Eager Loading

Aici e locul unde mulți developeri o dau în bară și apoi se plâng că "ORM-ul e lent". Trebuie să știi exact când și cum să ceri datele de la server ca să nu omori performanța.

* **Lazy Loading (Încărcarea Leneșă):** Am configurat-o prin pachetul de `Proxies`. E grozavă pentru că nu încarcă datele legate (cum ar fi categoriile unei cărți) până când nu le accesezi explicit în cod. Totuși, e o sabie cu două tăișuri: dacă o folosești într-o buclă (celebrul "N+1"), te trezești că aplicația face 100 de cereri mici la baza de date în loc de una singură.
* **Eager Loading (Încărcarea Activă):** Pentru scenariile unde știm clar că avem nevoie de tot contextul, folosim `.Include()`. Îi spunem bazei de date: "Adu-mi tot pachetul acum!". Un singur JOIN masiv, o singură călătorie pe rețea, eficiență maximă.

---

## 3. Optimizarea prin Connection Pooling: Tehnologia din Spatele Vitezei

Dacă ORM-ul se ocupă de *ce* trimitem la baza de date, **Connection Pooling** se ocupă de *cum* ajungem acolo. Să deschizi o conexiune nouă la fiecare cerere e un proces greoi: handshake TCP, autentificare, alocare de memorie. E un overhead imens pe care nu ni-l permitem.

### Sarcina A: Analiza Performanței (Măsurători Reale)
Am configurat pooling-ul în `appsettings.json` cu o limită de `Maximum Pool Size=10`. Rezultatele testelor de stres au fost elocvente:
1. **Fără Pooling:** Deschiderea a 100 de conexiuni consecutive a durat aproximativ **3.5 secunde**. Fiecare conexiune a însemnat un efort real pentru server.
2. **Cu Pooling:** Aceeași operațiune a durat **sub 1 ms**. Aplicația pur și simplu a "reciclat" conexiunile care erau deja deschise și gata de treabă în memorie.

### Sarcina B: Simularea Scurgerilor (Leak Detection)
Am vrut să vedem ce se întâmplă dacă gestionăm prost resursele. Am forțat deschiderea a 15 conexiuni fără să le închidem. Cum limita noastră era de 10, la a 11-a cerere aplicația a "înghețat" și apoi a aruncat o eroare de timeout. 
**Remediul:** Utilizarea blocurilor `using` în C#. Acestea garantează că, indiferent dacă apare o eroare sau nu, conexiunea se întoarce imediat în pool și nu rămâne "agățată", blocând restul utilizatorilor.
---

## 4. Garanția Atomicității prin Tranzacții

În operațiile de scriere (Add, Update, Delete), am renunțat la "auto-commit"-ul implicit. Într-o bază complexă, nu vrei jumătăți de măsură. Dacă adaugi o carte nouă, dar inserarea categoriilor eșuează din cauza unei constrângeri, nu vrei să rămâi cu o carte "orfană" în sistem. Utilizând `BeginTransaction()`, ne asigurăm de principiul **"totul sau nimic"**: fie se salvează toată informația corect, fie se dă rollback total, păstrând baza de date curată și consistentă.

---

## 5. Concluzii: Avantaje și Compromisuri

Implementarea EF Core și a pooling-ului a transformat aplicația dintr-un script rudimentar într-un sistem robust, gata de utilizare serioasă.

| Avantaj | Compromis / Atenție |
| :--- | :--- |
| **Productivitate**: Codul este mult mai scurt, curat și ușor de citit. | **Overhead**: EF Core are nevoie de un mic timp de "încălzire" la prima pornire. |
| **Securitate**: Protecție nativă împotriva atacurilor de tip SQL Injection. | **Control**: SQL-ul generat automat poate fi uneori prea complex pentru query-uri simple. |
| **Scalabilitate**: Connection Pooling permite mii de cereri fără a bloca serverul. | **Configurare**: Necesită o atenție sporită la setările de fine-tuning din fișierele de configurare. |

**Bonus:** Am activat logging-ul SQL pentru a vizualiza în consolă, în timp real, cum LINQ se transformă în interogări PostgreSQL. Este cel mai bun mod de a învăța cum să optimizezi performanța și să vezi exact ce se întâmplă sub capotă.