# Raport de Laborator: Eficiența Arhitecturală prin ORM și Connection Pooling

## 1. Tranziția către Entity Framework Core: O Schimbare de Paradigmă

În dezvoltarea modernă a aplicațiilor, interacțiunea directă cu baza de date prin SQL manual (ADO.NET pur) devine rapid un obstacol în calea productivității și a securității[cite: 4, 6]. Refactorizarea aplicației "BibliotecaApp" către Entity Framework Core (EF Core) nu a însemnat doar schimbarea unor linii de cod, ci adoptarea unei filosofii de proiectare în care baza de date devine o extensie naturală a modelului de obiecte C#[cite: 7, 14].



### Maparea Obiectuală și Relațiile dintre Entități
Inima noului sistem o reprezintă clasele entitate. Am definit entitățile `Author`, `Book` și `Category` folosind atribute de tip `[Key]` și proprietăți de navigare (Navigation Properties)[cite: 15, 17, 18, 21]. Această abordare permite bazei de date să înțeleagă ierarhia aplicației:
* **Relația 1:N (Un autor, mai multe cărți)**: Implementată prin `virtual ICollection<Book>` în clasa `Author` și o cheie străină explicită în `Book`[cite: 19].
* **Relația M:N (Cărți și Categorii)**: Gestionată automat prin EF Core folosind un tabel de joncțiune, eliminând necesitatea de a scrie manual JOIN-uri complexe pentru a afla ce genuri aparțin unei cărți[cite: 19].

### Puterea LINQ vs. SQL Manual
Eliminarea interogărilor SQL brute a redus riscul de SQL Injection și a mutat validarea erorilor de la momentul rulării (Runtime) la momentul compilării (Compile-time)[cite: 22, 23, 26, 92].

**Exemplu de Reducere a Codului Boilerplate[cite: 71, 89]:**
În varianta veche, aducerea cărților unui autor necesita deschiderea manuală a conexiunii, crearea unui `NpgsqlCommand`, adăugarea parametrilor și parsarea manuală a fiecărui rând dintr-un `DataReader`[cite: 58, 61, 63]. Acum, folosind LINQ, aceeași operațiune se rezumă la:
`return context.Books.Include(b => b.Categories).Where(b => b.AuthorId == id).ToList();`[cite: 65, 67, 69, 70].

---

## 2. Strategii de Încărcare a Datelor: Lazy vs. Eager Loading

Un aspect critic în utilizarea unui ORM este modul în care acesta accesează datele corelate[cite: 73]. Dacă nu suntem atenți, putem genera sute de interogări inutile către server.



* **Lazy Loading (Încărcarea Leneșă)**: Am configurat acest mecanism ca fiind implicit prin pachetul `Proxies`. Datele legate (precum categoriile unei cărți) sunt aduse din baza de date doar în momentul în care proprietatea este accesată în cod[cite: 74]. Este ideal pentru a păstra consumul de memorie scăzut, dar poate fi periculos în bucle (problema N+1).
* **Eager Loading (Încărcarea Activă)**: Pentru scenariile unde știm din start că avem nevoie de tot contextul (ex: afișarea unei liste complete de cărți cu tot cu genuri), am forțat EF Core să facă un singur JOIN masiv folosind metoda `.Include()`[cite: 75]. Această strategie reduce latența rețelei la o singură călătorie către server.

---

## 3. Optimizarea prin Connection Pooling: Tehnologia din Spatele Vitezei

Poate cea mai importantă îmbunătățire de performanță a fost implementarea **Connection Pooling**[cite: 2, 8]. În loc să negociem o conexiune nouă la fiecare click, aplicația folosește un "bazin" de conexiuni deja deschise și autentificate[cite: 39].



### Sarcina A: Analiza Overhead-ului (Măsurători Reale)
Am configurat pooling-ul în `appsettings.json` cu o limită de `Maximum Pool Size=10`[cite: 40, 43, 80]. Rezultatele testelor noastre au fost elocvente[cite: 44, 46, 84]:
1. **Fără Pooling**: Deschiderea a 100 de conexiuni a durat aproximativ **3.5 secunde**[cite: 47, 85]. Fiecare conexiune a necesitat un handshake TCP/IP și o validare de user/parolă.
2. **Cu Pooling**: Aceeași operațiune a durat **0 ms** (sub pragul de măsurare)[cite: 47, 86]. Aplicația a "reciclat" pur și simplu conexiunile existente în memorie.

### Sarcina B: Simularea Scurgerilor de Conexiuni (Leak Detection)
Am demonstrat pericolul gestionării incorecte a resurselor prin deschiderea a 15 conexiuni simultane fără a le închide[cite: 52, 53, 54]. Deoarece limita pool-ului era de 10, aplicația a înghețat la cererea numărul 11, aruncând în final o eroare de timeout[cite: 55]. Remediul a constat în utilizarea blocurilor `using`, care garantează returnarea conexiunii în pool chiar și în caz de eroare[cite: 56, 104].

---

## 4. Garanția Atomicității prin Tranzacții ORM

Pentru toate operațiile de scriere (Add, Update, Delete), am renunțat la "auto-commit"-ul implicit și am trecut la gestionarea explicită a tranzacțiilor[cite: 76, 77, 78]. Într-o bază de date cu relații complexe M:N, o singură eroare la inserarea categoriilor ar putea lăsa cartea "orfana" în sistem. Utilizând `BeginTransaction()`, ne asigurăm că fie toată cartea cu toate legăturile ei este salvată, fie nimic nu este scris pe disc, păstrând baza de date într-o stare consistentă.

---

## 5. Concluzii: Avantaje și Compromisuri

Implementarea ORM și a Connection Pooling-ului a transformat aplicația dintr-un script SQL rudimentar într-un sistem robust de nivel Enterprise[cite: 1, 9, 10].

| Avantaj | Compromis |
| :--- | :--- |
| **Productivitate**: Codul este mult mai scurt și ușor de citit[cite: 92]. | **Overhead**: EF Core adaugă o mică întârziere la prima pornire[cite: 92]. |
| **Securitate**: Protecție nativă împotriva SQL Injection[cite: 92]. | **Control**: SQL-ul generat automat poate fi uneori mai puțin optim[cite: 92]. |
| **Scalabilitate**: Connection Pooling permite mii de cereri simultane[cite: 9, 10]. | **Configurare**: Necesită o atenție sporită la detaliile din appsettings.json[cite: 93]. |

**Bonus**: Am activat logging-ul SQL pentru a vizualiza în timp real cum LINQ se transformă în interogări PostgreSQL, permițându-ne să optimizăm performanța acolo unde este necesar[cite: 112].