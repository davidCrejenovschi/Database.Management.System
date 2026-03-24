using System.Windows;
using System.Windows.Controls;
using BibliotecaApp.DataBase;

namespace BibliotecaApp
{
    public partial class MainWindow : Window
    {
        private LibraryRepository _repository;
        private List<Author> _allAuthors;

        public MainWindow()
        {
            InitializeComponent();
            _repository = new LibraryRepository();
            _allAuthors = new List<Author>();
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                _allAuthors = _repository.GetAllAuthors();
                dgAuthors.ItemsSource = _allAuthors;
                lstCategories.ItemsSource = _repository.GetAllCategories();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Loading Data", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Clear();
            LoadData();
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = txtSearch.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                dgAuthors.ItemsSource = _allAuthors;
            }
            else
            {
                var filteredList = _allAuthors.Where(a => a.Name.ToLower().Contains(searchText)).ToList();
                dgAuthors.ItemsSource = filteredList;
            }
        }

        private void dgAuthors_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshBooksGrid();
            ClearForm();
        }

        private void RefreshBooksGrid()
        {
            if (dgAuthors.SelectedItem is Author selectedAuthor)
            {
                try
                {
                    dgBooks.ItemsSource = _repository.GetBooksByAuthor(selectedAuthor.Id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                dgBooks.ItemsSource = null;
            }
        }

        private void dgBooks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgBooks.SelectedItem is Book selectedBook)
            {
                txtTitle.Text = selectedBook.Title;
                txtYear.Text = selectedBook.PublicationYear.ToString();

                lstCategories.SelectedItems.Clear();
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
            if (dgAuthors.SelectedItem is not Author selectedAuthor)
            {
                MessageBox.Show("Please select an author first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ValidateInput(out int validYear))
            {
                return;
            }

            try
            {
                var newBook = new Book
                {
                    Title = txtTitle.Text,
                    PublicationYear = validYear,
                    AuthorId = selectedAuthor.Id
                };

                var selectedCategoryIds = lstCategories.SelectedItems.Cast<Category>().Select(c => c.Id).ToList();

                _repository.AddBookWithCategories(newBook, selectedCategoryIds);
                RefreshBooksGrid();
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

            if (!ValidateInput(out int validYear))
            {
                return;
            }

            try
            {
                selectedBook.Title = txtTitle.Text;
                selectedBook.PublicationYear = validYear;

                var selectedCategoryIds = lstCategories.SelectedItems.Cast<Category>().Select(c => c.Id).ToList();

                _repository.UpdateBookWithCategories(selectedBook, selectedCategoryIds);
                RefreshBooksGrid();
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
                    RefreshBooksGrid();
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
            testWindow.Show(); // Folosim Show() in loc de ShowDialog() ca sa poti umbla in ambele ferestre simultan daca vrei
        }
    }
}