## Cerința 1: Problema N+1 și Optimizarea prin Eager Loading

Am început acest laborator analizând una dintre cele mai comune probleme de performanță atunci când lucrez cu Entity Framework Core: Problema N+1. Pentru a o evidenția, m-am folosit de relația One-to-Many dintre entitățile Author și Book. Obiectivul a fost simplu: să extrag din baza de date toți autorii și să le afișez cărțile.

Inițial, am implementat o abordare naivă, dar foarte des întâlnită. Am extras toți autorii printr-o interogare principală, iar apoi am iterat prin această listă folosind o buclă. În interiorul buclei, am cerut cărțile corespunzătoare fiecărui autor. 

Analizând logurile generate de PostgreSQL, am observat imediat impactul negativ al acestei abordări. Aplicația a trimis o interogare inițială (cea cu numărul 1) pentru a aduce autorii, urmată de alte zeci de interogări separate (cele N) – câte una pentru fiecare autor găsit. Astfel, pentru o listă relativ mică de date, am generat un trafic de rețea masiv, iar timpul de execuție a fost vizibil afectat.

Pentru a repara această problemă, am optimizat logica folosind tehnica de Eager Loading. Prin adăugarea unei directive specifice în interogarea principală, i-am transmis lui EF Core să aducă simultan atât autorii, cât și cărțile acestora.

În urma acestei modificări, baza de date a primit o singură interogare complexă (folosind un LEFT JOIN), în loc de sute de interogări mici și ineficiente. Rezultatul optimizării a fost clar: timpul de execuție a scăzut drastic, de la câteva zeci de milisecunde la doar câteva milisecunde. Această cerință mi-a demonstrat practic de ce trebuie să evit executarea interogărilor către baza de date în interiorul structurilor repetitive.

![Rezultat Benchmark UI](extra/poza1.png)


## Cerința 2: Analiza Performanței Indexării

A doua etapă a laboratorului s-a concentrat pe analiza modului în care index-urile influențează viteza de răspuns a bazei de date. Pentru a avea rezultate relevante, am construit un mecanism care a inserat automat 10.000 de înregistrări (cărți) în baza de date pentru un anumit autor de test.

Obiectivul a fost să rulez un set de patru interogări diferite, mai întâi pe un tabel "curat" (fără index-uri), și apoi pe același tabel, dar cu index-uri strategice aplicate, folosind comanda `EXPLAIN ANALYZE` din PostgreSQL pentru a vedea exact planul de execuție din spate.

### Pasul 1: Execuția fără Index-uri (Baseline)

Am rulat următoarele tipuri de căutări:
1. **Căutare exactă pe text** (după Titlul cărții).
2. **Căutare exactă pe Foreign Key** (după AuthorId).
3. **Căutare pe interval** (după Anul Publicării, folosind `BETWEEN`).
4. **Căutare multi-coloană** (după AuthorId și Anul Publicării).

La analiza log-urilor brute returnate de baza de date, am observat că, în lipsa index-urilor, PostgreSQL folosea strategia de **`Seq Scan`** (Sequential Scan). Asta înseamnă că motorul bazei de date era forțat să citească orbește fiecare rând din cele 10.000 pentru a filtra rezultatele, operațiune foarte costisitoare ca timp.

![Plan de execuție Seq Scan](extra/poza_explain_fara_index.png)
*(Exemplu de plan de execuție Sequential Scan extras din consolă)*

### Pasul 2: Aplicarea Index-urilor Strategice

Am aplicat index-uri pe coloanele utilizate frecvent în instrucțiunile `WHERE` (`Title`, `AuthorId`, `PublicationYear`) și un index compus pe (`AuthorId`, `PublicationYear`). 

La re-rularea acelorași interogări, planul de execuție generat de `EXPLAIN ANALYZE` s-a schimbat dramatic. Baza de date a început să folosească **`Bitmap Index Scan`**, sărind direct la paginile de memorie unde se aflau datele căutate, fără să mai scaneze întregul tabel.

![Plan de execuție Index Scan](extra/poza_explain_cu_index.png)
*(Exemplu de plan de execuție îmbunătățit cu Bitmap Index Scan)*

### Rezultate și Concluzii

Pentru a avea o perspectivă clară, aplicația mea calculează media a 100 de rulări pentru fiecare interogare și generează un raport comparativ. 

| Tip Interogare | Fără Index (ms) | Cu Index (ms) | Îmbunătățire |
| :--- | :--- | :--- | :--- |
| Căutare exactă (Titlu) | 2.5724 ms | 0.3750 ms | **~ 85% mai rapid** |
| Căutare Foreign Key (Autor)* | 7.8644 ms | 7.6778 ms | ~ 2% mai rapid |
| Interval (Anul Publicării) | 2.4198 ms | 1.3474 ms | **~ 44% mai rapid** |
| Multi-coloană | 3.3031 ms | 1.6283 ms | **~ 50% mai rapid** |

**Lecție învățată:** Am observat o anomalie interesantă la căutarea după Autor, unde index-ul nu a adus o îmbunătățire semnificativă. Analizând datele, mi-am dat seama că toate cele 10.000 de înregistrări de test aparțineau aceluiași autor. Când interogarea cere bazei de date să returneze aproape 100% din tabel, index-ul devine inutilizabil (selectivitate scăzută), iar baza de date alege de multe ori să facă tot o scanare completă. Asta demonstrează că index-urile nu sunt o soluție magică, ci trebuie aplicate strategic, acolo unde datele sunt cu adevărat variate.

![Tabel Rezultate Benchmark](extra/poza_tabel_benchmark_indexi.png)
*(Tabelul generat automat de aplicație cu compararea timpilor de execuție)*