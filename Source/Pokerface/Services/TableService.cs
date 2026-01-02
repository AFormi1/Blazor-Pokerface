using Pokerface.Components.Pages;
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
            return [.. items.OrderBy(x => x.Name).ThenBy(x => x.Id)];
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
                Name = "Neuer Tisch",
                IsActive = false,
                MaxUsers = 8,
                CurrentUsers = 0
            };

            return newTable;
        }

        public async Task<int> GetNextTableIdAsync()
        {
            var count = await _tableService.GetCountAsync();
                       
            return count + 1;
        }
    }
}
