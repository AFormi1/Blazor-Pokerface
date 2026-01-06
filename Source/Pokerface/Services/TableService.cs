using Pokerface.Models;
using Pokerface.Services.DB;

namespace Pokerface.Services
{
    public class TableService
    {
        private readonly DbTableService _tableService;
        public TableService(DbTableService tableService)
        {
            _tableService = tableService;
        }

        public async Task<List<TableModel>> GetTablesAsync()
        {
            var items = await _tableService.GetItemsAsync();

            return [.. items
                .OrderBy(t => GetNamePrefix(t.Name))
                .ThenBy(t => GetNumberFromName(t.Name))
                .ThenBy(t => t.Id)];
        }

        public async Task<TableModel?> GetTableByIdAsync(int id)
        {
            return await _tableService.GetItemByIdAsync(id);
        }

        public async Task<bool> SaveTableAsync(TableModel model)
        {
           return await _tableService.SaveItemAsync(model) > 0;
        }

        public async Task<bool> DeleteTableAsync(TableModel model)
        {
            return await _tableService.DeleteItemAsync(model) > 0;
        }

        public async Task<bool> IsTableNameUniqueAsync(int id, string desiredName)
        {
            var tables = await _tableService.GetItemsAsync();
            return !tables.Where(x => x.Id != id).Any(t => t.Name.Equals(desiredName, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<TableModel> CreateNewTableAsync()
        {
            int nextID = await GetNextTableIdAsync();

            TableModel newTable = new TableModel
            {
                Id = nextID,
                Name = $"Neuer Tisch {nextID}",
                CurrentPlayers = 0
            };

            return newTable;
        }

        public async Task<int> GetNextTableIdAsync()
        {
            var tables = await GetTablesAsync();

            var usedIds = tables
                .Select(t => t.Id)
                .Where(id => id > 0)
                .OrderBy(id => id)
                .ToList();

            int expectedId = 1;

            foreach (var id in usedIds)
            {
                if (id == expectedId)
                {
                    expectedId++;
                }
                else if (id > expectedId)
                {
                    // gap found
                    break;
                }
            }

            return expectedId;
        }

        private static string GetNamePrefix(string name)
        {
            // everything before the number
            return new string(name.TakeWhile(c => !char.IsDigit(c)).ToArray()).Trim();
        }

        private static int GetNumberFromName(string name)
        {
            var numberPart = new string(name.Where(char.IsDigit).ToArray());
            return int.TryParse(numberPart, out var number) ? number : int.MaxValue;
        }

    }
}
