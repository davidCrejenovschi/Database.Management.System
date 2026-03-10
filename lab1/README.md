# Aplicație Gestiune unei Biblioteci

Aceasta este o aplicație desktop (WPF) dezvoltată în C# și .NET 10, creată pentru a gestiona relațiile dintre Autori și Cărțile acestora într-o bază de date PostgreSQL. Proiectul implementează un model de vizualizare Master-Detail și operații CRUD, gestionând nativ relații de tip 1:N și M:N prin interogări SQL scrise manual (ADO.NET), fără a utiliza un framework ORM.

## 🛠️ Cerințe Preliminare

Pentru a rula acest proiect, veți avea nevoie de următoarele instalate pe mașina locală:
* **Visual Studio 2022** (sau o versiune compatibilă cu .NET 10)
* **.NET 10.0 SDK**
* **PostgreSQL** (serverul de baze de date)
* **pgAdmin 4** (recomandat pentru vizualizarea și rularea scriptului SQL)

## 🗄️ Configurarea Bazei de Date

1. Deschideți pgAdmin și conectați-vă la serverul local PostgreSQL.
2. Creați o bază de date nouă cu numele `library` (sau folosiți baza de date implicită `postgres`).
3. Deschideți fișierul `Script_SQL.sql` inclus în rădăcina proiectului.
4. Rulați întregul script. Acesta va crea automat tabelele necesare (`Authors`, `Books`, `Categories`, `Books_Categories`) și va popula baza de date cu date de test.

## ⚙️ Configurarea Aplicației

Înainte de a rula proiectul, trebuie să vă asigurați că aplicația se poate conecta la baza de date locală:
1. Deschideți soluția `BibliotecaApp.sln` în Visual Studio.
2. Navigați în folderul `DataBase` (sau locația relevantă) și deschideți fișierul `DatabaseManager.cs`.
3. Căutați variabila `_connectionString`.
4. Actualizați câmpurile `Username`, `Password` și `Database` cu datele specifice mediului dumneavoastră local:
   ```csharp
   private readonly string _connectionString = "Host=localhost;Username=postgres;Password=ParolaDumneavoastra;Database=library";