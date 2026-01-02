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
            return await _tableService.GetItemsAsync();
        }

        public async Task<TableModel?> GetTableByIdAsync(Guid id)
        {
            return await _tableService.GetItemByIdAsync(id);
        }

        public async Task<bool> SaveTableAsync(TableModel model)
        {
            return await _tableService.SaveItemAsync(model) > 0;
        }

        public async Task<bool> IsTableNameUniqueAsync(string desiredName)
        {
            var tables = await _tableService.GetItemsAsync();
            return !tables.Any(t => t.Name.Equals(desiredName, StringComparison.OrdinalIgnoreCase));
        }

        public TableModel CreateNewTable()
        {
            TableModel newTable = new TableModel
            {
                Id = Guid.NewGuid(),
                Name = "Neuer Tisch",
                IsActive = false,
                MaxUsers = 8,
                CurrentUsers = 0
            };

            return newTable;
        }
    }
}
