# 📄 Raport Laborator 3: Introducere în ORM și Connection Pooling

## 🛠️ Cerințe Preliminare

Pentru a rula acest proiect, veți avea nevoie de următoarele instalate pe mașina locală:
* **Visual Studio 2022/2026** (compatibil cu .NET)
* **.NET SDK** (v8.0 sau mai nou)
* **PostgreSQL** (Server baze de date)
* **Pachete NuGet**: `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Proxies`, `Microsoft.Extensions.Configuration.Json`, `Microsoft.EntityFrameworkCore.Design`.

---

## 🎯 1. Obiectiv
Refactorizarea aplicației din Laboratorul 1 pentru a utiliza framework-ul **Entity Framework Core (C#)**. Implementarea și configurarea **connection pooling-ului** pentru a îmbunătăți performanța aplicației și mentenabilitatea codului. Măsurarea și compararea performanței cu și fără connection pooling.

---

## 🏗️ 2. Migrarea la ORM (Entity Framework Core)

### Maparea Entităților
Am creat clase entitate pentru tabelele `Author` (Părinte), `Book` (Copil) și `Category`.
* **Chei Primare**: Definite prin atributul `[Key]`.
* **Relații 1-la-N**: Un `Author` are o colecție `virtual ICollection<Book>`.
* **Relații M-la-N**: `Book` și `Category` sunt legate printr-un tabel de joncțiune `Books_Categories` configurat în `OnModelCreating`.


# 📄 Raport Laborator 3: Introducere în ORM și Connection Pooling

## 🛠️ Cerințe Preliminare

Pentru a rula acest proiect, veți avea nevoie de următoarele instalate pe mașina locală:
* **Visual Studio 2022/2026** (compatibil cu .NET)
* **.NET SDK** (v8.0 sau mai nou)
* **PostgreSQL** (Server baze de date)
* **Pachete NuGet**: Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.EntityFrameworkCore.Proxies, Microsoft.Extensions.Configuration.Json, Microsoft.EntityFrameworkCore.Design.

---

## 🎯 1. Obiectiv
Refactorizarea aplicației din Laboratorul 1 pentru a utiliza framework-ul **Entity Framework Core (C#)**. Implementarea și configurarea **connection pooling-ului** pentru a îmbunătăți performanța aplicației și mentenabilitatea codului. Măsurarea și compararea performanței cu și fără connection pooling.

---

## 🏗️ 2. Migrarea la ORM (Entity Framework Core)

### Maparea Entităților
Am creat clase entitate pentru tabelele Author (Părinte), Book (Copil) și Category.
* **Chei Primare**: Definite prin atributul [Key].
* **Relații 1-la-N**: Un Author are o colecție virtual ICollection<Book>.
* **Relații M-la-N**: Book și Category sunt legate printr-un tabel de joncțiune Books_Categories configurat în OnModelCreating.

### Interogarea Datelor
Toate interogările SQL manuale din Lab 1 au fost eliminate. Acum utilizăm interogări LINQ puternic tipizate.
* **Create**: context.Books.Add(book).
* **Read**: context.Authors.ToList().
* **Update**: context.UpdateBookWithCategories(...).
* **Delete**: context.Books.Remove(book).

### 📝 Comparație de Cod: Înainte vs. După

Înainte (Lab 1 - SQL Manual/ADO.NET):

    // Procesare manuală cu string-uri SQL și parametri
    string sql = "SELECT * FROM Books WHERE AuthorId = @id";
    using var cmd = new NpgsqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("id", authorId);
    using var reader = cmd.ExecuteReader();
    while (reader.Read()) { /* mapare manuală a rezultatelor */ }

După (Lab 3 - ORM/LINQ):

    // Interogare puternic tipizată fără SQL manual
    return context.Books
        .Include(b => b.Categories)
        .Where(b => b.AuthorId == authorId)
        .ToList();

Analiză: Reducerea codului boilerplate este semnificativă. Se elimină riscul de erori de tipărire în string-urile SQL și se asigură protecție automată împotriva SQL Injection.

---

## ⚡ 3. Connection Pooling

### Configurare Externalizată
Detaliile conexiunii au fost mutate în appsettings.json pentru a nu hardcoda credențialele în codul sursă.
* **Configurare DbContext**: Parametrii pool-ului sunt transmiși prin connection string.
* **Parametri Pool**: Maximum Pool Size=10; Minimum Pool Size=5; Pooling=true;.

### 📊 Sarcina A: Overhead-ul Creării Conexiunilor
Am măsurat timpul pentru crearea a 100 de conexiuni:
* **Fără Pooling**: ~3543 ms (timp mediu: 35.43 ms/conn).
* **Cu Pooling**: ~0 ms (reutilizare instantanee a conexiunilor din pool).

### 🚨 Sarcina B: Detectarea Scurgerilor (Leaks)
S-a simulat un scenariu unde 15 conexiuni sunt deschise fără a fi închise corespunzător.
* **Rezultat**: Pool-ul a devenit epuizat la limita de 10 conexiuni (Max Pool Size), generând o eroare de timeout.
* **Remediu**: Implementarea gestionării corecte a resurselor prin blocuri using (care apelează Dispose()).

---

## 🔍 4. Cerințe Suplimentare (Lazy vs Eager Loading)

* **Lazy Loading**: Configurat să fie implicit pentru relația părinte-copil. Datele legate sunt încărcate doar la prima accesare a proprietății virtuale.
* **Eager Loading**: Implementat în GetBooksByAuthor folosind .Include(b => b.Categories). Aceasta reduce numărul de interogări (N+1) prin executarea unui singur JOIN.
* **Gestionarea Tranzacțiilor**: Toate operațiile de scriere utilizează BeginTransaction() pentru a asigura atomicitatea și consistența datelor.

---

## ⚖️ 5. Analiză Finală

Avantaje ORM:
1. Productivitate crescută (fără SQL manual)
2. Mapare automată a obiectelor la tabele
3. Mentenabilitate și securitate prin LINQ

Dezavantaje ORM:
1. Overhead de performanță la interogări complexe
2. Curba de învățare pentru configurații avansate
3. Control limitat asupra SQL-ului generat

### 🚀 Puncte Bonus Atinse
1. Generarea schemei: Tabelele au fost create automat din clasele entitate folosind Migrations.
2. Logging SQL: Am activat logarea interogărilor SQL generate de ORM în consolă.

---
Concluzie: Implementarea ORM și a Connection Pooling-ului a simplificat codul sursă și a optimizat drastic timpii de răspuns ai aplicației prin reciclarea eficientă a resurselor bazei de date.