
using Pokerface.Models;

namespace Pokerface.Services.DB
{
    public class DbTableService
    {
        public BaseDataBase DataBase { get; }

        public DbTableService(BaseDataBase dataBase)
        {
            DataBase = dataBase ?? throw new ArgumentNullException(nameof(dataBase));
        }

        public async Task Init()
        {
            await DataBase.Init<TableModel>();

            var tables = await GetItemsAsync();
            foreach (var table in tables)
            {
                await SaveItemAsync(table);
            }
        }

        public async Task<List<TableModel>> GetItemsAsync() => await DataBase.GetItemsAsync<TableModel>();

        public async Task<TableModel?> GetItemByIdAsync(int id)
        {
            return await DataBase.GetItemByPredicateAsync<TableModel>(u => u.Id == id);
        }

        public async Task<int> SaveItemAsync(TableModel item) => await DataBase.SaveItemAsync(item, x => x.Id == item.Id);

        public async Task<int> DeleteItemAsync(TableModel item) => await DataBase.DeleteItemAsync<TableModel>(x => x.Id == item.Id);
        public async Task<int> GetCountAsync() => await DataBase.GetCountAsync<TableModel>();

    }
}
