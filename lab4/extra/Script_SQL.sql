
CREATE TABLE Authors (
    Id SERIAL PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    Nationality VARCHAR(50)
);

CREATE TABLE Books (
    Id SERIAL PRIMARY KEY,
    Title VARCHAR(200) NOT NULL,
    PublicationYear INT,
    AuthorId INT NOT NULL,
    CONSTRAINT fk_author FOREIGN KEY (AuthorId) REFERENCES Authors(Id) ON DELETE CASCADE
);

CREATE TABLE Categories (
    Id SERIAL PRIMARY KEY,
    Name VARCHAR(50) NOT NULL
);

CREATE TABLE Books_Categories (
    BookId INT NOT NULL,
    CategoryId INT NOT NULL,
    PRIMARY KEY (BookId, CategoryId),
    CONSTRAINT fk_book FOREIGN KEY (BookId) REFERENCES Books(Id) ON DELETE CASCADE,
    CONSTRAINT fk_category FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE CASCADE
);

INSERT INTO Authors (Name, Nationality) VALUES 
('Isaac Asimov', 'American'),
('Frank Herbert', 'American'),
('Mircea Eliade', 'Romanian'),
('J.R.R. Tolkien', 'British'),
('Agatha Christie', 'British'),
('George Orwell', 'British');

INSERT INTO Books (Title, PublicationYear, AuthorId) VALUES 
('Foundation', 1951, 1),
('Foundation and Empire', 1952, 1),
('Second Foundation', 1953, 1),
('Dune', 1965, 2),
('Dune Messiah', 1969, 2),
('Children of Dune', 1976, 2),
('God Emperor of Dune', 1981, 2),
('Maitreyi', 1933, 3),
('The Forbidden Forest', 1955, 3),
('The Hobbit', 1937, 4),
('The Fellowship of the Ring', 1954, 4),
('Murder on the Orient Express', 1934, 5),
('1984', 1949, 6),
('Animal Farm', 1945, 6);

INSERT INTO Categories (Name) VALUES 
('Science Fiction'),
('Philosophy'),
('Classic'),
('Fantasy'),
('Mystery'),
('Dystopian');

INSERT INTO Books_Categories (BookId, CategoryId) VALUES
(1, 1), (2, 1), (3, 1),
(4, 1), (4, 2), (5, 1), (6, 1), (7, 1),
(8, 3), (9, 3), (9, 2),
(10, 4), (11, 4),
(12, 5), (12, 3),
(13, 6), (13, 1),
(14, 6);