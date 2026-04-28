## Cerința 1: Problema N+1 și Optimizarea prin Eager Loading

Am început acest laborator analizând una dintre cele mai comune probleme de performanță atunci când lucrez cu Entity Framework Core: Problema N+1. Pentru a o evidenția, m-am folosit de relația One-to-Many dintre entitățile Author și Book. Obiectivul a fost simplu: să extrag din baza de date toți autorii și să le afișez cărțile.

Inițial, am implementat o abordare naivă, dar foarte des întâlnită. Am extras toți autorii printr-o interogare principală, iar apoi am iterat prin această listă folosind o buclă. În interiorul buclei, am cerut cărțile corespunzătoare fiecărui autor. 

Analizând logurile generate de PostgreSQL, am observat imediat impactul negativ al acestei abordări. Aplicația a trimis o interogare inițială (cea cu numărul 1) pentru a aduce autorii, urmată de alte zeci de interogări separate (cele N) – câte una pentru fiecare autor găsit. Astfel, pentru o listă relativ mică de date, am generat un trafic de rețea masiv, iar timpul de execuție a fost vizibil afectat.

Pentru a repara această problemă, am optimizat logica folosind tehnica de Eager Loading. Prin adăugarea unei directive specifice în interogarea principală, i-am transmis lui EF Core să aducă simultan atât autorii, cât și cărțile acestora.

În urma acestei modificări, baza de date a primit o singură interogare complexă (folosind un LEFT JOIN), în loc de sute de interogări mici și ineficiente. Rezultatul optimizării a fost clar: timpul de execuție a scăzut drastic, de la câteva zeci de milisecunde la doar câteva milisecunde. Această cerință mi-a demonstrat practic de ce trebuie să evit executarea interogărilor către baza de date în interiorul structurilor repetitive.

![Rezultat Benchmark UI](extra/poza1.png)