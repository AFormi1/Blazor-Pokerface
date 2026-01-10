using SQLite;

namespace Pokerface.Services.DB
{
    public class BaseDataBase
    {
        public string DatabaseFilename { get; private set; }
        public SQLiteAsyncConnection Database { get; private set; }
        public string DBPath { get; private set; }

        public BaseDataBase(string dbName, string dbPath)
        {
            if (string.IsNullOrWhiteSpace(dbName))
                throw new ArgumentNullException(nameof(dbName));

            if (string.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentNullException(nameof(dbPath));

            DatabaseFilename = dbName;
            DBPath = Path.Combine(dbPath, "DB");

            Directory.CreateDirectory(DBPath);

            Database = new SQLiteAsyncConnection(
                Path.Combine(DBPath, DatabaseFilename),
                SQLiteOpenFlags.ReadWrite |
                SQLiteOpenFlags.Create |
                SQLiteOpenFlags.SharedCache);
        }

        public Task Init<T>() where T : new()
            => Database.CreateTableAsync<T>();


        // Generalized SQLite connection
        private SQLiteAsyncConnection CreateDatabaseConnection()
        {
            if (string.IsNullOrWhiteSpace(DBPath))
                throw new ArgumentNullException(nameof(DBPath));

            var fullPath = Path.Combine(DBPath, DatabaseFilename);
            return new SQLiteAsyncConnection(
                fullPath,
                SQLiteOpenFlags.ReadWrite |
                SQLiteOpenFlags.Create |
                SQLiteOpenFlags.SharedCache);
        }

       
        // Generalized Delete method for deleting the specific database file for the type T
        public async Task DeleteDBFiles<T>()
        {
            if (string.IsNullOrWhiteSpace(DBPath))
                throw new ArgumentNullException(nameof(DBPath));

            var dbPath = Path.Combine(DBPath, DatabaseFilename);

            if (File.Exists(dbPath))
            {
                try
                {
                    // Close and dispose of the SQLite connection if open
                    var database = CreateDatabaseConnection();
                    await database.CloseAsync();

                    // Delete the database file
                    File.Delete(dbPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting database file {DatabaseFilename}: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"Error: {DatabaseFilename} does not exist.");
            }
        }

        public async Task<List<T>> GetItemsAsync<T>() where T : new()
        {
            if (Database == null)
                throw new ArgumentNullException("Database s null");

            // Get the list of items from the database table of type T
            List<T> tempList = await Database.Table<T>().ToListAsync();

            // Create an observable collection to return the data
            List<T> returnCollection = new();

            // If the list is null, return an empty List
            if (tempList == null)
                return new List<T>();

            // Iterate over the list and add each item to the List
            foreach (T item in tempList)
            {
                returnCollection.Add(item);
            }

            // Return the populated List
            return returnCollection;
        }

        public async Task<T?> GetItemByPredicateAsync<T>(Func<T, bool> predicate) where T : class, new()
        {
            if (Database is null)
                throw new InvalidOperationException("Database is not initialized.");

            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            var items = await Database.Table<T>().ToListAsync();

            return items.FirstOrDefault(predicate);
        }


        public async Task<int> SaveItemAsync<T>(T item, Func<T, bool> predicate) where T : class, new()
        {
            if (Database is null)
                throw new InvalidOperationException("Database is not initialized.");

            if (item is null)
                throw new ArgumentNullException(nameof(item));

            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            var list = await Database.Table<T>().ToListAsync();
            var existingItem = list.FirstOrDefault(predicate);

            return existingItem is not null
                ? await Database.UpdateAsync(item)
                : await Database.InsertAsync(item);
        }


        public async Task<int> DeleteItemAsync<T>(Func<T, bool> predicate) where T : class, new()
        {
            if (Database is null)
                throw new InvalidOperationException("Database is not initialized.");

            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            var list = await Database.Table<T>().ToListAsync();
            var itemToDelete = list.FirstOrDefault(predicate);

            if (itemToDelete is not null)
            {
                Console.WriteLine($"Item deleted: {typeof(T).Name}");
                return await Database.DeleteAsync(itemToDelete);
            }

            Console.WriteLine($"No item found to delete: {typeof(T).Name}");
            return 0;
        }


        public async Task<int> UpdateItemPropertyAsync<T>(Func<T, bool> predicate, Action<T> updateAction) where T : class, new()
        {
            if (Database is null)
                throw new InvalidOperationException("Database is not initialized.");

            // Load items from the database
            var list = await Database.Table<T>().ToListAsync();

            // Find the target item
            var existingItem = list.FirstOrDefault(predicate);

            if (existingItem != null)
            {
                // Apply the update
                updateAction(existingItem);

                // Save changes
                return await Database.UpdateAsync(existingItem);
            }

            // Not found
            return 0;
        }

        public async Task<List<T>> GetItemsByPredicateAsync<T>(Func<T, bool> predicate) where T : class, new()
        {
            // Get all items as an List
            List<T> allItems = await GetItemsAsync<T>();

            // Filter the items based on the predicate
            IEnumerable<T> filteredItems = allItems.Where(predicate);

            // Return the filtered items as an List
            return new List<T>(filteredItems);
        }

        public async Task<bool> IsItemUsedInOtherItemsAsync<TItem, TReference>(
            int? referenceId,
            int? excludingItemId,
            Func<TItem, IEnumerable<TReference>> itemReferenceSelector,
            Func<TReference, int?> referenceIdSelector,
            Func<TItem, int?> itemIdSelector)  // New Func to get the ID of the item
            where TItem : class, new()
        {
            // Retrieve all items of type TItem except the one being saved
            var allItems = await GetItemsAsync<TItem>();
            var otherItems = allItems.Where(item => itemIdSelector(item) != excludingItemId);

            // Check if any other item contains the reference that matches the referenceId
            return otherItems.Any(item => itemReferenceSelector(item)?.Any(reference => referenceIdSelector(reference) == referenceId) == true);
        }

        public async Task<int> GetCountAsync<T>() where T : new()
        {
            if (Database == null)
                return 0;

            return await Database.Table<T>().CountAsync();
        }

    }
}
