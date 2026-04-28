using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BibliotecaApp.DataBase;

namespace BibliotecaApp
{
    public partial class MainWindow : Window
    {
        private readonly LibraryRepository _repository;

        private List<Author> _currentAuthorsPage;
        private int _authorsPageNumber = 1;
        private int _authorsPageSize = 10;
        private int _authorsTotalPages = 1;
        private int _currentAuthorId = 0;
        private int _booksPageSize = 10;
        private int _booksLastId = 0; 
        private Stack<int> _booksHistory = new Stack<int>(); 

        public MainWindow()
        {
            InitializeComponent();
            _repository = new LibraryRepository();
            _currentAuthorsPage = new List<Author>();

            // Wait for UI to load before parsing ComboBoxes
            this.Loaded += (s, e) => LoadInitialData();
        }

        private void LoadInitialData()
        {
            try
            {
                lstCategories.ItemsSource = _repository.GetAllCategories();
                UpdatePaginationSizes();
                LoadAuthorsPage();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Loading Data", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdatePaginationSizes()
        {
            if (cmbAuthorsPageSize.SelectedItem is ComboBoxItem authorItem &&
                int.TryParse(authorItem.Content?.ToString(), out int aSize))
            {
                _authorsPageSize = aSize;
            }

            if (cmbBooksPageSize.SelectedItem is ComboBoxItem bookItem &&
                int.TryParse(bookItem.Content?.ToString(), out int bSize))
            {
                _booksPageSize = bSize;
            }
        }

        private void LoadAuthorsPage()
        {
            try
            {
                var result = _repository.GetAuthorsPage(_authorsPageNumber, _authorsPageSize);

                _currentAuthorsPage = result.Items;
                dgAuthors.ItemsSource = _currentAuthorsPage;

                // Calculate total pages
                _authorsTotalPages = (int)Math.Ceiling((double)result.TotalRecords / _authorsPageSize);
                if (_authorsTotalPages == 0) _authorsTotalPages = 1;

                txtAuthorsPageInfo.Text = $"Page {_authorsPageNumber} of {_authorsTotalPages} (Total: {result.TotalRecords})";

                // Enable/Disable buttons
                btnAuthorsPrev.IsEnabled = _authorsPageNumber > 1;
                btnAuthorsNext.IsEnabled = _authorsPageNumber < _authorsTotalPages;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnAuthorsPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_authorsPageNumber > 1)
            {
                _authorsPageNumber--;
                LoadAuthorsPage();
            }
        }

        private void btnAuthorsNext_Click(object sender, RoutedEventArgs e)
        {
            if (_authorsPageNumber < _authorsTotalPages)
            {
                _authorsPageNumber++;
                LoadAuthorsPage();
            }
        }

        private void cmbAuthorsPageSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                UpdatePaginationSizes();
                _authorsPageNumber = 1; // Reset to page 1 on resize
                LoadAuthorsPage();
            }
        }

        private void LoadBooksPage()
        {
            if (_currentAuthorId == 0)
            {
                dgBooks.ItemsSource = null;
                txtBooksPageInfo.Text = "Loaded: 0";
                btnBooksPrev.IsEnabled = false;
                btnBooksNext.IsEnabled = false;
                return;
            }

            try
            {
                var result = _repository.GetBooksByAuthorAfter(_currentAuthorId, _booksLastId, _booksPageSize);

                dgBooks.ItemsSource = result.Items;
                txtBooksPageInfo.Text = $"Loaded: {result.Items.Count} items";

                // We can go backwards if we have history
                btnBooksPrev.IsEnabled = _booksHistory.Count > 0;

                // We can go forwards if the page is full (suggesting there might be more)
                btnBooksNext.IsEnabled = result.Items.Count == _booksPageSize;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnBooksNext_Click(object sender, RoutedEventArgs e)
        {
            var currentItems = dgBooks.ItemsSource as List<Book>;
            if (currentItems != null && currentItems.Any())
            {
                // Save current state to history so we can go back
                _booksHistory.Push(_booksLastId);

                // Set the cursor to the ID of the last item in the current view
                _booksLastId = currentItems.Last().Id;

                LoadBooksPage();
            }
        }

        private void btnBooksPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_booksHistory.Count > 0)
            {
                // Pop the last used cursor to go back
                _booksLastId = _booksHistory.Pop();
                LoadBooksPage();
            }
        }

        private void cmbBooksPageSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && _currentAuthorId != 0)
            {
                UpdatePaginationSizes();
                ResetBooksPagination();
            }
        }

        private void ResetBooksPagination()
        {
            _booksLastId = 0;
            _booksHistory.Clear();
            LoadBooksPage();
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Clear();
            _authorsPageNumber = 1;
            LoadAuthorsPage();
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Note: Since we are paginating the database, filtering here 
            // only filters the CURRENT PAGE in memory.
            string searchText = txtSearch.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                dgAuthors.ItemsSource = _currentAuthorsPage;
            }
            else
            {
                var filteredList = _currentAuthorsPage
                    .Where(a => a.Name.ToLower().Contains(searchText))
                    .ToList();
                dgAuthors.ItemsSource = filteredList;
            }
        }

        private void dgAuthors_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgAuthors.SelectedItem is Author selectedAuthor)
            {
                _currentAuthorId = selectedAuthor.Id;
                ResetBooksPagination(); // Start fresh for the new author
            }
            else
            {
                _currentAuthorId = 0;
                ResetBooksPagination();
            }
            ClearForm();
        }

        private void dgBooks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgBooks.SelectedItem is Book selectedBook)
            {
                txtTitle.Text = selectedBook.Title;
                txtYear.Text = selectedBook.PublicationYear.ToString();

                lstCategories.SelectedItems.Clear();

                if (selectedBook.Categories != null)
                {
                    foreach (var cat in selectedBook.Categories)
                    {
                        foreach (Category item in lstCategories.Items)
                        {
                            if (item.Id == cat.Id)
                            {
                                lstCategories.SelectedItems.Add(item);
                            }
                        }
                    }
                }
            }
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            dgBooks.SelectedItem = null;
            txtTitle.Clear();
            txtYear.Clear();
            lstCategories.SelectedItems.Clear();
        }

        private bool ValidateInput(out int validYear)
        {
            validYear = 0;
            if (string.IsNullOrWhiteSpace(txtTitle.Text) || txtTitle.Text.Length < 2)
            {
                MessageBox.Show("Title must be at least 2 characters long.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!int.TryParse(txtYear.Text, out validYear))
            {
                MessageBox.Show("Year must be a valid number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (validYear < 1000 || validYear > 2026)
            {
                MessageBox.Show("Please enter a valid publication year (between 1000 and 2026).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (_currentAuthorId == 0)
            {
                MessageBox.Show("Please select an author first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ValidateInput(out int validYear)) return;

            try
            {
                var newBook = new Book
                {
                    Title = txtTitle.Text,
                    PublicationYear = validYear,
                    AuthorId = _currentAuthorId
                };

                var selectedCategoryIds = lstCategories.SelectedItems.Cast<Category>().Select(c => c.Id).ToList();

                _repository.AddBookWithCategories(newBook, selectedCategoryIds);

                // After adding, reset pagination to see the new data
                ResetBooksPagination();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (dgBooks.SelectedItem is not Book selectedBook)
            {
                MessageBox.Show("Please select a book to update.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ValidateInput(out int validYear)) return;

            try
            {
                selectedBook.Title = txtTitle.Text;
                selectedBook.PublicationYear = validYear;

                var selectedCategoryIds = lstCategories.SelectedItems.Cast<Category>().Select(c => c.Id).ToList();

                _repository.UpdateBookWithCategories(selectedBook, selectedCategoryIds);

                // Refresh the current page view
                LoadBooksPage();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgBooks.SelectedItem is not Book selectedBook)
            {
                MessageBox.Show("Please select a book to delete.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"Are you sure you want to delete '{selectedBook.Title}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _repository.DeleteBook(selectedBook.Id);

                    ResetBooksPagination(); // Safest way to handle state after deletion
                    ClearForm();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void btnOpenTests_Click(object sender, RoutedEventArgs e)
        {
            TestWindow testWindow = new TestWindow();
            testWindow.Show();
        }
    }
}